using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum WorldAction { AddDomain, Bind, DeliverTribute, OpenAttack, SendRelief, Advance, DemandCure, Secede, Release, CureProtection }
public enum TributeTransport { SubjectDelivery, LiegeCollection }
public enum AttackState { AwaitingRelief, Relieved, Captured }
public enum ArrearsState { Upcoming, Grace, Overdue, Paid }

/// <summary>Stable political identity, separate from native Faction ownership. Never changes player control.</summary>
public sealed class PoliticalDomain
{
    public string Id { get; internal set; }
    public string NativeFactionId { get; internal set; }
    public bool Player { get; internal set; }
    public bool InUnion { get; internal set; }
    public string Parent { get; internal set; }
    public decimal Honor { get; internal set; } = .5m;
    public int Troops { get; internal set; }
    public int Committed { get; internal set; }
    public int Available => Troops - Committed;
    public decimal Prosperity { get; internal set; } = .5m;
}

public sealed class PoliticalContract
{
    public string Id { get; internal set; }
    public string Liege { get; internal set; }
    public string Subject { get; internal set; }
    public decimal TributeValue { get; internal set; }
    public long Interval { get; internal set; }
    public long Grace { get; internal set; }
    public long NextDue { get; internal set; }
    public TributeTransport Transport { get; internal set; }
    public decimal TransportRate { get; internal set; }
    public bool Active { get; internal set; } = true;
    public bool ProtectionBreached { get; internal set; }
    public long? CureUntil { get; internal set; }
    public decimal Trust { get; internal set; } = .5m;
    public decimal DueValue => Transport == TributeTransport.LiegeCollection
        ? TributeValue * (1 + TransportRate) : TributeValue * (1 - TransportRate);
}

public sealed class TributeAssessment
{
    public string Id { get; internal set; }
    public string Contract { get; internal set; }
    public long Due { get; internal set; }
    public long GraceUntil { get; internal set; }
    public decimal Principal { get; internal set; }
    public decimal DeliveryValue { get; internal set; }
    public bool Paid { get; internal set; }
    public bool OverdueRecorded { get; internal set; }
    public ArrearsState State(long now) => Paid ? ArrearsState.Paid : now < Due ? ArrearsState.Upcoming
        : now <= GraceUntil ? ArrearsState.Grace : ArrearsState.Overdue;
}

public sealed class PoliticalAttack
{
    public string Id { get; internal set; }
    public string Attacker { get; internal set; }
    public string Target { get; internal set; }
    public string ProtectionContract { get; internal set; }
    public int Strength { get; internal set; }
    public long Deadline { get; internal set; }
    public AttackState State { get; internal set; }
    internal Dictionary<string, int> Relief { get; } = new(StringComparer.Ordinal);
    public int ReliefTroops => Relief.Values.Sum();
}

/// <summary>Replayable commands are development-model evidence, NOT verified game events or player consent.</summary>
public sealed record WorldCommand(WorldAction Action, long At, string Id = null, string Actor = null,
    string Target = null, string Evidence = null, decimal Value = 0, int Count = 0,
    long Interval = 0, long Grace = 0, decimal Rate = 0, bool Flag = false);

public sealed record PoliticalWorldSave(int Version, long TurnTicks, WorldCommand[] Commands);

/// <summary>
/// Broad deterministic development slice. Contains no engine resource transactions, Faction.SetFaction,
/// arbitrary combat results, automatic player defeat, or honor-only justified secession.
/// All mutation is validated then journaled; replay uses the same validation path.
/// </summary>
public sealed class PoliticalWorld
{
    public const long Day = 60000;
    public const int SchemaVersion = 1;
    public const int MaxCommands = 10000;
    private readonly Dictionary<string, PoliticalDomain> domains = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PoliticalContract> contracts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TributeAssessment> assessments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PoliticalAttack> attacks = new(StringComparer.Ordinal);
    private readonly HashSet<string> receipts = new(StringComparer.Ordinal);
    private readonly List<WorldCommand> commands = [];
    public IReadOnlyCollection<PoliticalDomain> Domains => domains.Values.ToArray();
    public IReadOnlyCollection<PoliticalContract> Contracts => contracts.Values.ToArray();
    public IReadOnlyCollection<TributeAssessment> Assessments => assessments.Values.ToArray();
    public IReadOnlyCollection<PoliticalAttack> Attacks => attacks.Values.ToArray();
    public long Now { get; private set; }
    public long TurnTicks { get; }
    public long NextTurn { get; private set; }

    public PoliticalWorld(long turnTicks = Day)
    {
        if (turnTicks < Day / 4 || turnTicks > Day * 10) throw new ArgumentOutOfRangeException(nameof(turnTicks));
        TurnTicks = turnTicks; NextTurn = turnTicks;
    }

