using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;

namespace Honor.Contracts.Preview;

/// <summary>Execution evidence only. The original prisoner remains saved by WorldPawns.</summary>
public sealed class ResidentPrisonerRecord : IExposable
{
    public Pawn Pawn;
    public string Snapshot;
    public string SourceHost;
    public string ResetSnapshot;
    public void ExposeData()
    {
        Scribe_References.Look(ref Pawn, "pawn"); Scribe_Values.Look(ref Snapshot, "prisonerV1");
        Scribe_Values.Look(ref SourceHost, "sourceHost"); Scribe_Values.Look(ref ResetSnapshot, "observedNativeResetV1");
    }
    public PrisonerEvidence Evidence()
    {
        if (string.IsNullOrWhiteSpace(Snapshot) || Snapshot.Length > 5000) throw new ArgumentException("Invalid prisoner record.");
        var evidence = JsonSerializer.Deserialize<PrisonerEvidence>(Snapshot);
        PrisonerPreservation.Proof(evidence);
        if (Pawn == null || Pawn.GetUniqueLoadID() != evidence.Id || string.IsNullOrWhiteSpace(SourceHost) || SourceHost.Length > 120 || SourceHost.Any(char.IsControl))
            throw new ArgumentException("Invalid prisoner reference or source.");
        return evidence;
    }
    public PrisonerNativeReset Reset()
    {
        if (ResetSnapshot == null) return null;
        if (ResetSnapshot.Length > 5000) throw new ArgumentException("Oversized native reset record.");
        return JsonSerializer.Deserialize<PrisonerNativeReset>(ResetSnapshot) ?? throw new ArgumentException("Invalid native reset record.");
    }
}

