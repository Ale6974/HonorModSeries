using System.Text.Json;
namespace Honor.Contracts.Rules;

public sealed record OriginalDropInput(string Parent, string City, string Map, string Pawn, string Job,
    string Cargo, int Count, int Cell, int Tick);
public sealed record OriginalDropOutput(string Start, bool Returned, bool SameStackAtDestination, int Tick);

public static class LawfulHaulDrop
{
    public static void Validate(TerritoryTransfer transfer, int? profile, string start, string finish, long now)
    {
        if (start == null && finish == null) return;
        if (start == null || start.Length > 4096 || finish?.Length > 1024 ||
            transfer.LawfulHaulParentProof == null || profile != 16)
            throw new ArgumentException("Original lawful hauling record is incomplete.");
        var input = JsonSerializer.Deserialize<OriginalDropInput>(start);
        var map = LoadedMapPreservation.Restore(transfer.LoadedMap);
        if (input == null || JsonSerializer.Serialize(input) != start ||
            input.Parent != transfer.LawfulHaulParentProof || input.City != transfer.Id || input.Map != map.Map ||
            input.Count <= 0 || input.Cell < 0 || input.Cell >= (long)map.Width * map.Height ||
            input.Tick < transfer.PreparedTick || input.Tick > (transfer.ResolvedTick ?? now))
            throw new ArgumentException("Original lawful hauling input changed.");
        var actor = map.ActiveWorkV1?.SingleOrDefault(row => row.Pawn == input.Pawn);
        if (actor == null || actor.Jobs.FirstOrDefault() != input.Job)
            throw new ArgumentException("Original hauling job and resident required.");
        using var saved = JsonDocument.Parse(actor.Saved);
        if (!saved.RootElement.TryGetProperty("Carry", out var cargo) || cargo.ValueKind != JsonValueKind.Object ||
            !cargo.TryGetProperty("Id", out var id) || id.GetString() != input.Cargo ||
            !cargo.TryGetProperty("stackCount", out var count) || !count.TryGetInt32(out int originalCount) ||
            originalCount != input.Count)
            throw new ArgumentException("Original carried stack and count required.");
        if (finish == null) return; // An interrupted call retains only its start.
        var output = JsonSerializer.Deserialize<OriginalDropOutput>(finish);
        if (output == null || JsonSerializer.Serialize(output) != finish || output.Start != WorkProgress.Hash(start) ||
            output.Tick < input.Tick || output.Tick > (transfer.ResolvedTick ?? now) ||
            output.SameStackAtDestination && !output.Returned)
            throw new ArgumentException("Unpaired original lawful hauling output.");
    }
}
