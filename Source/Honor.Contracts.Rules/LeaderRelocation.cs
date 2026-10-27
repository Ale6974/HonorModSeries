using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum LeaderRelocationAction { AddDestination, RemoveSource, Verify }
public enum LeaderRelocationPhase { Prepared, Completed, Cancelled }

/// <summary>Same-faction residency only. Never authorizes recruitment, succession or faction merger.</summary>
public sealed class LeaderRelocationPlan
{
    private sealed record Data(int Version, string Charter, string Leader, string Faction, string Source, string Destination, string Book,
        string Preserved, int? Care, string SourceStock, int SourceFormat, string DestinationStock, int DestinationFormat,
        string[] SourceRoster, string[] DestinationRoster, long Tick)
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] public int ResidentProfile { get; init; }
    }
    private readonly Data data;
    public string Charter => data.Charter;
    public string Leader => data.Leader;
    public string Faction => data.Faction;
    public string Source => data.Source;
    public string Destination => data.Destination;
    public string Book => data.Book;
    public long Tick => data.Tick;
    public string Proof { get; }
    public int ResidentProfile => data.ResidentProfile;
    public int EffectiveResidentProfile => LeaderPreservationProfiles.Resolve(data.ResidentProfile);
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 180 && !s.Any(char.IsControl);
    private LeaderRelocationPlan(Data d)
    {
        data = d;
        _ = EffectiveResidentProfile;
        if (d.Version != 1 || !Key(d.Charter) || !Key(d.Leader) || !Key(d.Faction) || !Key(d.Source) || !Key(d.Destination) || d.Source == d.Destination ||
            !CityStockProof.Valid(d.Book) || !CityStockProof.Valid(d.Preserved) || d.Care is < 0 or > 4 || d.Tick < 0 ||
            !CityStockProof.Valid(d.SourceStock) || !CityStockProof.Valid(d.DestinationStock) || d.SourceFormat is < 0 or > 1 || d.DestinationFormat is < 0 or > 1 ||
            d.SourceRoster == null || d.DestinationRoster == null || d.SourceRoster.Length is < 1 or > 128 || d.DestinationRoster.Length > 127 ||
            d.SourceRoster.Concat(d.DestinationRoster).Any(s => !Key(s)) ||
            d.SourceRoster.Concat(d.DestinationRoster).Distinct(StringComparer.Ordinal).Count() != d.SourceRoster.Length + d.DestinationRoster.Length ||
            d.SourceRoster.Count(s => s == d.Leader) != 1)
            throw new ArgumentException("Invalid retained-office relocation plan.");
        Proof = Hash(JsonSerializer.Serialize(d));
    }
    public static string Hash(string value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(value ?? throw new ArgumentNullException(nameof(value)))));
    public static LeaderRelocationPlan Capture(string charter, string leader, string faction, string source, string destination, string book,
        ResidentMoveActual actor, string sourceStock, int sourceFormat, string destinationStock, int destinationFormat,
        IEnumerable<string> sourceRoster, IEnumerable<string> destinationRoster, long tick)
    {
        if (actor == null || actor.Id != leader || actor.Faction != faction) throw new ArgumentException("Wrong retained leader.");
        RuntimeCompat.NotNull(sourceRoster); RuntimeCompat.NotNull(destinationRoster);
        return new(new(1, charter, leader, faction, source, destination, Hash(book), actor.Preserved, actor.MedicalCare,
            sourceStock, sourceFormat, destinationStock, destinationFormat,
            sourceRoster.Take(129).OrderBy(s => s, StringComparer.Ordinal).ToArray(), destinationRoster.Take(129).OrderBy(s => s, StringComparer.Ordinal).ToArray(), tick));
    }
    public static LeaderRelocationPlan CaptureProfiled(string charter, string leader, string faction, string source, string destination, string book,
        ResidentMoveActual actor, string sourceStock, int sourceFormat, string destinationStock, int destinationFormat,
        IEnumerable<string> sourceRoster, IEnumerable<string> destinationRoster, long tick, int residentProfile)
    {
        _ = LeaderPreservationProfiles.Resolve(residentProfile);
        var original = Capture(charter, leader, faction, source, destination, book, actor, sourceStock, sourceFormat,
            destinationStock, destinationFormat, sourceRoster, destinationRoster, tick);
        return new(original.data with { ResidentProfile = residentProfile });
    }
    public LeaderRelocationAction Review(ResidentMoveActual actor, string actualLeader, string sourceOwner, string destinationOwner, string book,
        string sourceStock, int sourceFormat, string destinationStock, int destinationFormat, IEnumerable<string> sourceRoster, IEnumerable<string> destinationRoster)
    {
        if (actor == null || actor.Id != data.Leader || actualLeader != data.Leader || actor.Faction != data.Faction || sourceOwner != data.Faction || destinationOwner != data.Faction ||
            actor.Preserved != data.Preserved || actor.MedicalCare != data.Care || Hash(book) != data.Book ||
            sourceStock != data.SourceStock || destinationStock != data.DestinationStock || sourceFormat != data.SourceFormat || destinationFormat != data.DestinationFormat)
            throw new ArgumentException("Leader, custody, book or stock changed; never repair it.");
        RuntimeCompat.NotNull(sourceRoster); RuntimeCompat.NotNull(destinationRoster);
        var source = sourceRoster.Take(129).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var destination = destinationRoster.Take(129).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        bool original = source.SequenceEqual(data.SourceRoster);
        bool removed = source.SequenceEqual(data.SourceRoster.Where(s => s != data.Leader));
        bool copied = destination.SequenceEqual(data.DestinationRoster.Append(data.Leader).OrderBy(s => s, StringComparer.Ordinal));
        if (original && destination.SequenceEqual(data.DestinationRoster)) return LeaderRelocationAction.AddDestination;
        if (original && copied) return LeaderRelocationAction.RemoveSource;
        if (removed && copied) return LeaderRelocationAction.Verify;
        throw new ArgumentException("Unexpected residency change; do not invent a missing or replacement pawn.");
    }
    public string Export() => JsonSerializer.Serialize(new Save(data, Proof));
    public static LeaderRelocationPlan Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 100000) throw new ArgumentException("Invalid leader relocation snapshot.");
        var save = JsonSerializer.Deserialize<Save>(json);
        if (save?.Plan == null) throw new ArgumentException("Missing leader relocation plan.");
        var result = new LeaderRelocationPlan(save.Plan);
        if (result.Proof != save.Proof) throw new ArgumentException("Relocation proof mismatch.");
        return result;
    }
    private sealed record Save(Data Plan, string Proof);
}
