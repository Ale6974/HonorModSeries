using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum RecordedCharterPhase { Offered, Active, Declined, Released, Expired, TerritoryLost, Replaced }
public sealed record LostTownReplacement(string LostRoot, string Next, long Tick,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[] SkippedRoots = null);
public enum CharterAvailability { Current, MissingTerritory, OwnershipChanged, Ineligible }
public sealed record TerritoryIdentity(string Id, string NativeOwner, bool Player);
public sealed record TerritoryObservation(string Id, string NativeOwner, bool Exists, bool Player, bool Eligible);
public sealed record RecordedCharterTerms(string Liege, string Subject, decimal TributeValue,
    long IntervalTicks, long GraceTicks, TributeTransport Transport, decimal TransportRate);
public sealed record NpcCharterAuthority(string LiegeRepresentative, string SubjectRepresentative);
public sealed record RecordedPoliticalLoss(string Id, string Proof);
public sealed record CharterAmendmentLink(string Previous, string Proof);
public sealed record RecordedCharter(string Id, RecordedCharterTerms Terms, long OfferedTick, long ExpiresTick,
    RecordedCharterPhase Phase = RecordedCharterPhase.Offered, long? PlayerSignedTick = null,
    long? NpcSignedTick = null, long? PlayerReleaseTick = null, long? NpcReleaseTick = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NpcCharterAuthority NpcAuthority = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? TerritoryLostTick = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool ConditionalOffer = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LostTownReplacement Replacement = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string Replaces = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] RecordedPoliticalLoss PoliticalLoss = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public CharterAmendmentLink Amendment { get; init; }
}
public sealed record CharterBookSave(int Version, long LastTick, TerritoryIdentity[] Entities, RecordedCharter[] Charters);

