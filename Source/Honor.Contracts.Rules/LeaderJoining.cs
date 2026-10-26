using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum LeaderJoinPhase { Prepared, Applying, Observed, Completed, Cancelled }
public enum LeaderJoinAction { ChangeFaction, Observe, RestoreCare, AddDestination, RemoveSource, Verify }
public sealed record LeaderJoinPlan(string Charter, string Leader, string SourceFaction, string TargetFaction, string SourceCity, string DestinationCity,
    string TargetLeader, string Book, string Transfers, ResidentMoveEvidence Actor, string SourceStock, string DestinationStock,
    IReadOnlyList<string> SourceRoster, IReadOnlyList<string> DestinationRoster, long Tick, bool PersonalConsent);
public sealed record LeaderJoinReturn(string Successor, int? MedicalCare, long Tick);
public sealed record LeaderJoinSave(int Version, LeaderJoinPlan Plan, LeaderJoinPhase Phase, LeaderJoinReturn NativeReturn = null,
    string Successor = null, int? ObservedCare = null, long? ObservedTick = null, bool FromNormalReturn = false, long? FinishedTick = null, long? CareCheckTick = null);

/// <summary>One separately consented leader joins a surviving source's nonplayer destination; no city or whole-faction transfer.</summary>
public sealed class LeaderJoinExecution
{
    public LeaderJoinSave State { get; private set; }
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    private static bool Care(int? n) => !n.HasValue || n is >= 0 and <= 4;
    private static bool Same(IEnumerable<string> a, IEnumerable<string> b) => a.SequenceEqual(b);
    private static void Require(bool value, string reason) { if (!value) throw new ArgumentException(reason); }
    private void Time(long now) => Require(now >= (State.FinishedTick ?? State.CareCheckTick ?? State.ObservedTick ?? State.NativeReturn?.Tick ?? State.Plan.Tick) && now <= int.MaxValue, "Invalid leader join clock.");
    private static void Validate(LeaderJoinPlan p)
    {
        Require(p != null && Key(p.Charter) && Key(p.Leader) && Key(p.SourceFaction) && Key(p.TargetFaction) && Key(p.SourceCity) && Key(p.DestinationCity) && Key(p.TargetLeader) &&
            p.SourceFaction != p.TargetFaction && p.SourceCity != p.DestinationCity && p.TargetLeader != p.Leader && p.PersonalConsent && p.Tick is >= 0 and <= int.MaxValue &&
            p.Actor != null && p.Actor.Id == p.Leader && CityStockProof.Valid(p.Actor.Preserved) && Care(p.Actor.MedicalCare) &&
            CityStockProof.Valid(p.SourceStock) && CityStockProof.Valid(p.DestinationStock) && p.SourceRoster != null && p.DestinationRoster != null &&
            p.SourceRoster.Count is >= 1 and <= ResidentMoveSet.Capacity && p.DestinationRoster.Count < ResidentMoveSet.Capacity &&
            p.SourceRoster.All(Key) && p.DestinationRoster.All(Key) && p.SourceRoster.Count(id => id == p.Leader) == 1 && !p.DestinationRoster.Contains(p.Leader) &&
            p.SourceRoster.Distinct().Count() == p.SourceRoster.Count && p.DestinationRoster.Distinct().Count() == p.DestinationRoster.Count &&
            !p.SourceRoster.Intersect(p.DestinationRoster).Any(), "Invalid leader join plan/rosters/individual consent.");
        var book = CharterBook.Restore(p.Book); var transfers = TerritoryTransferLedger.Restore(p.Transfers); var c = book.Charter(p.Charter);
        Require(p.Tick >= book.LastTick && p.Tick >= transfers.LastTick && c.Phase == RecordedCharterPhase.Active && book.PlayerManaged(c) &&
            !c.PlayerReleaseTick.HasValue && !c.NpcReleaseTick.HasValue && c.Terms.Subject == "territory/" + p.SourceCity &&
            !book.Entities.Any(e => e.Player && (e.NativeOwner == p.SourceFaction || e.NativeOwner == p.TargetFaction)) &&
            transfers.VerifiedOwner(c.Terms.Subject, book.Entity(c.Terms.Subject).NativeOwner, p.SourceFaction) &&
            !transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared), "Exact active town and resolved prior execution required.");
    }
    public static LeaderJoinExecution Prepare(LeaderJoinPlan p)
    {
        Validate(p);
        return new() { State = new(1, p with { SourceRoster = Array.AsReadOnly(p.SourceRoster.ToArray()), DestinationRoster = Array.AsReadOnly(p.DestinationRoster.ToArray()) }, LeaderJoinPhase.Prepared) };
    }
    public LeaderJoinAction Review(ResidentMoveActual actor, string sourceLeader, string targetLeader, string book, string transfers,
        string sourceStock, string destinationStock, IEnumerable<string> sourceRoster, IEnumerable<string> destinationRoster, long now)
    {
        Time(now); var p = State.Plan;
        Require(State.Phase is LeaderJoinPhase.Prepared or LeaderJoinPhase.Applying or LeaderJoinPhase.Observed && actor != null && actor.Id == p.Leader &&
            actor.Preserved == p.Actor.Preserved && actor.MedicalCare.HasValue == p.Actor.MedicalCare.HasValue && Care(actor.MedicalCare) &&
            targetLeader == p.TargetLeader && book == p.Book && transfers == p.Transfers && sourceStock == p.SourceStock && destinationStock == p.DestinationStock,
            "Current leader, preservation, authority or original journals changed.");
        var source = sourceRoster.ToArray(); var destination = destinationRoster.ToArray();
        if (State.Phase == LeaderJoinPhase.Prepared)
        {
            Require(actor.Faction == p.SourceFaction && sourceLeader == p.Leader && actor.MedicalCare == p.Actor.MedicalCare &&
                Same(source, p.SourceRoster) && Same(destination, p.DestinationRoster), "Only wholly unchanged preparation may start.");
            return LeaderJoinAction.ChangeFaction;
        }
        if (State.Phase == LeaderJoinPhase.Applying)
        {
            Require(actor.Faction == p.TargetFaction && Key(sourceLeader) && sourceLeader != p.Leader && Same(source, p.SourceRoster) && Same(destination, p.DestinationRoster) &&
                (State.NativeReturn == null ? actor.MedicalCare == p.Actor.MedicalCare : sourceLeader == State.NativeReturn.Successor && actor.MedicalCare == State.NativeReturn.MedicalCare),
                "Never replay an uncertain native call or infer/repair an unobserved successor/care reset.");
            return LeaderJoinAction.Observe;
        }
        Require(actor.Faction == p.TargetFaction && sourceLeader == State.Successor &&
            (actor.MedicalCare == p.Actor.MedicalCare || !State.CareCheckTick.HasValue && actor.MedicalCare == State.ObservedCare), "Observed destination, successor or care changed.");
        bool original = Same(source, p.SourceRoster), removed = Same(source, p.SourceRoster.Where(id => id != p.Leader));
        bool destOriginal = Same(destination, p.DestinationRoster), added = Same(destination, p.DestinationRoster.Append(p.Leader));
        Require(original && destOriginal || original && added || removed && added, "Unreviewed/split residency change.");
        if (actor.MedicalCare != p.Actor.MedicalCare) return LeaderJoinAction.RestoreCare;
        return destOriginal ? LeaderJoinAction.AddDestination : original ? LeaderJoinAction.RemoveSource : LeaderJoinAction.Verify;
    }
    public void Begin(long now)
    { Time(now); Require(State.Phase == LeaderJoinPhase.Prepared, "Prepare before native joining."); State = State with { Phase = LeaderJoinPhase.Applying }; }
    public void RecordReturn(string successor, int? care, long now)
    {
        Time(now); Require(State.Phase == LeaderJoinPhase.Applying && State.NativeReturn == null && (successor == null || Key(successor)) && Care(care), "Native return can be recorded once.");
        // A null/invalid successor is retained as evidence of an incomplete outcome, never silently filled or retried.
        State = State with { NativeReturn = new(successor, care, now) };
    }
    public void Observe(LeaderJoinAction action, string successor, int? care, long now)
    {
        Time(now); Require(State.Phase == LeaderJoinPhase.Applying && action == LeaderJoinAction.Observe && Key(successor) && successor != State.Plan.Leader && Care(care) &&
            care.HasValue == State.Plan.Actor.MedicalCare.HasValue && (State.NativeReturn == null ? care == State.Plan.Actor.MedicalCare :
                successor == State.NativeReturn.Successor && care == State.NativeReturn.MedicalCare), "Positive current successor and destination observation required.");
        State = State with { Phase = LeaderJoinPhase.Observed, Successor = successor, ObservedCare = care, ObservedTick = now, FromNormalReturn = State.NativeReturn != null };
    }
    public void Finish(LeaderJoinAction action, long now)
    { Time(now); Require(State.Phase == LeaderJoinPhase.Observed && State.CareCheckTick.HasValue && action == LeaderJoinAction.Verify, "Verify exact destination residency and care first."); State = State with { Phase = LeaderJoinPhase.Completed, FinishedTick = now }; }
    public void SealCare(LeaderJoinAction action, long now)
    {
        Time(now); Require(State.Phase == LeaderJoinPhase.Observed && State.CareCheckTick == null &&
            action is LeaderJoinAction.RestoreCare or LeaderJoinAction.AddDestination or LeaderJoinAction.RemoveSource or LeaderJoinAction.Verify, "Consume recorded-care permission once.");
        State = State with { CareCheckTick = now };
    }
    public void Cancel(LeaderJoinAction action, long now)
    { Time(now); Require(State.Phase == LeaderJoinPhase.Prepared && action == LeaderJoinAction.ChangeFaction, "Only wholly unchanged preparation may cancel."); State = State with { Phase = LeaderJoinPhase.Cancelled, FinishedTick = now }; }
    public string Export() => JsonSerializer.Serialize(State);
    public static LeaderJoinExecution Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 8000000, "Invalid leader join snapshot.");
        var s = JsonSerializer.Deserialize<LeaderJoinSave>(json); Require(s != null && s.Version == 1 && RuntimeCompat.IsDefined(s.Phase), "Invalid leader join phase/schema."); Validate(s.Plan);
        bool observed = s.Phase is LeaderJoinPhase.Observed or LeaderJoinPhase.Completed, finished = s.Phase is LeaderJoinPhase.Completed or LeaderJoinPhase.Cancelled;
        Require(observed == s.ObservedTick.HasValue && observed == (s.Successor != null) && finished == s.FinishedTick.HasValue &&
            (!observed ? s.ObservedCare == null && !s.FromNormalReturn : Key(s.Successor) && s.Successor != s.Plan.Leader && Care(s.ObservedCare) &&
                s.ObservedCare.HasValue == s.Plan.Actor.MedicalCare.HasValue && s.ObservedTick >= (s.NativeReturn?.Tick ?? s.Plan.Tick) && s.ObservedTick <= int.MaxValue &&
                s.FromNormalReturn == (s.NativeReturn != null) && (s.NativeReturn == null ? s.ObservedCare == s.Plan.Actor.MedicalCare : s.Successor == s.NativeReturn.Successor && s.ObservedCare == s.NativeReturn.MedicalCare)) &&
            (s.NativeReturn == null || s.Phase is not (LeaderJoinPhase.Prepared or LeaderJoinPhase.Cancelled) &&
                (s.NativeReturn.Successor == null || Key(s.NativeReturn.Successor)) && Care(s.NativeReturn.MedicalCare) && s.NativeReturn.Tick >= s.Plan.Tick && s.NativeReturn.Tick <= int.MaxValue) &&
            (s.CareCheckTick == null || observed && s.CareCheckTick >= s.ObservedTick && s.CareCheckTick <= int.MaxValue) &&
            (s.Phase != LeaderJoinPhase.Completed || s.CareCheckTick.HasValue) &&
            (!finished || s.FinishedTick >= (s.CareCheckTick ?? s.ObservedTick ?? s.Plan.Tick) && s.FinishedTick <= int.MaxValue), "Invalid saved leader outcome evidence/clock.");
        var p = s.Plan; return new() { State = s with { Plan = p with { SourceRoster = Array.AsReadOnly(p.SourceRoster.ToArray()), DestinationRoster = Array.AsReadOnly(p.DestinationRoster.ToArray()) } } };
    }
}