internal static class ResidentPrisoners
{
    private static readonly FieldInfo[] GuestFields = typeof(Pawn_GuestTracker).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly string[] KnownGuestFields = ["pawn", "interactionMode", "enabledNonExclusiveInteractions", "slaveInteractionMode",
        "hostFactionInt", "guestStatusInt", "joinStatus", "slaveFactionInt", "lastRecruiterName", "lastRecruiterOpinion",
        "hasOpinionOfLastRecruiter", "lastResistanceInteractionData", "finalResistanceInteractionData", "releasedInt",
        "ticksWhenAllowedToEscapeAgain", "spotToWaitInsteadOfEscaping", "lastPrisonBreakTicks", "everParticipatedInPrisonBreak",
        "resistance", "will", "ideoForConversion", "recruitable", "everEnslaved", "getRescuedThoughtOnUndownedBecauseOfPlayer", "leftAfterRescue"];
    private static readonly FieldInfo Priorities = AccessTools.Field(typeof(Pawn_WorkSettings), "priorities");
    private static readonly MethodInfo Native = AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus), [typeof(Faction), typeof(GuestStatus)]);
    private static string Hash(object value) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    internal static bool IsPrisoner(Pawn p) => p?.IsPrisoner == true;
    internal static string Authority(Pawn p) => (IsPrisoner(p) ? p.HostFaction : p.Faction)?.GetUniqueLoadID();
    internal static void Eligible(Pawn p)
    {
        if (!p.IsPrisoner || p.IsSlave || p.guest == null || p.guest.GetType() != typeof(Pawn_GuestTracker) ||
            p.HostFaction == null || p.HostFaction.IsPlayer || p.HostFaction == p.Faction || p.guest.SlaveFaction != null || p.guest.Released ||
            p.Ideo != null || p.mindState == null || p.workSettings == null || p.workSettings.GetType() != typeof(Pawn_WorkSettings) || !p.workSettings.Initialized || p.kindDef.destroyGearOnDrop ||
            p.kindDef.initialResistanceRange == null || p.kindDef.initialWillRange == null || p.IsWildMan() ||
            p.guest.ExclusiveInteractionMode == null || p.guest.ExclusiveInteractionMode == PrisonerInteractionModeDefOf.Enslave || p.guest.joinStatus == JoinStatus.Undefined ||
            p.ownership?.OwnedBed != null || p.ownership?.AssignedDeathrestCasket != null || p.ownership?.AssignedThrone != null ||
            p.mindState?.wantsToTradeWithColony == true || p.trader != null ||
            Native == null || Native.ReturnType != typeof(void) || Harmony.GetPatchInfo(Native)?.Owners.Any() == true)
            throw new ArgumentException("Unsupported prisoner custody, callbacks or persistent state.");
        var resistance = p.kindDef.initialResistanceRange.Value; var will = p.kindDef.initialWillRange.Value;
        if (!RuntimeCompat.IsFinite(resistance.min) || !RuntimeCompat.IsFinite(resistance.max) || resistance.min < 0 || resistance.max < resistance.min ||
            !RuntimeCompat.IsFinite(will.min) || !RuntimeCompat.IsFinite(will.max) || will.min < 0 || will.max < will.min)
            throw new ArgumentException("Invalid native prisoner initialization ranges.");
        // NPC-to-NPC host change keeps the prisoner role. The native drop path returns before touching unheld world-pawn gear.
        if (p.SpawnedOrAnyParentSpawned || p.InContainerEnclosed) throw new ArgumentException("Prisoner gear cannot be preserved here.");
    }
    private static object Value(FieldInfo field, object value)
    {
        if (value == null) return null;
        var type = field.FieldType;
        if (type == typeof(bool) || type == typeof(int) || type == typeof(string) || type.IsEnum) return value.ToString();
        if (type == typeof(float))
        {
            if (!RuntimeCompat.IsFinite((float)value)) throw new ArgumentException("Nonfinite prisoner state.");
            return ((float)value).ToString("R", CultureInfo.InvariantCulture);
        }
        if (value is IntVec3 cell) return new[] { cell.x, cell.y, cell.z };
        if (value is Def def && def.GetType().Assembly == typeof(Pawn).Assembly && def.defName != null) return def.defName;
        if (value is List<PrisonerInteractionModeDef> modes && modes.Count <= 128 && modes.All(d => d != null && d.GetType() == typeof(PrisonerInteractionModeDef)))
            return modes.Select(d => d.defName).ToArray();
        // Conversion/complex recruiter records and future unknown fields require explicit preservation support.
        throw new ArgumentException("Unsupported guest tracker field: " + field.Name);
    }
    internal static PrisonerEvidence Describe(Pawn p)
    {
        Eligible(p);
        if (GuestFields.Length != KnownGuestFields.Length || GuestFields.Any(f => !KnownGuestFields.Contains(f.Name)) ||
            !GuestFields.Any(f => f.Name == "pawn" && f.FieldType == typeof(Pawn) && f.GetValue(p.guest) == p) ||
            !GuestFields.Any(f => f.Name == "hostFactionInt" && f.FieldType == typeof(Faction) && f.GetValue(p.guest) == p.HostFaction))
            throw new ArgumentException("Unavailable guest field layout.");
        var fields = GuestFields.Where(f => f.Name is not ("pawn" or "hostFactionInt" or "resistance" or "will"))
            .OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => new { f.Name, Type = f.FieldType.FullName, Value = Value(f, f.GetValue(p.guest)) }).ToArray();
        // GetPriority returns display-normalized values when manual priorities are disabled. Bind the original raw priorities instead.
        if (Priorities == null || Priorities.IsStatic || Priorities.FieldType != typeof(DefMap<WorkTypeDef, int>) ||
            Priorities.GetValue(p.workSettings) is not DefMap<WorkTypeDef, int> map || map.Count != DefDatabase<WorkTypeDef>.DefCount ||
            DefDatabase<WorkTypeDef>.AllDefsListForReading.Any(d => d.index < 0 || d.index >= map.Count || map[d] is < 0 or > 4))
            throw new ArgumentException("Unsupported prisoner work priority map.");
        var priorities = DefDatabase<WorkTypeDef>.AllDefsListForReading.OrderBy(d => d.defName, StringComparer.Ordinal)
            .Select(d => new { d.defName, Priority = map[d] }).ToArray();
        var row = new PrisonerEvidence(p.GetUniqueLoadID(), p.Faction.GetUniqueLoadID(), Hash(new { Fields = fields, Priorities = priorities }), p.guest.resistance, p.guest.will);
        PrisonerPreservation.Proof(row); return row;
    }
    internal static ResidentPrisonerRecord Capture(Pawn p)
    {
        var row = Describe(p);
        return new() { Pawn = p, Snapshot = JsonSerializer.Serialize(row), SourceHost = Authority(p) };
    }
    internal static string Proof(Pawn p, ResidentPrisonerRecord record)
    {
        if (!IsPrisoner(p))
        {
            if (record != null) throw new ArgumentException("Prisoner status changed.");
            return PrisonerPreservation.None;
        }
        var actual = Describe(p);
        if (record == null) return PrisonerPreservation.Proof(actual);
        var before = record.Evidence();
        if (before.Id != actual.Id || before.OriginalFaction != actual.OriginalFaction || before.State != actual.State ||
            (actual.Resistance != before.Resistance || actual.Will != before.Will) &&
            (record.Reset() is not { } reset || Authority(p) != reset.Host || actual.Resistance != reset.Resistance || actual.Will != reset.Will))
            throw new ArgumentException("Prisoner preservation mismatch; never rewrite later progress.");
        return PrisonerPreservation.Proof(before);
    }
    internal static bool NeedsRestore(ResidentPrisonerRecord record, Faction source, Faction target)
    {
        if (source == null || target == null || source.IsPlayer || target.IsPlayer) throw new ArgumentException("Only reviewed NPC custodial authorities are supported.");
        var p = record.Pawn; var actual = Describe(p);
        if (record.SourceHost != source.GetUniqueLoadID()) throw new ArgumentException("Prisoner source changed.");
        return PrisonerPreservation.NeedsRestore(record.Evidence(), new(actual.Id, actual.OriginalFaction, Authority(p), actual.State, actual.Resistance, actual.Will),
            source.GetUniqueLoadID(), target.GetUniqueLoadID(), record.Reset());
    }
    internal static void ChangeHost(ResidentPrisonerRecord record, Faction source, Faction target)
    {
        NeedsRestore(record, source, target); var p = record.Pawn;
        if (Authority(p) != source.GetUniqueLoadID()) throw new ArgumentException("Prisoner is not at the reviewed source.");
        p.guest.SetGuestStatus(target, GuestStatus.Prisoner); // Never SetFaction, release, enslave or recapture the prisoner.
        // If the native call throws before this observation, recovery deliberately cannot invent a reset proof.
        var actual = Describe(p);
        if (Authority(p) != target.GetUniqueLoadID() || actual.OriginalFaction != record.Evidence().OriginalFaction || actual.State != record.Evidence().State)
            throw new ArgumentException("Native custody change did not preserve prisoner state.");
        if (actual.Resistance < 0 || actual.Will < 0) throw new ArgumentException("Native custody initialization was not observed.");
        record.ResetSnapshot = JsonSerializer.Serialize(new PrisonerNativeReset(target.GetUniqueLoadID(), actual.Resistance, actual.Will));
    }
    internal static void Restore(ResidentPrisonerRecord record, Faction source, Faction target)
    {
        if (NeedsRestore(record, source, target))
        {
            var before = record.Evidence();
            record.Pawn.guest.resistance = before.Resistance; record.Pawn.guest.will = before.Will;
        }
        if (Authority(record.Pawn) != target.GetUniqueLoadID() || NeedsRestore(record, source, target))
            throw new ArgumentException("Prisoner custody preservation incomplete.");
        // Consume the reset permission. Later natural progress must never match an old random reset and become repairable again.
        record.ResetSnapshot = null;
    }
}