    public PoliticalDomain Domain(string id) => domains.TryGetValue(id ?? "", out var value)
        ? value : throw new ArgumentException("Unknown political domain.");
    public string Root(string id)
    {
        var cursor = Domain(id);
        while (cursor.Parent != null) cursor = Domain(cursor.Parent);
        return cursor.Id;
    }
    public string DiplomaticGroup(string id) => Domain(Root(id)).InUnion ? "free-city-union" : Root(id);
    public int Depth(string id) => Domain(id).Parent is string p ? 1 + Depth(p) : 1;
    private PoliticalDomain[] Subtree(string id) => domains.Values.Where(d => d.Id == id || IsBelow(d.Id, id)).ToArray();
    private bool IsBelow(string id, string ancestor)
    {
        var cursor = Domain(id);
        while (cursor.Parent != null) { if (cursor.Parent == ancestor) return true; cursor = Domain(cursor.Parent); }
        return false;
    }
    private PoliticalContract CurrentContract(string subject) => contracts.Values.SingleOrDefault(c => c.Active && c.Subject == subject);
    private bool Busy(string id) => attacks.Values.Any(a => a.State == AttackState.AwaitingRelief &&
        (a.Target == id || a.Attacker == id || a.Relief.ContainsKey(id)));
    private static void Key(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100 || id.Any(char.IsControl)) throw new ArgumentException("Invalid stable ID.");
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new ArgumentException(reason); }

    public void Apply(WorldCommand command)
    {
        RuntimeCompat.NotNull(command);
        Require(RuntimeCompat.IsDefined(command.Action) && commands.Count < MaxCommands, "Unknown action or journal capacity reached.");
        Require(command.At == Now || command.Action == WorldAction.Advance && command.At > Now,
            "Only Advance may move model time. Other commands must use current time.");
        switch (command.Action)
        {
            case WorldAction.AddDomain:
                Key(command.Id); Key(command.Actor);
                Require(!domains.ContainsKey(command.Id) && domains.Count < 200 && command.Count is >= 0 and <= 100000,
                    "Duplicate domain, capacity, or invalid army.");
                Require(!command.Flag || !domains.Values.Any(d => d.Player), "One player political identity covers all player settlements.");
                domains.Add(command.Id, new() { Id = command.Id, NativeFactionId = command.Actor,
                    Player = command.Flag, InUnion = command.Target == "union", Troops = command.Count });
                break;
            case WorldAction.Bind:
                Bind(command); break;
            case WorldAction.DeliverTribute:
                Deliver(command); break;
            case WorldAction.OpenAttack:
                OpenAttack(command); break;
            case WorldAction.SendRelief:
                SendRelief(command); break;
            case WorldAction.Advance:
                Advance(command.At); break;
            case WorldAction.DemandCure:
            {
                var contract = CurrentContract(command.Actor);
                Require(contract != null && contract.ProtectionBreached && contract.CureUntil == null,
                    "A verified model protection breach is needed; low honor alone is insufficient.");
                Require(command.Interval >= Day && command.Interval <= Day * 30 && command.Interval <= long.MaxValue - Now,
                    "A bounded real-time cure period is required.");
                contract.CureUntil = Now + command.Interval;
                break;
            }
            case WorldAction.Secede:
            {
                var contract = CurrentContract(command.Actor);
                Require(contract != null && contract.ProtectionBreached && contract.CureUntil.HasValue && Now > contract.CureUntil,
                    "Justified exit requires an unresolved serious breach and an elapsed delivered cure notice.");
                EndContract(contract); break;
            }
            case WorldAction.CureProtection:
            {
                Key(command.Evidence);
                var contract = CurrentContract(command.Actor);
                Require(contract != null && contract.ProtectionBreached && command.Target == contract.Liege && command.Flag &&
                    !receipts.Contains(command.Evidence), "Both parties must accept a fresh remedy for the recorded breach.");
                receipts.Add(command.Evidence); contract.ProtectionBreached = false; contract.CureUntil = null;
                contract.Trust = Math.Min(1, contract.Trust + .05m);
                break;
            }
            case WorldAction.Release:
            {
                var contract = CurrentContract(command.Actor);
                Require(contract != null && command.Target == contract.Liege && command.Flag,
                    "Mutual release requires both parties' agreement in this development protocol.");
                EndContract(contract); break;
            }
        }
        // No decisions occur between consecutive time advances. Coalesce bounded spans to avoid
        // filling saves with per-tick commands while preserving deterministic chronological replay.
        if (command.Action == WorldAction.Advance && commands.Count > 0 && commands[^1].Action == WorldAction.Advance &&
            command.At - (commands.Count > 1 ? commands[^2].At : 0) <= Day * 120)
            commands[^1] = command;
        else commands.Add(command);
    }

    private void Bind(WorldCommand c)
    {
        Key(c.Id);
        var subject = Domain(c.Actor); var liege = Domain(c.Target);
        Require(!contracts.ContainsKey(c.Id) && CurrentContract(subject.Id) == null && subject.Parent == null,
            "Release the existing bond before changing liege; historical IDs cannot be reused.");
        Require(subject.Id != liege.Id && !IsBelow(liege.Id, subject.Id), "Cyclic vassalage.");
        int subtreeHeight = Subtree(subject.Id).Max(d => Depth(d.Id));
        Require(Depth(liege.Id) + subtreeHeight <= 4, "Maximum four levels, including root and moving subtree.");
        Require(!Subtree(subject.Id).Any(d => Busy(d.Id)), "Settle ongoing attacks and deployments before transfer.");
        Require(c.Value > 0 && c.Value <= 10000000 && c.Interval is >= Day and <= Day * 60 &&
            c.Grace is >= 0 and <= Day * 30 && c.Rate is >= 0 and <= .5m &&
            c.Interval <= long.MaxValue - Now && c.Grace <= long.MaxValue - Now - c.Interval,
            "Invalid fixed-value tribute terms.");
        var contract = new PoliticalContract { Id = c.Id, Liege = liege.Id, Subject = subject.Id,
            TributeValue = c.Value, Interval = c.Interval, Grace = c.Grace, NextDue = Now + c.Interval,
            Transport = c.Flag ? TributeTransport.LiegeCollection : TributeTransport.SubjectDelivery, TransportRate = c.Rate,
            Trust = liege.Honor };
        contracts.Add(c.Id, contract); subject.Parent = liege.Id;
        subject.InUnion = false;
        Schedule(contract);
    }

    private void Schedule(PoliticalContract c)
    {
        string id = $"{c.Id}@{c.NextDue}";
        assessments.Add(id, new() { Id = id, Contract = c.Id, Due = c.NextDue,
            GraceUntil = checked(c.NextDue + c.Grace), Principal = c.TributeValue, DeliveryValue = c.DueValue });
    }

    private void Deliver(WorldCommand c)
    {
        Key(c.Evidence);
        Require(assessments.TryGetValue(c.Id ?? "", out var bill), "Unknown assessment.");
        var contract = contracts[bill.Contract];
        Require(contract.Active && c.Actor == contract.Subject && !bill.Paid && Now >= bill.Due,
            "Only an unpaid due assessment of the active subject can be fulfilled.");
        // Value is the adapter's sum(quantity * actual agreed trade price), not an inventory or silver balance.
        Require(c.Value >= bill.DeliveryValue && c.Value <= 100000000 && !receipts.Contains(c.Evidence),
            "Insufficient delivered trade value or reused global receipt.");
        receipts.Add(c.Evidence); bill.Paid = true;
        contract.Trust = Math.Min(1, contract.Trust + .02m);
        Domain(c.Actor).Honor = Math.Min(1, Domain(c.Actor).Honor + .01m);
        // No credit beyond this obligation, no offset against unrelated unpaid bills.
    }

    private void OpenAttack(WorldCommand c)
    {
        Key(c.Id);
        var attacker = Domain(c.Actor); var target = Domain(c.Target);
        Require(!attacks.ContainsKey(c.Id) && !target.Player && attacker.Id != target.Id &&
            DiplomaticGroup(attacker.Id) != DiplomaticGroup(target.Id),
            "Duplicate or internal attack; player defeat requires actual gameplay, not the model.");
        Require(!Busy(target.Id) && c.Count > 0 && c.Count <= attacker.Available &&
            c.Interval is >= Day and <= Day * 30 && c.Interval <= long.MaxValue - Now,
            "Invalid attacking deployment or response deadline.");
        attacks.Add(c.Id, new() { Id = c.Id, Attacker = attacker.Id, Target = target.Id,
            Strength = c.Count, Deadline = Now + c.Interval, ProtectionContract = CurrentContract(target.Id)?.Id });
        attacker.Committed += c.Count;
    }

    private void SendRelief(WorldCommand c)
    {
        Require(attacks.TryGetValue(c.Id ?? "", out var attack) && attack.State == AttackState.AwaitingRelief && Now <= attack.Deadline,
            "No live relief request.");
        var helper = Domain(c.Actor);
        Require(helper.Id != attack.Target && DiplomaticGroup(helper.Id) == DiplomaticGroup(attack.Target) &&
            c.Count > 0 && c.Count <= helper.Available && !attack.Relief.ContainsKey(helper.Id),
            "Relief must be a distinct eligible ally with available, unallocated troops.");
        attack.Relief.Add(helper.Id, c.Count); helper.Committed += c.Count;
    }

    private void Advance(long until)
    {
        Require(until <= Day * 6000 && until - Now <= Day * 120, "Advance in bounded intervals within model horizon.");
        // Chronological scheduling makes sparse ticks equivalent to daily advancement. Deadlines are absolute,
        // not reset on load. The final tick of grace/response is inclusive.
        while (Now < until)
        {
            long boundary = until;
            if (NextTurn <= boundary) boundary = NextTurn;
            foreach (var bill in assessments.Values.Where(b => !b.Paid && !b.OverdueRecorded && contracts[b.Contract].Active))
                if (bill.GraceUntil + 1 > Now) boundary = Math.Min(boundary, bill.GraceUntil + 1);
            foreach (var attack in attacks.Values.Where(a => a.State == AttackState.AwaitingRelief))
                if (attack.Deadline + 1 > Now) boundary = Math.Min(boundary, attack.Deadline + 1);
            foreach (var contract in contracts.Values.Where(c => c.Active))
                if (contract.NextDue > Now) boundary = Math.Min(boundary, contract.NextDue);
            Now = boundary;
            foreach (var bill in assessments.Values.Where(b => !b.Paid && !b.OverdueRecorded &&
                contracts[b.Contract].Active && Now > b.GraceUntil))
            { bill.OverdueRecorded = true; contracts[bill.Contract].Trust = Math.Max(0, contracts[bill.Contract].Trust - .05m); }
            foreach (var attack in attacks.Values.Where(a => a.State == AttackState.AwaitingRelief && Now > a.Deadline)) Resolve(attack);
            foreach (var contract in contracts.Values.Where(c => c.Active && c.NextDue <= Now))
            { contract.NextDue = checked(contract.NextDue + contract.Interval); Schedule(contract); }
            if (Now == NextTurn)
            {
                foreach (var domain in domains.Values)
                    domain.Prosperity = RuntimeCompat.Clamp(domain.Prosperity + (Busy(domain.Id) ? -.01m : .001m), 0, 1);
                NextTurn += TurnTicks;
            }
        }
    }

    private void Resolve(PoliticalAttack a)
    {
        var target = Domain(a.Target); var attacker = Domain(a.Attacker);
        bool defended = target.Available + a.ReliefTroops >= a.Strength;
        a.State = defended ? AttackState.Relieved : AttackState.Captured;
        attacker.Committed -= a.Strength;
        int attackerLoss = defended ? Math.Max(1, a.Strength / 4) : a.Strength / 10;
        attacker.Troops -= attackerLoss;
        // Each deployed cohort takes its own losses; committed soldiers cannot also defend their home.
        foreach (var contribution in a.Relief)
        {
            var helper = Domain(contribution.Key); helper.Committed -= contribution.Value;
            helper.Troops -= contribution.Value / (defended ? 10 : 4);
            if (defended) helper.Honor = Math.Min(1, helper.Honor + .02m);
        }
        int localLoss = target.Available / (defended ? 10 : 4);
        target.Troops -= localLoss; target.Prosperity = Math.Max(0, target.Prosperity - (defended ? .02m : .1m));
        if (a.ProtectionContract != null)
        {
            var contract = contracts[a.ProtectionContract];
            if (contract.Active && !a.Relief.ContainsKey(contract.Liege))
            {
                contract.ProtectionBreached = true; contract.Trust = Math.Max(0, contract.Trust - .15m);
                var liege = Domain(contract.Liege); liege.Honor = Math.Max(0, liege.Honor - .05m);
            }
        }
        // Capture is a model outcome only: no implicit ownership transfer, fabricated signature or player defeat.
        // Conquest adapters must negotiate/validate the new terms before calling Bind.
    }

    private void EndContract(PoliticalContract contract)
    {
        Require(!Subtree(contract.Subject).Any(d => Busy(d.Id)), "Resolve active attacks/deployments before exiting.");
        contract.Active = false;
        var subject = Domain(contract.Subject); subject.Parent = null; subject.InUnion = true;
        // Child contracts, receipts, arrears and native/player faction IDs remain intact.
    }

    public string Export() => JsonSerializer.Serialize(new PoliticalWorldSave(SchemaVersion, TurnTicks, commands.ToArray()));
    public static PoliticalWorld Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 8000000) throw new ArgumentException("Invalid model snapshot size.");
        var save = JsonSerializer.Deserialize<PoliticalWorldSave>(json) ?? throw new ArgumentException("Missing snapshot.");
        Require(save.Version == SchemaVersion && save.Commands != null && save.Commands.Length <= MaxCommands,
            "Unsupported model schema or command capacity.");
        var world = new PoliticalWorld(save.TurnTicks);
        foreach (var command in save.Commands) world.Apply(command);
        return world;
    }
}
