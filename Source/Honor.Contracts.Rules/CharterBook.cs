using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum RecordedCharterPhase { Offered, Active, Declined, Released, Expired }
public enum CharterAvailability { Current, MissingTerritory, OwnershipChanged, Ineligible }
public sealed record TerritoryIdentity(string Id, string NativeOwner, bool Player);
public sealed record TerritoryObservation(string Id, string NativeOwner, bool Exists, bool Player, bool Eligible);
public sealed record RecordedCharterTerms(string Liege, string Subject, decimal TributeValue,
    long IntervalTicks, long GraceTicks, TributeTransport Transport, decimal TransportRate);
public sealed record NpcCharterAuthority(string LiegeRepresentative, string SubjectRepresentative);
public sealed record RecordedCharter(string Id, RecordedCharterTerms Terms, long OfferedTick, long ExpiresTick,
    RecordedCharterPhase Phase = RecordedCharterPhase.Offered, long? PlayerSignedTick = null,
    long? NpcSignedTick = null, long? PlayerReleaseTick = null, long? NpcReleaseTick = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NpcCharterAuthority NpcAuthority = null);
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
    public RecordedCharter OfferNpc(string id, RecordedCharterTerms terms, NpcCharterAuthority authority,
        long now, long responseTicks, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(authority);
        return OfferCore(id, terms, now, responseTicks, observations, authority);
    }
    private RecordedCharter OfferCore(string id, RecordedCharterTerms terms, long now, long responseTicks,
        IEnumerable<TerritoryObservation> observations, NpcCharterAuthority authority)
    {
        Key(id); Time(now); Terms(terms, authority); Fresh(terms, observations); Topology(terms);
        Require(!charters.ContainsKey(id) && charters.Count < MaxCharters && responseTicks >= PoliticalWorld.Day &&
            responseTicks <= PoliticalWorld.Day * 30 && now <= int.MaxValue - responseTicks, "Invalid offer capacity, ID or response period.");
        Require(!charters.Values.Any(c => Phase(c, now) == RecordedCharterPhase.Offered && c.Terms.Subject == terms.Subject), "One pending proposal per subject.");
        var offer = new RecordedCharter(id, terms, now, now + responseTicks, NpcAuthority: authority);
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
        Fresh(offer.Terms, observations); Topology(offer.Terms);
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
            Require(both == (charter.Phase is RecordedCharterPhase.Active or RecordedCharterPhase.Released), "Invalid saved bilateral consent.");
            foreach (var release in new[] { charter.PlayerReleaseTick, charter.NpcReleaseTick })
                Require(!release.HasValue || both && release >= Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value) && release <= save.LastTick,
                    "Invalid saved release time.");
            Require((charter.PlayerReleaseTick.HasValue && charter.NpcReleaseTick.HasValue) == (charter.Phase == RecordedCharterPhase.Released), "Invalid saved mutual release.");
            book.charters.Add(charter.Id, charter);
        }
        // Validate the final graph independent of save order, including disconnected chains.
        foreach (var charter in book.Active()) book.Topology(charter.Terms, charter.Id);
        Require(book.charters.Values.Where(c => book.Phase(c, save.LastTick) == RecordedCharterPhase.Offered)
            .GroupBy(c => c.Terms.Subject, StringComparer.Ordinal).All(g => g.Count() == 1), "Duplicate saved pending subject.");
        book.LastTick = save.LastTick; return book;
    }
}
