using System.Text;
using System.Text.Json;

namespace Honor.Contracts.Rules;

public sealed record SurgeryOrder(string Id, string Recipe, string Body, int PartIndex, string PartDef, string Configuration);
public enum SurgeryCustodyAction { Detach, ChangeFaction, Restore, Verify }

/// <summary>Conserves ordered, unstarted medical orders across one native faction change.</summary>
public sealed class SurgeryOrderSet
{
    public const int Capacity = 15;
    public IReadOnlyList<SurgeryOrder> Orders { get; }
    public string Proof { get; }
    private SurgeryOrderSet(SurgeryOrder[] orders)
    {
        Orders = Array.AsReadOnly(orders);
        Proof = RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Save(1, orders)))));
    }
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public static SurgeryOrderSet Capture(IEnumerable<SurgeryOrder> orders)
    {
        RuntimeCompat.NotNull(orders); var rows = orders.Take(Capacity + 1).ToArray();
        if (rows.Length > Capacity || rows.Any(r => r == null || !Key(r.Id) || !Key(r.Recipe) || !Key(r.Body) ||
            r.PartIndex is < -1 or > 10000 || (r.PartIndex == -1 ? r.PartDef != null : !Key(r.PartDef)) || !CityStockProof.Valid(r.Configuration)) ||
            rows.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new ArgumentException("Invalid unstarted surgical order set.");
        return new(rows);
    }
    public static void ValidateDistinctOwners(IEnumerable<SurgeryOrderSet> owners)
    {
        RuntimeCompat.NotNull(owners); var all = owners.Take(ResidentMoveSet.Capacity + 1).ToArray();
        if (all.Length > ResidentMoveSet.Capacity || all.Any(s => s == null)) throw new ArgumentException("Invalid surgical owner set.");
        var ids = all.SelectMany(s => s.Orders).Select(o => o.Id).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new ArgumentException("A surgical ID belongs to more than one resident.");
    }
    public SurgeryCustodyAction Review(IEnumerable<SurgeryOrder> attached, IEnumerable<SurgeryOrder> detached, bool atTarget)
    {
        var a = Capture(attached); var d = Capture(detached);
        if (a.Orders.SequenceEqual(Orders) && d.Orders.Count == 0)
            return atTarget ? SurgeryCustodyAction.Verify : Orders.Count == 0 ? SurgeryCustodyAction.ChangeFaction : SurgeryCustodyAction.Detach;
        if (a.Orders.Count == 0 && d.Orders.SequenceEqual(Orders))
            return atTarget ? SurgeryCustodyAction.Restore : SurgeryCustodyAction.ChangeFaction;
        throw new ArgumentException("Surgery objects, order, ownership or configuration changed.");
    }
    public string Export() => JsonSerializer.Serialize(new Save(1, Orders.ToArray()));
    public void VerifyAttached(IEnumerable<SurgeryOrder> attached, IEnumerable<SurgeryOrder> detached)
    {
        if (Review(attached, detached, true) != SurgeryCustodyAction.Verify) throw new ArgumentException("Surgical originals remain detached.");
    }
    public SurgeryOrder DecodePartLink(SurgeryOrder actual, bool partWasPresentAtSave)
    {
        var expected = Orders.SingleOrDefault(o => o.Id == actual?.Id) ?? throw new ArgumentException("Unrecorded surgical link.");
        if (partWasPresentAtSave != (expected.PartIndex >= 0)) throw new ArgumentException("Surgical part changed before save.");
        if (actual == expected) return actual;
        if (actual.PartIndex != -1 || actual.PartDef != null || expected.PartIndex < 0 ||
            actual with { PartIndex = expected.PartIndex, PartDef = expected.PartDef } != expected)
            throw new ArgumentException("Only a proven missing decode link can be filled.");
        return expected;
    }
    public static SurgeryOrderSet Restore(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 20000) throw new ArgumentException("Invalid surgical snapshot size.");
        var save = JsonSerializer.Deserialize<Save>(json);
        if (save == null || save.Version != 1) throw new ArgumentException("Unsupported surgical snapshot.");
        return Capture(save.Orders);
    }
    private sealed record Save(int Version, SurgeryOrder[] Orders);
}