/// <summary>
/// Persistent bilateral charter records for real-world bindings. This book does not change ownership,
/// move goods or confer NPC authority. The engine adapter must supply fresh observations at every consent.
/// Native player identity is separate from political identity; one entity covers every player settlement.
/// </summary>
public sealed class CharterBook
{
    private readonly Dictionary<string, TerritoryIdentity> entities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RecordedCharter> charters = new(StringComparer.Ordinal);
    public const int Version = 1;
    public const int MaxEntities = 1000;
    public const int MaxCharters = 2000;
    public long LastTick { get; private set; }
    public IReadOnlyCollection<TerritoryIdentity> Entities => entities.Values.ToArray();
    public IReadOnlyCollection<RecordedCharter> Charters => charters.Values.ToArray();
    private static void Require(bool valid, string message) { if (!valid) throw new ArgumentException(message); }
    private static void Key(string value) => Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 120 && !value.Any(char.IsControl), "Invalid stable identity.");
    private void Time(long now) => Require(now >= LastTick && now <= int.MaxValue, "Invalid or reversed game time.");
    public TerritoryIdentity Entity(string id) => entities.TryGetValue(id ?? "", out var value) ? value : throw new ArgumentException("Unknown territory.");
    public RecordedCharter Charter(string id) => charters.TryGetValue(id ?? "", out var value) ? value : throw new ArgumentException("Unknown charter.");
    public RecordedCharterPhase Phase(RecordedCharter charter, long now) => charter.Phase == RecordedCharterPhase.Offered && now > charter.ExpiresTick
        ? RecordedCharterPhase.Expired : charter.Phase;
    public void Register(TerritoryIdentity entity, long now)
    {
        RuntimeCompat.NotNull(entity); Key(entity.Id); Key(entity.NativeOwner); Time(now);
        if (entities.TryGetValue(entity.Id, out var previous))
        { Require(previous == entity, "An ownership change cannot silently reassign a recorded political identity."); return; }
        Require(entities.Count < MaxEntities && (!entity.Player || entities.Values.All(e => !e.Player)), "Capacity or duplicate player political identity.");
        entities.Add(entity.Id, entity); LastTick = now;
    }
    public CharterAvailability Availability(string id, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(observations);
        var entity = Entity(id);
        var matches = observations.Where(o => o != null && o.Id == id).ToArray();
        if (matches.Length != 1 || !matches[0].Exists) return CharterAvailability.MissingTerritory;
        var current = matches[0];
        if (current.NativeOwner != entity.NativeOwner || current.Player != entity.Player) return CharterAvailability.OwnershipChanged;
        return current.Eligible ? CharterAvailability.Current : CharterAvailability.Ineligible;
    }
    private RecordedCharter[] Active() => charters.Values.Where(c => c.Phase == RecordedCharterPhase.Active).ToArray();
    public string Root(string entityId)
    {
        Entity(entityId); var cursor = entityId; var active = Active();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            Require(visited.Add(cursor), "Cyclic political structure.");
            var parent = active.SingleOrDefault(c => c.Terms.Subject == cursor);
            if (parent == null) return cursor;
            cursor = parent.Terms.Liege;
        }
    }
    public string RecordedPoliticalOwner(string entityId) => Entity(Root(entityId)).NativeOwner;
    public bool IsBelow(string node, string ancestor)
    {
        Entity(node); Entity(ancestor);
        if (node == ancestor) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (visited.Add(node))
        {
            var parent = Active().SingleOrDefault(c => c.Terms.Subject == node);
            if (parent == null) return false;
            node = parent.Terms.Liege;
            if (node == ancestor) return true;
        }
        throw new ArgumentException("Cyclic charter hierarchy.");
    }
    public bool PlayerManaged(RecordedCharter charter)
    {
        var player = entities.Values.SingleOrDefault(e => e.Player);
        return player != null && !Entity(charter.Terms.Subject).Player &&
            (charter.Terms.Liege == player.Id || IsBelow(charter.Terms.Liege, player.Id));
    }
    public RecordedCharter[] PlayerFollowers() => Active().Where(PlayerManaged).ToArray();
    private void Terms(RecordedCharterTerms terms, NpcCharterAuthority authority = null)
    {
        RuntimeCompat.NotNull(terms);
        Require(Entity(terms.Liege).Id != Entity(terms.Subject).Id, "Self-contract.");
        if (authority == null)
            Require(Entity(terms.Liege).Player != Entity(terms.Subject).Player, "Player/NPC consent cannot create an NPC-only charter.");
        else
        {
            Require(!Entity(terms.Liege).Player && !Entity(terms.Subject).Player, "NPC consent requires two NPC parties.");
            Key(authority.LiegeRepresentative); Key(authority.SubjectRepresentative);
        }
        Require(terms.TributeValue is > 0 and <= 10000000 && terms.IntervalTicks is >= PoliticalWorld.Day and <= PoliticalWorld.Day * 60 &&
            terms.GraceTicks is >= 0 and <= PoliticalWorld.Day * 30 && RuntimeCompat.IsDefined(terms.Transport) && terms.TransportRate is >= 0 and <= .5m,
            "Invalid fixed-value tribute terms.");
    }
    private void Topology(RecordedCharterTerms candidate, string omit = null)
    {
        var links = Active().Where(c => c.Id != omit).Select(c => c.Terms).Append(candidate).ToArray();
        Require(links.GroupBy(t => t.Subject, StringComparer.Ordinal).All(g => g.Count() == 1), "Subject already bound.");
        foreach (var node in entities.Keys)
        {
            string cursor = node; int depth = 1; var visited = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Require(visited.Add(cursor) && depth <= 4, "Cyclic or more than four levels, including descendants.");
                var link = links.SingleOrDefault(t => t.Subject == cursor);
                if (link == null) break;
                cursor = link.Liege; depth++;
            }
        }
    }
    private void Fresh(RecordedCharterTerms terms, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(observations);
        var current = observations.ToArray();
        // Validate the entire connected component, not just the two endpoints: an absent intermediate
        // lord must not make a stale contract look valid or allow two effective masters.
        var ids = new HashSet<string>(StringComparer.Ordinal) { terms.Liege, terms.Subject };
        bool changed;
        do
        {
            changed = false;
            foreach (var link in Active())
                if (ids.Contains(link.Terms.Liege) || ids.Contains(link.Terms.Subject))
                { changed |= ids.Add(link.Terms.Liege); changed |= ids.Add(link.Terms.Subject); }
        } while (changed);
        Require(ids.All(id => Availability(id, current) == CharterAvailability.Current), "A connected territory is missing, changed owner or is ineligible.");
    }
    public void ValidateCurrent(string charterId, IEnumerable<TerritoryObservation> observations) => Fresh(Charter(charterId).Terms, observations);
    public RecordedCharter Offer(string id, RecordedCharterTerms terms, long now, long responseTicks, IEnumerable<TerritoryObservation> observations)
        => OfferCore(id, terms, now, responseTicks, observations, null);
    /// <summary>A nonbinding candidate for the player. Existing allegiance must end lawfully before final player signing.</summary>
    public RecordedCharter OfferAlternative(string id, RecordedCharterTerms terms, long now, long responseTicks, IEnumerable<TerritoryObservation> observations)
        => OfferCore(id, terms, now, responseTicks, observations, null, true);
    private void ProposalTopology(RecordedCharterTerms terms)
    {
        Require(Entity(terms.Subject).Player && !Entity(terms.Liege).Player, "Conditional candidates are player-to-NPC liege proposals only.");
        var existing = Active().SingleOrDefault(c => c.Terms.Subject == terms.Subject);
        Topology(terms, existing?.Id); // Hypothetical review only: never remove this existing contract.
    }
    public RecordedCharter OfferNpc(string id, RecordedCharterTerms terms, NpcCharterAuthority authority,
        long now, long responseTicks, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(authority);
        return OfferCore(id, terms, now, responseTicks, observations, authority);
    }
    private RecordedCharter OfferCore(string id, RecordedCharterTerms terms, long now, long responseTicks,
        IEnumerable<TerritoryObservation> observations, NpcCharterAuthority authority, bool conditional = false)
    {
        Key(id); Time(now); Terms(terms, authority); Fresh(terms, observations);
        if (conditional) { Require(authority == null, "No NPC-only conditional signatures."); ProposalTopology(terms); }
        else Topology(terms);
        Require(!charters.ContainsKey(id) && charters.Count < MaxCharters && responseTicks >= PoliticalWorld.Day &&
            responseTicks <= PoliticalWorld.Day * 30 && now <= int.MaxValue - responseTicks, "Invalid offer capacity, ID or response period.");
        var pending = charters.Values.Where(c => Phase(c, now) == RecordedCharterPhase.Offered && c.Terms.Subject == terms.Subject).ToArray();
        Require(conditional ? pending.Length < 8 && pending.All(c => c.Terms.Liege != terms.Liege) : pending.Length == 0,
            "Pending proposals exceed the scope/counterparty limit.");
        var offer = new RecordedCharter(id, terms, now, now + responseTicks, NpcAuthority: authority, ConditionalOffer: conditional);
        charters.Add(id, offer); LastTick = now; return offer;
    }
    public void Sign(string id, bool player, long now, IEnumerable<TerritoryObservation> observations)
    {
        Require(Charter(id).NpcAuthority == null, "Player consent slots cannot sign NPC-only contracts.");
        SignCore(id, player, now, observations);
    }
    public void SignNpc(string id, string party, string representative, long now, IEnumerable<TerritoryObservation> observations)
    {
        var c = Charter(id); RuntimeCompat.NotNull(c.NpcAuthority);
        Require(party == c.Terms.Liege || party == c.Terms.Subject, "Unknown NPC party.");
        bool liege = party == c.Terms.Liege;
        Require(representative == (liege ? c.NpcAuthority.LiegeRepresentative : c.NpcAuthority.SubjectRepresentative), "NPC representative differs from the reviewed authority.");
        SignCore(id, liege, now, observations);
    }
    // Historic JSON slot names are retained: on NPC-only records the first slot is liege, second is subject.
    private void SignCore(string id, bool player, long now, IEnumerable<TerritoryObservation> observations)
    {
        Time(now); var offer = Charter(id);
        Require(Phase(offer, now) == RecordedCharterPhase.Offered, "Offer is not open.");
        Require(player ? !offer.PlayerSignedTick.HasValue : !offer.NpcSignedTick.HasValue, "Duplicate consent.");
        Fresh(offer.Terms, observations);
        if (offer.ConditionalOffer && !player) ProposalTopology(offer.Terms);
        else
        {
            if (offer.ConditionalOffer) Require(offer.NpcSignedTick.HasValue, "The actual NPC must respond before final player consent.");
            Topology(offer.Terms); // Final consent never waives an existing liege or a cycle/depth constraint.
        }
        var next = player ? offer with { PlayerSignedTick = now } : offer with { NpcSignedTick = now };
        if (next.PlayerSignedTick.HasValue && next.NpcSignedTick.HasValue) next = next with { Phase = RecordedCharterPhase.Active };
        charters[id] = next; LastTick = now;
    }
    public void Decline(string id, long now)
    {
        Time(now); var offer = Charter(id);
        Require(Phase(offer, now) == RecordedCharterPhase.Offered, "Only a pending offer can be declined.");
        charters[id] = offer with { Phase = RecordedCharterPhase.Declined }; LastTick = now;
    }
    public void ConsentToRelease(string id, bool player, long now, IEnumerable<TerritoryObservation> observations)
    {
        Require(Charter(id).NpcAuthority == null, "Player release slots cannot release NPC-only contracts.");
        ReleaseCore(id, player, now, observations);
    }
    internal void BindAmendment(string id, string previous, string proof)
    {
        var c = Charter(id); var old = Charter(previous);
        Require(c.Phase == RecordedCharterPhase.Offered && c.Amendment == null && c.NpcAuthority == null && old.NpcAuthority == null &&
            old.Phase == RecordedCharterPhase.Released && c.Terms.Liege == old.Terms.Liege && c.Terms.Subject == old.Terms.Subject &&
            Entity(c.Terms.Liege).Player != Entity(c.Terms.Subject).Player && CityStockProof.Valid(proof), "Invalid same-party amendment link.");
        charters[id] = c with { Amendment = new(previous, proof) };
    }
    public void ConsentNpcRelease(string id, string party, long now, IEnumerable<TerritoryObservation> observations)
    {
        var c = Charter(id);
        Require(c.NpcAuthority != null && (party == c.Terms.Liege || party == c.Terms.Subject), "Unknown NPC release party.");
        // The live adapter must validate CURRENT representatives. Old signatories may have died or been succeeded.
        ReleaseCore(id, party == c.Terms.Liege, now, observations);
    }
    private void ReleaseCore(string id, bool player, long now, IEnumerable<TerritoryObservation> observations)
    {
        Time(now); var charter = Charter(id);
        Require(charter.Phase == RecordedCharterPhase.Active && (player ? !charter.PlayerReleaseTick.HasValue : !charter.NpcReleaseTick.HasValue), "No active unsigned release.");
        Fresh(charter.Terms, observations);
        var next = player ? charter with { PlayerReleaseTick = now } : charter with { NpcReleaseTick = now };
        if (next.PlayerReleaseTick.HasValue && next.NpcReleaseTick.HasValue) next = next with { Phase = RecordedCharterPhase.Released };
        charters[id] = next; LastTick = now;
    }
    public void CloseDestroyedLeaf(string id, string territory, string baselineOwner, long lossTick, long now, bool verifiedDestroyed)
        => CloseDestroyedTown(id, territory, baselineOwner, lossTick, now, verifiedDestroyed, false);
    internal void ClosePoliticalLostLeaf(string id, string territory, string baselineOwner, long lossTick, long now, RecordedPoliticalLoss proof)
    {
        RuntimeCompat.NotNull(proof); Key(proof.Id); Require(CityStockProof.Valid(proof.Proof), "Exact independent loss proof required.");
        CloseDestroyedLeaf(id, territory, baselineOwner, lossTick, now, true);
        charters[id] = Charter(id) with { PoliticalLoss = proof };
    }
    public void CloseDestroyedTown(string id, string territory, string baselineOwner, long lossTick, long now, bool verifiedDestroyed, bool preserveChildren)
        => CloseLostTown(id, territory, baselineOwner, lossTick, now, verifiedDestroyed, preserveChildren);
    public void CloseHostileTransferredTown(string id, string territory, string baselineOwner, string previousOwner,
        string currentOwner, long lossTick, long now, bool verifiedNativeChange, bool preserveChildren)
    {
        Key(previousOwner); Key(currentOwner);
        Require(previousOwner != currentOwner, "Hostile loss requires distinct actual owners.");
        CloseLostTown(id, territory, baselineOwner, lossTick, now, verifiedNativeChange, preserveChildren);
    }
    internal void CloseRecordedDescendantLoss(string id, string territory, string baselineOwner, long lossTick, long now, CharterBook original)
    {
        Require(original.PlayerManaged(original.Charter(id)) && original.Charter(id) == Charter(id) &&
            original.Entity(territory) == Entity(territory), "Exact originally managed descendant required.");
        CloseLostTown(id, territory, baselineOwner, lossTick, now, true, true, true);
    }
    internal void RecordLossResolution(long now) { Time(now); LastTick = now; }
    private void CloseLostTown(string id, string territory, string baselineOwner, long lossTick, long now, bool verifiedLoss, bool preserveChildren, bool recordedScope = false)
    {
        Time(now); var c = Charter(id); var entity = Entity(c.Terms.Subject);
        Require(verifiedLoss && !entity.Player && entity.Id == territory && entity.NativeOwner == baselineOwner &&
            c.Phase == RecordedCharterPhase.Active && c.PlayerSignedTick.HasValue && c.NpcSignedTick.HasValue &&
            lossTick >= Math.Max(c.PlayerSignedTick.Value, c.NpcSignedTick.Value) && lossTick <= now &&
            (!c.PlayerReleaseTick.HasValue || c.PlayerReleaseTick <= lossTick) && (!c.NpcReleaseTick.HasValue || c.NpcReleaseTick <= lossTick),
            "A verified native NPC town loss with an active incoming charter is required.");
        Require((!Active().Any(other => other.Terms.Liege == territory) || preserveChildren && (PlayerManaged(c) || recordedScope) &&
                Active().Where(other => IsBelow(other.Terms.Subject, territory)).All(other => !Entity(other.Terms.Subject).Player)) &&
            !charters.Values.Any(other => Phase(other, now) == RecordedCharterPhase.Offered && (other.Terms.Liege == territory || other.Terms.Subject == territory)),
            "Descendants or open city proposals need a coordinated loss/succession adapter.");
        charters[id] = c with { Phase = RecordedCharterPhase.TerritoryLost, TerritoryLostTick = lossTick }; LastTick = now;
    }
    internal void ReplaceLostChildren(string lostRoot, IReadOnlyDictionary<string, string> replacements, string parent,
        NpcCharterAuthority authority, long now, IEnumerable<TerritoryObservation> observations, string[] skippedRoots = null)
    {
        Time(now); var root = Charter(lostRoot); RuntimeCompat.NotNull(authority);
        string expectedParent = ReplacementParent(lostRoot, skippedRoots, now);
        Require(root.Phase == RecordedCharterPhase.TerritoryLost && parent == expectedParent &&
            (Entity(parent).Player || Active().Any(c => c.Terms.Subject == parent) && IsBelow(parent, entities.Values.Single(e => e.Player).Id)), "No surviving higher authority.");
        var children = Active().Where(c => c.Terms.Liege == root.Terms.Subject).ToArray();
        Require(children.Length > 0 && children.Length == replacements.Count && children.All(c => replacements.ContainsKey(c.Id) &&
            !c.PlayerReleaseTick.HasValue && !c.NpcReleaseTick.HasValue && !Entity(c.Terms.Subject).Player), "Exact unreleased child contracts required.");
        foreach (var c in children)
            charters[c.Id] = c with { Phase = RecordedCharterPhase.Replaced, Replacement = new(lostRoot, replacements[c.Id], now, skippedRoots) };
        foreach (var c in children.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            string next = replacements[c.Id];
            if (Entity(parent).Player)
            {
                Offer(next, c.Terms with { Liege = parent }, now, PoliticalWorld.Day * 3, observations);
                Sign(next, false, now, observations); Sign(next, true, now, observations);
            }
            else
            {
                OfferNpc(next, c.Terms with { Liege = parent }, authority, now, PoliticalWorld.Day * 3, observations);
                SignNpc(next, parent, authority.LiegeRepresentative, now, observations);
                SignNpc(next, c.Terms.Subject, authority.SubjectRepresentative, now, observations);
            }
            charters[next] = Charter(next) with { Replaces = c.Id };
        }
    }
    internal string ReplacementParent(string lostRoot, string[] skippedRoots, long now)
    {
        var root = Charter(lostRoot); Require(root.Phase == RecordedCharterPhase.TerritoryLost && root.TerritoryLostTick <= now, "Recorded loss required.");
        string parent = root.Terms.Liege;
        Require(skippedRoots == null || skippedRoots.Length is >= 1 and <= 3 && skippedRoots.Distinct(StringComparer.Ordinal).Count() == skippedRoots.Length &&
            !skippedRoots.Contains(lostRoot), "Invalid skipped ancestor chain.");
        foreach (string id in skippedRoots ?? [])
        {
            var ancestor = Charter(id);
            Require(ancestor.Phase == RecordedCharterPhase.TerritoryLost && ancestor.Terms.Subject == parent && ancestor.TerritoryLostTick <= now,
                "Every skipped ancestor must be a correctly ordered recorded loss.");
            parent = ancestor.Terms.Liege;
        }
        return parent;
    }
    public string Export() => JsonSerializer.Serialize(new CharterBookSave(Version, LastTick, entities.Values.ToArray(), charters.Values.ToArray()));
    public static CharterBook Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 8000000, "Invalid book size.");
        var save = JsonSerializer.Deserialize<CharterBookSave>(json);
        Require(save != null && save.Version == Version && save.LastTick is >= 0 and <= int.MaxValue &&
            save.Entities != null && save.Entities.Length <= MaxEntities && save.Charters != null && save.Charters.Length <= MaxCharters,
            "Unsupported or invalid charter book.");
        var book = new CharterBook();
        Require(save.Entities.All(e => e != null) && save.Entities.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() == save.Entities.Length,
            "Duplicate or missing saved territory identity.");
        foreach (var entity in save.Entities) book.Register(entity, 0);
        foreach (var charter in save.Charters)
        {
            RuntimeCompat.NotNull(charter); Key(charter.Id); book.Terms(charter.Terms, charter.NpcAuthority);
            Require(!book.charters.ContainsKey(charter.Id) && RuntimeCompat.IsDefined(charter.Phase) && charter.Phase != RecordedCharterPhase.Expired &&
                charter.OfferedTick >= 0 && charter.OfferedTick <= save.LastTick && charter.ExpiresTick > charter.OfferedTick &&
                charter.ExpiresTick - charter.OfferedTick is >= PoliticalWorld.Day and <= PoliticalWorld.Day * 30 && charter.ExpiresTick <= int.MaxValue,
                "Invalid saved charter.");
            foreach (var signature in new[] { charter.PlayerSignedTick, charter.NpcSignedTick })
                Require(!signature.HasValue || signature >= charter.OfferedTick && signature <= charter.ExpiresTick && signature <= save.LastTick, "Invalid saved consent time.");
            bool both = charter.PlayerSignedTick.HasValue && charter.NpcSignedTick.HasValue;
            if (charter.ConditionalOffer)
                Require(charter.NpcAuthority == null && book.Entity(charter.Terms.Subject).Player && !book.Entity(charter.Terms.Liege).Player &&
                    (!charter.PlayerSignedTick.HasValue || charter.NpcSignedTick.HasValue && charter.PlayerSignedTick >= charter.NpcSignedTick),
                    "Invalid saved conditional proposal scope or consent order.");
            Require(both == (charter.Phase is RecordedCharterPhase.Active or RecordedCharterPhase.Released or RecordedCharterPhase.TerritoryLost or RecordedCharterPhase.Replaced), "Invalid saved bilateral consent.");
            foreach (var release in new[] { charter.PlayerReleaseTick, charter.NpcReleaseTick })
                Require(!release.HasValue || both && release >= Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value) && release <= save.LastTick,
                    "Invalid saved release time.");
            Require((charter.PlayerReleaseTick.HasValue && charter.NpcReleaseTick.HasValue) == (charter.Phase == RecordedCharterPhase.Released), "Invalid saved mutual release.");
            Require((charter.Phase == RecordedCharterPhase.TerritoryLost) == charter.TerritoryLostTick.HasValue &&
                (!charter.TerritoryLostTick.HasValue || !book.Entity(charter.Terms.Subject).Player && charter.TerritoryLostTick >= Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value) &&
                    charter.TerritoryLostTick <= save.LastTick && (!charter.PlayerReleaseTick.HasValue || charter.PlayerReleaseTick <= charter.TerritoryLostTick) &&
                    (!charter.NpcReleaseTick.HasValue || charter.NpcReleaseTick <= charter.TerritoryLostTick)), "Invalid saved territorial loss.");
            if (charter.PoliticalLoss != null)
            {
                Key(charter.PoliticalLoss.Id);
                Require(charter.Phase == RecordedCharterPhase.TerritoryLost && CityStockProof.Valid(charter.PoliticalLoss.Proof), "Invalid independent political loss binding.");
            }
            book.charters.Add(charter.Id, charter);
        }
        foreach (var c in book.charters.Values)
        {
            if (c.Amendment != null)
            {
                Key(c.Amendment.Previous); var old = book.Charter(c.Amendment.Previous);
                Require(old.Id != c.Id && CityStockProof.Valid(c.Amendment.Proof) && c.NpcAuthority == null && old.NpcAuthority == null &&
                    book.Entity(c.Terms.Liege).Player != book.Entity(c.Terms.Subject).Player && old.Phase == RecordedCharterPhase.Released &&
                    c.Terms.Liege == old.Terms.Liege && c.Terms.Subject == old.Terms.Subject &&
                    Math.Max(old.PlayerReleaseTick.Value, old.NpcReleaseTick.Value) <= c.OfferedTick, "Invalid saved negotiated amendment.");
                var seen = new HashSet<string>(StringComparer.Ordinal) { c.Id }; var previous = old;
                while (true)
                {
                    Require(seen.Add(previous.Id), "Cyclic negotiated amendment history.");
                    if (previous.Amendment == null) break;
                    previous = book.Charter(previous.Amendment.Previous);
                }
            }
            Require((c.Phase == RecordedCharterPhase.Replaced) == (c.Replacement != null), "Invalid replacement phase.");
            if (c.Replacement != null)
            {
                var r = c.Replacement; Key(r.LostRoot); Key(r.Next);
                var root = book.Charter(r.LostRoot); var next = book.Charter(r.Next);
                Require(!book.Entity(c.Terms.Subject).Player && c.NpcAuthority != null && root.Phase == RecordedCharterPhase.TerritoryLost &&
                    c.Terms.Liege == root.Terms.Subject && r.Tick >= root.TerritoryLostTick && r.Tick >= Math.Max(c.PlayerSignedTick.Value, c.NpcSignedTick.Value) && r.Tick <= save.LastTick &&
                    !c.PlayerReleaseTick.HasValue && !c.NpcReleaseTick.HasValue && next.Replaces == c.Id &&
                    next.Terms == c.Terms with { Liege = book.ReplacementParent(r.LostRoot, r.SkippedRoots, r.Tick) } && next.OfferedTick == r.Tick &&
                    next.PlayerSignedTick == r.Tick && next.NpcSignedTick == r.Tick, "Invalid lost-town replacement link or terms.");
            }
            if (c.Replaces != null)
            { Key(c.Replaces); Require(book.Charter(c.Replaces).Replacement?.Next == c.Id, "Unpaired successor link."); }
        }
        // Validate the final graph independent of save order, including disconnected chains.
        foreach (var charter in book.Active()) book.Topology(charter.Terms, charter.Id);
        Require(book.charters.Values.Where(c => book.Phase(c, save.LastTick) == RecordedCharterPhase.Offered)
            .GroupBy(c => c.Terms.Subject, StringComparer.Ordinal).All(g => g.Count() <= 8 && g.Count(c => !c.ConditionalOffer) <= 1 &&
                g.GroupBy(c => c.Terms.Liege, StringComparer.Ordinal).All(pair => pair.Count() == 1)), "Duplicate or excessive saved pending proposals.");
        book.LastTick = save.LastTick; return book;
    }
}
