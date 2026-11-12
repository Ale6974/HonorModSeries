using System.Collections;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using Verse;
namespace Honor.Contracts.Preview;
internal sealed record RoyalRightsCapture(string Document, List<Faction> Factions, List<Pawn> Heirs, List<Ability> Abilities);
/// <summary>B05 reads original saved fields. Native royalty remains their sole deep-save owner.</summary>
internal static class RoyalRightsAdapter
{
    private static object Raw(object owner, Type declared, string name)
    { var f = AccessTools.Field(declared, name); if (owner == null || f == null || f.IsStatic || f.DeclaringType != declared) throw new ArgumentException("Unknown saved royal schema."); return f.GetValue(owner); }
    private static T Field<T>(object owner, Type declared, string name)
    { object v = Raw(owner, declared, name); if (v is not T typed || v.GetType() != typeof(T)) throw new ArgumentException("Unknown royal field/container type."); return typed; }
    private static T Registered<T>(T def) where T : Def
    { if (def == null || def.GetType() != typeof(T) || DefDatabase<T>.GetNamedSilentFail(def.defName) != def) throw new ArgumentException("Original registered royal definition required."); return def; }
    private static string[] IDs(IEnumerable<string> ids) => ids.Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
    private static void Unpatched(Type t, string name)
    { var m = AccessTools.Method(t, name); if (m == null || Harmony.GetPatchInfo(m)?.Owners.Any() == true) throw new ArgumentException("Unknown royal saved/read dispatch."); }
    internal static bool HasTitles(Pawn pawn) => pawn?.royalty != null && Field<List<RoyalTitle>>(pawn.royalty, typeof(Pawn_RoyaltyTracker), "titles").Count > 0;
    internal static RoyalRightsCapture Capture(IEnumerable<Pawn> people)
    {
        if (!NativeTransferBodies.RoyalCustodyBodies || !ModsConfig.RoyaltyActive) throw new ArgumentException("Unreviewed private royal bodies.");
        Unpatched(typeof(RoyalTitle), nameof(RoyalTitle.ExposeData)); Unpatched(typeof(FactionPermit), nameof(FactionPermit.ExposeData));
        Unpatched(typeof(Pawn_RoyaltyTracker), "AssignHeirIfNone"); Unpatched(typeof(RoyalTitleDef), "get_Awardable");
        Unpatched(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.ExposeData)); Unpatched(typeof(Pawn_RoyaltyTracker), nameof(Pawn_RoyaltyTracker.UpdateAvailableAbilities));
        var factions = new Dictionary<string, Faction>(StringComparer.Ordinal); var heirs = new Dictionary<string, Pawn>(StringComparer.Ordinal); var abilities = new Dictionary<string, Ability>(StringComparer.Ordinal);
        var rows = new List<RoyalRightRow>();
        foreach (var pawn in (people ?? []).OrderBy(p => p?.GetUniqueLoadID(), StringComparer.Ordinal))
        {
            if (pawn?.GetType() != typeof(Pawn) || pawn.royalty?.GetType() != typeof(Pawn_RoyaltyTracker) || pawn.royalty.pawn != pawn || Find.QuestManager == null || Find.QuestManager.IsReservedByAnyQuest(pawn)) throw new ArgumentException("Original royal owner unavailable.");
            var realms = new List<string>(); var ownedHeirs = new List<string>(); var ownedAbilities = new List<string>();
            string Realm(Faction f)
            { if (f?.GetType() != typeof(Faction) || Find.FactionManager.AllFactionsListForReading.Count(x => ReferenceEquals(x, f)) != 1) throw new ArgumentException("Missing original claimant."); string id = f.GetUniqueLoadID(); factions[id] = f; if (!f.IsPlayer) realms.Add(id); return id; }
            var r = pawn.royalty;
            var titles = Field<List<RoyalTitle>>(r, typeof(Pawn_RoyaltyTracker), "titles");
            var favor = Field<Dictionary<Faction, int>>(r, typeof(Pawn_RoyaltyTracker), "favor");
            var highest = Field<Dictionary<Faction, RoyalTitleDef>>(r, typeof(Pawn_RoyaltyTracker), "highestTitles");
            var inherited = Field<Dictionary<Faction, Pawn>>(r, typeof(Pawn_RoyaltyTracker), "heirs");
            var permits = Field<List<FactionPermit>>(r, typeof(Pawn_RoyaltyTracker), "factionPermits");
            var spells = Field<List<Ability>>(r, typeof(Pawn_RoyaltyTracker), "abilities");
            if (new[] { titles.Count, favor.Count, highest.Count, inherited.Count, permits.Count, spells.Count }.Any(n => n > 256)) throw new ArgumentException("Royal scope capacity.");
            if (titles.Any(t => t == null || t.GetType() != typeof(RoyalTitle) || t.pawn != pawn || !inherited.ContainsKey(t.faction) && (t.def == null || t.def.favorCost > 0)) || titles.Select(t => t.faction).Distinct().Count() != titles.Count) throw new ArgumentException("Original title/load-stable heir entry required; never generate heirs on load.");
            var titleData = titles.Select(t => new { Faction = Realm(t.faction), Def = Registered(t.def).defName, t.receivedTick, t.wasInherited, t.conceited }).ToArray();
            var favorData = favor.OrderBy(e => e.Key.GetUniqueLoadID(), StringComparer.Ordinal).Select(e => new { Faction = Realm(e.Key), Value = e.Value }).ToArray();
            var highestData = highest.OrderBy(e => e.Key.GetUniqueLoadID(), StringComparer.Ordinal).Select(e => new { Faction = Realm(e.Key), Def = Registered(e.Value).defName }).ToArray();
            var heirData = inherited.OrderBy(e => e.Key.GetUniqueLoadID(), StringComparer.Ordinal).Select(e => {
                string id = e.Value?.GetUniqueLoadID(); if (e.Value != null) { if (e.Value.GetType() != typeof(Pawn) || e.Value.Destroyed) throw new ArgumentException("Original heir reference missing."); heirs[id] = e.Value; ownedHeirs.Add(id); }
                return new { Faction = Realm(e.Key), Pawn = id };
            }).ToArray();
            // Native PostLoad removes superseded permits. Require already normalized originals instead of silently repairing them.
            var permitData = permits.Select(p => {
                if (p?.GetType() != typeof(FactionPermit)) throw new ArgumentException("Unknown permit type.");
                var f = Field<Faction>(p, typeof(FactionPermit), "faction"); var title = Registered(Field<RoyalTitleDef>(p, typeof(FactionPermit), "title")); var permit = Registered(Field<RoyalTitlePermitDef>(p, typeof(FactionPermit), "permit"));
                if (permits.Any(other => !ReferenceEquals(other, p) && ReferenceEquals(Raw(other, typeof(FactionPermit), "faction"), f) && Raw(other, typeof(FactionPermit), "permit") is RoyalTitlePermitDef higher && higher.prerequisite == permit)) throw new ArgumentException("Unnormalized superseded permit.");
                return new { Faction = Realm(f), Title = title.defName, Permit = permit.defName, LastUsed = Field<int>(p, typeof(FactionPermit), "lastUsedTick") };
            }).ToArray();
            var expected = titles.SelectMany(t => t.def.grantedAbilities ?? throw new ArgumentException("Ability definition list unavailable.")).Distinct().ToArray();
            if (spells.Select(a => a?.def).Distinct().Count() != spells.Count || !new HashSet<AbilityDef>(expected).SetEquals(spells.Select(a => a?.def))) throw new ArgumentException("Native load would add/remove royal abilities.");
            var spellData = spells.Select(a => {
                if ((a?.GetType() != typeof(Ability) && a?.GetType() != typeof(Psycast)) || a.pawn != pawn || a.Id < 0 || a.sourcePrecept != null) throw new ArgumentException("Only original basic royal ability ownership is reviewed.");
                Registered(a.def); Unpatched(typeof(Ability), nameof(Ability.ExposeData)); Unpatched(typeof(Ability), nameof(Ability.GetUniqueLoadID)); Unpatched(typeof(Ability), nameof(Ability.Initialize));
                string id = a.GetUniqueLoadID(); if (abilities.ContainsKey(id)) throw new ArgumentException("Duplicate original ability ID."); abilities[id] = a; ownedAbilities.Add(id);
                return AbilityData(a, pawn);
            }).ToArray();
            var personal = Current.Game.GetComponent<PersonalFealtyLedger>();
            if (personal.RestoreFailed) throw new ArgumentException("Personal oath provenance unavailable.");
            var oaths = personal.Book.Oaths.Where(o => !o.EndedTick.HasValue && (o.Subject == pawn.GetUniqueLoadID() || o.Liege == pawn.GetUniqueLoadID() || o.Heir == pawn.GetUniqueLoadID())).OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();
            foreach (var oath in oaths) { var realm = Find.FactionManager.AllFactionsListForReading.Single(f => f.GetUniqueLoadID() == oath.Realm); Realm(realm); }
            string data = JsonSerializer.Serialize(new { Titles = titleData, Favor = favorData, Highest = highestData, Heirs = heirData, Permits = permitData, Abilities = spellData, Oaths = oaths, r.lastDecreeTicks, r.allowRoomRequirements, r.allowApparelRequirements });
            rows.Add(new(pawn.GetUniqueLoadID(), data, IDs(realms), IDs(ownedHeirs), IDs(ownedAbilities)));
        }
        return new(RoyalRights.Capture(rows), factions.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Value).ToList(), heirs.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Value).ToList(), abilities.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Value).ToList());
    }
    private static object AbilityData(Ability a, Pawn pawn)
    {
        if (a.GetType() == typeof(Psycast))
        {
            if (Raw(a, typeof(Psycast), "moteCast") != null || AccessTools.Method(typeof(Psycast), nameof(Ability.ExposeData))?.DeclaringType != typeof(Ability)) throw new ArgumentException("Psycast has active or unknown saved state.");
            foreach (var ctor in typeof(Psycast).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (Harmony.GetPatchInfo(ctor)?.Owners.Any() == true) throw new ArgumentException("Unknown Psycast load constructor.");
        }
        foreach (string name in new[] { "warmupMote", "warmupEffecter", "soundCast" }) if (Raw(a, typeof(Ability), name) != null) throw new ArgumentException("Active royal ability effect.");
        foreach (string name in new[] { "preCastActions", "maintainedEffecters", "customWarmupMotes" }) if (Raw(a, typeof(Ability), name) is not ICollection list || list.Count != 0) throw new ArgumentException("Active or unknown ability work.");
        if (Field<bool>(a, typeof(Ability), "wasCastingOnPrevTick")) throw new ArgumentException("Ability cast in progress.");
        var comps = a.comps;
        if (a.def.comps == null || (a.def.comps.Count == 0 ? comps != null : comps == null || comps.Count != a.def.comps.Count)) throw new ArgumentException("Ability component scope changed.");
        var compData = comps?.Select((c, i) => {
            var saved = c == null ? null : AccessTools.Method(c.GetType(), nameof(AbilityComp.PostExposeData));
            var initialize = c == null ? null : AccessTools.Method(c.GetType(), nameof(AbilityComp.Initialize));
            if (c == null || c.GetType().Assembly != typeof(Ability).Assembly || c.parent != a || c.props != a.def.comps[i] || c.GetType() != c.props.compClass || saved?.DeclaringType != typeof(AbilityComp) || initialize?.DeclaringType != typeof(AbilityComp) || Harmony.GetPatchInfo(saved)?.Owners.Any() == true || Harmony.GetPatchInfo(initialize)?.Owners.Any() == true) throw new ArgumentException("Stateful/custom ability comp requires a separate reviewed adapter.");
            for (var type = c.GetType(); type != typeof(AbilityComp); type = type.BaseType)
                if (type == null || type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length != 0) throw new ArgumentException("Ability comp has unreviewed private state.");
            return c.GetType().FullName;
        }).ToArray();
        var tracker = Field<VerbTracker>(a, typeof(Ability), "verbTracker"); if (tracker.directOwner != a) throw new ArgumentException("Ability verb owner changed.");
        Unpatched(typeof(VerbTracker), nameof(VerbTracker.ExposeData));
        var raw = Raw(tracker, typeof(VerbTracker), "verbs");
        object verbs = null;
        if (raw != null)
        {
            if (raw.GetType() != typeof(List<Verb>)) throw new ArgumentException("Unknown original verbs.");
            verbs = ((List<Verb>)raw).Select(v => {
                if (v?.GetType() != typeof(Verb_CastAbility) || v.verbTracker != tracker || v.caster != pawn || v.state != VerbState.Idle || v.castCompleteCallback != null || v.verbProps != a.def.verbProperties || AccessTools.Method(v.GetType(), nameof(Verb.ExposeData))?.DeclaringType != typeof(Verb)) throw new ArgumentException("Unreviewed/active royal verb.");
                Unpatched(typeof(Verb), nameof(Verb.ExposeData));
                if (Field<int>(v, typeof(Verb), "burstShotsLeft") != 0 || Field<bool>(v, typeof(Verb), "nonInterruptingSelfCast") || Raw(v, typeof(Verb), "maintainedEffecters") is not ICollection effects || effects.Count != 0) throw new ArgumentException("Royal verb still has active work.");
                if (!Field<LocalTargetInfo>(v, typeof(Verb), "currentTarget").Equals(LocalTargetInfo.Invalid) || !Field<LocalTargetInfo>(v, typeof(Verb), "currentDestination").Equals(LocalTargetInfo.Invalid)) throw new ArgumentException("Ability retains a target.");
                return new { v.loadID, State = (int)v.state, Burst = Field<int>(v, typeof(Verb), "burstShotsLeft"), Next = Field<int>(v, typeof(Verb), "ticksToNextBurstShot"), Last = Field<int>(v, typeof(Verb), "lastShotTick"), Surprise = Field<bool>(v, typeof(Verb), "surpriseAttack"), NonTarget = Field<bool>(v, typeof(Verb), "canHitNonTargetPawnsNow"), v.preventFriendlyFire, Self = Field<bool>(v, typeof(Verb), "nonInterruptingSelfCast") };
            }).ToArray();
        }
        var savedAbility = new { a.Id, Def = a.def.defName, a.maxCharges, a.lastCastTick, Cooldown = Field<bool>(a, typeof(Ability), "inCooldown"), End = Field<int>(a, typeof(Ability), "cooldownEndTick"), Duration = Field<int>(a, typeof(Ability), "cooldownDuration"), Charges = Field<int>(a, typeof(Ability), "charges"), Comps = compData, Verbs = verbs };
        return a.GetType() == typeof(Ability) ? savedAbility : (object)new { Class = typeof(Psycast).FullName, Saved = savedAbility };
    }
    internal static void Attach(ResidentExecutionRecord record, RoyalRightsCapture capture)
    { if (record == null) return; record.RoyalRightsDocument = capture.Document; record.RoyalFactions = capture.Factions; record.RoyalHeirs = capture.Heirs; record.RoyalAbilities = capture.Abilities; }
    internal static void Verify(ResidentExecutionRecord record, string original, string proof)
    {
        RoyalRights.Pair(original, proof, record?.Pawns.Select(p => p.GetUniqueLoadID()) ?? []);
        var actual = Capture(record?.Pawns); RoyalRights.RequireOriginal(original, actual.Document);
        if (record != null && (record.RoyalRightsDocument != original || record.RoyalFactions == null || !record.RoyalFactions.SequenceEqual(actual.Factions) || record.RoyalHeirs == null || !record.RoyalHeirs.SequenceEqual(actual.Heirs) || record.RoyalAbilities == null || !record.RoyalAbilities.SequenceEqual(actual.Abilities))) throw new ArgumentException("Original royal references changed/missing.");
    }
}
