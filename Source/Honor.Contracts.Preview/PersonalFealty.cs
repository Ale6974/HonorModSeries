using HarmonyLib;
using Honor.Contracts.Rules;
using RimWorld;
using UnityEngine;
using Verse;

namespace Honor.Contracts.Preview;

public sealed class PersonalOathReference : IExposable
{
    public string Id;
    public Pawn Pawn;
    public Faction Realm;
    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id"); Scribe_References.Look(ref Pawn, "pawn"); Scribe_References.Look(ref Realm, "realm");
    }
}

/// <summary>Actual personal pledges; no native faction, territorial account or duty mutation.</summary>
public sealed class PersonalFealtyLedger : GameComponent
{
    private readonly Game game;
    private string snapshot;
    private List<PersonalOathReference> references = [];
    public PersonalOathBook Book { get; private set; } = new();
    public bool RestoreFailed { get; private set; }
    public PersonalFealtyLedger(Game game) { this.game = game; }
    private void Context()
    {
        if (Current.Game != game || Current.ProgramState != ProgramState.Playing || RestoreFailed || UnionWarCouncil.HasExternalProvider)
            throw new ArgumentException("Personal oath context is unavailable.");
        if (Book.Events.LastOrDefault()?.Tick > Find.TickManager.TicksGame) throw new ArgumentException("Personal oath clock is ahead of the loaded game.");
    }
    private Pawn Actor(string id) => references.SingleOrDefault(r => r.Id == id)?.Pawn ?? throw new ArgumentException("Original actor unavailable.");
    private Faction Realm(string id) => references.SingleOrDefault(r => r.Id == id)?.Realm ?? throw new ArgumentException("Original realm unavailable.");
    private static bool Subject(Pawn p) => p != null && p.GetType() == typeof(Pawn) && p.Faction == Faction.OfPlayer && p.IsColonist &&
        p.ageTracker != null && p.ageTracker.AgeBiologicalYears >= 18 && !p.Dead && !p.Destroyed && !p.Downed && !p.IsPrisoner && !p.IsSlave && !p.InMentalState && p.Awake() &&
        p.HostFaction == null && !p.HasExtraHomeFaction() && !p.HasExtraMiniFaction() && p.GetExtraHostFaction() == null &&
        PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Count(x => x == p) == 1 && Find.QuestManager != null && !Find.QuestManager.IsReservedByAnyQuest(p);
    internal IEnumerable<Pawn> Candidates() => Find.Maps.SelectMany(m => m.mapPawns.FreeColonistsSpawned).Where(Subject).Distinct().ToArray();
    internal IEnumerable<Faction> Realms() => Find.FactionManager.AllFactionsListForReading.Where(f => {
        try {
            var d = NpcDecisionAuthority.Observe(f);
            var e = new PersonalOathEvent(PersonalOathAction.Swear, "review-only", "review-player", d.Representative, d.Realm, Find.TickManager.TicksGame, d);
            var mandate = NativeNpcContractAuthority.ReviewPersonal(Book, e, f);
            Book.ValidateDecision(e with { Mandate = mandate });
            return true;
        } catch (ArgumentException) { return false; }
    }).ToArray();
    private static void Reference(Pawn p, List<PersonalOathReference> rows)
    {
        if (!rows.Any(r => r.Id == p.GetUniqueLoadID())) rows.Add(new() { Id = p.GetUniqueLoadID(), Pawn = p });
        else if (rows.Single(r => r.Id == p.GetUniqueLoadID()).Pawn != p) throw new ArgumentException("Actor identity changed.");
    }
    private void Commit(PersonalOathEvent e, Pawn first, Pawn second, Faction realm, Faction currentRealm = null)
    {
        Context();
        if (e.Decision != null) e = e with { Mandate = NativeNpcContractAuthority.ReviewPersonal(Book, e, realm) };
        var next = Book.Append(e); var rows = references.ToList();
        if (first != null) Reference(first, rows);
        if (second != null) Reference(second, rows);
        var existing = rows.SingleOrDefault(r => r.Id == realm.GetUniqueLoadID());
        if (existing == null) rows.Add(new() { Id = realm.GetUniqueLoadID(), Realm = realm });
        else if (existing.Realm != realm) throw new ArgumentException("Realm identity changed.");
        if (currentRealm != null)
        {
            if (e.PersonalRelease?.CurrentRealm != currentRealm.GetUniqueLoadID()) throw new ArgumentException("Personal release current realm does not match its original decision.");
            var actual = rows.SingleOrDefault(r => r.Id == currentRealm.GetUniqueLoadID());
            if (actual == null) rows.Add(new() { Id = currentRealm.GetUniqueLoadID(), Realm = currentRealm });
            else if (actual.Realm != currentRealm) throw new ArgumentException("Current personal realm identity changed.");
        }
        Validate(next, rows); Context();
        if (e.Decision != null)
        {
            var current = NpcDecisionAuthority.Observe(realm);
            var mandate = NativeNpcContractAuthority.ReviewPersonal(Book, e with { Decision = current }, realm);
            if (current != e.Decision || System.Text.Json.JsonSerializer.Serialize(mandate) != System.Text.Json.JsonSerializer.Serialize(e.Mandate))
                throw new ArgumentException("Personal mandate changed before commit.");
        }
        if (e.PersonalRelease != null && ReadPersonalRelease(second, first, realm) != e.PersonalRelease)
            throw new ArgumentException("Original person's current release decision changed before commit.");
        references = rows; Book = next;
    }
    private static void Validate(PersonalOathBook book, List<PersonalOathReference> rows)
    {
        if (rows == null || rows.Count > 8192 || rows.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || r.Pawn != null && r.Realm != null ||
            r.Pawn != null && r.Id != r.Pawn.GetUniqueLoadID() || r.Realm != null && r.Id != r.Realm.GetUniqueLoadID()) ||
            rows.Select(r => r.Id).Distinct().Count() != rows.Count) throw new ArgumentException("Invalid personal oath references.");
        var required = new HashSet<string>(book.Events.SelectMany(e => new[] { e.Actor, e.Other, e.Realm, e.PersonalRelease?.CurrentRealm }).Where(x => x != null), StringComparer.Ordinal);
        var actors = new HashSet<string>(book.Events.SelectMany(e => new[] { e.Actor, e.Other }), StringComparer.Ordinal);
        var realms = new HashSet<string>(book.Events.SelectMany(e => new[] { e.Realm, e.PersonalRelease?.CurrentRealm }).Where(x => x != null), StringComparer.Ordinal);
        if (actors.Overlaps(realms) || rows.Any(r => actors.Contains(r.Id) && r.Realm != null || realms.Contains(r.Id) && r.Pawn != null))
            throw new ArgumentException("Personal actors and realms cannot exchange identities.");
        if (rows.Count != required.Count || rows.Any(r => !required.Contains(r.Id))) throw new ArgumentException("Unpaired personal oath history.");
        foreach (var e in book.Events)
            if (!rows.Any(r => r.Id == e.Actor) || !rows.Any(r => r.Id == e.Other) || !rows.Any(r => r.Id == e.Realm && r.Realm != null))
                throw new ArgumentException("Missing oath references.");
        if (realms.Any(id => !rows.Any(r => r.Id == id && r.Realm != null))) throw new ArgumentException("Missing historical personal realm reference.");
        // Missing historical pawns are not regenerated. Missing current actors suspend explicit succession.
    }
    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving && !RestoreFailed && Book.Events.Count > 0) snapshot = Book.Export();
        Scribe_Values.Look(ref snapshot, "honorPersonalFealtyV1");
        Scribe_Collections.Look(ref references, "honorPersonalFealtyReferencesV1", LookMode.Deep);
        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        try { references ??= []; var restored = snapshot == null ? new PersonalOathBook() : PersonalOathBook.Restore(snapshot); Validate(restored, references); Book = restored; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Personal oath restore rejected; original snapshot and references retained. " + e.GetType().Name); }
    }
    public override void FinalizeInit()
    {
        if (!RestoreFailed && Book.Events.LastOrDefault()?.Tick > Find.TickManager.TicksGame)
        { RestoreFailed = true; Log.Error("[Honor.Contracts.Preview] Future personal oath clock rejected; original snapshot retained."); }
    }
    internal void Swear(Pawn subject, Faction realm, Pawn expectedLeader)
    {
        Context(); if (!Subject(subject)) throw new ArgumentException("Subject cannot presently swear.");
        var decision = NpcDecisionAuthority.Observe(realm);
        if (realm.leader != expectedLeader || decision.Representative != expectedLeader.GetUniqueLoadID()) throw new ArgumentException("Ruler changed since review.");
        Commit(new(PersonalOathAction.Swear, Guid.NewGuid().ToString("N"), subject.GetUniqueLoadID(), expectedLeader.GetUniqueLoadID(), realm.GetUniqueLoadID(), Find.TickManager.TicksGame, decision), subject, expectedLeader, realm);
    }
    internal void Heir(string id, Pawn expectedSubject, Pawn heir)
    {
        Context(); var s = Book.Oaths.Single(x => x.Id == id);
        var player = s.PlayerLiege ? s.Liege : s.Subject;
        if (Actor(player) != expectedSubject || !Subject(expectedSubject) || !Subject(heir)) throw new ArgumentException("Only the current player party can designate an eligible heir.");
        Commit(new PersonalOathEvent(PersonalOathAction.DesignateHeir, id, player, heir.GetUniqueLoadID(), s.Realm, Find.TickManager.TicksGame) { PlayerLiege = s.PlayerLiege }, expectedSubject, heir, Realm(s.Realm));
    }
    internal void AcceptVassal(Pawn playerLiege, Faction realm, Pawn expectedSubject)
    {
        Context(); if (!Subject(playerLiege)) throw new ArgumentException("Player liege cannot presently accept.");
        var decision = NpcDecisionAuthority.Observe(realm);
        if (realm.leader != expectedSubject || decision.Representative != expectedSubject.GetUniqueLoadID()) throw new ArgumentException("NPC subject changed after review.");
        Commit(new PersonalOathEvent(PersonalOathAction.Swear, Guid.NewGuid().ToString("N"), expectedSubject.GetUniqueLoadID(), playerLiege.GetUniqueLoadID(), realm.GetUniqueLoadID(), Find.TickManager.TicksGame, decision) { PlayerLiege = true }, expectedSubject, playerLiege, realm);
    }
    internal void Release(string id, Pawn expectedPlayer)
    {
        Context(); var s = Book.Oaths.Single(x => x.Id == id); var realm = Realm(s.Realm);
        var player = Actor(s.PlayerLiege ? s.Liege : s.Subject); var npc = Actor(s.PlayerLiege ? s.Subject : s.Liege);
        if (player != expectedPlayer || !Subject(player)) throw new ArgumentException("Current player party cannot release.");
        if (realm.leader == npc && npc.Faction == realm)
        {
            var decision = NpcDecisionAuthority.Observe(realm);
            if (decision.Representative != npc.GetUniqueLoadID()) throw new ArgumentException("Current bilateral personal release consent unavailable.");
            Commit(new PersonalOathEvent(PersonalOathAction.MutualRelease, id, player.GetUniqueLoadID(), npc.GetUniqueLoadID(), s.Realm, Find.TickManager.TicksGame, decision) { PlayerLiege = s.PlayerLiege }, player, npc, realm);
        }
        else
        {
            // A former ruler or emigrant may consent ONLY for their own personal oath, not their old office.
            var decision = ReadPersonalRelease(npc, player, realm);
            Commit(new PersonalOathEvent(PersonalOathAction.MutualRelease, id, player.GetUniqueLoadID(), npc.GetUniqueLoadID(), s.Realm, Find.TickManager.TicksGame) { PlayerLiege = s.PlayerLiege, PersonalRelease = decision }, player, npc, realm, decision.CurrentRealm == null ? null : npc.Faction);
        }
    }
    private PersonalReleaseDecision ReadPersonalRelease(Pawn npc, Pawn player, Faction originalRealm)
    {
        Context(); var factions = Find.FactionManager.AllFactionsListForReading; var realm = npc.Faction;
        if (realm == null) throw new ArgumentException("Original person has no current native realm.");
        bool migrated = realm != originalRealm;
        var regime = realm.def?.defName == UnionPoliticsLedger.UnionDefName ? NpcDecisionRegime.Union : realm.def?.royalTitleTags?.Count > 0 ? NpcDecisionRegime.Royal : NpcDecisionRegime.Ordinary;
        bool registered =
            realm.GetType() == typeof(Faction) && realm.def?.GetType() == typeof(FactionDef) && factions.Contains(realm) &&
            factions.Count(f => f.GetUniqueLoadID() == realm.GetUniqueLoadID()) == 1 && !realm.IsPlayer && !realm.Hidden && !realm.temporary &&
            !realm.defeated && !realm.def.permanentEnemy && realm.def.humanlikeFaction;
        bool ordinary = regime == NpcDecisionRegime.Ordinary;
        var decision = new PersonalReleaseDecision(npc.GetUniqueLoadID(), player.GetUniqueLoadID(), originalRealm.GetUniqueLoadID(), Find.TickManager.TicksGame, ordinary ? migrated ? 2 : 1 : migrated ? 4 : 3,
            registered && ordinary,
            npc.GetType() == typeof(Pawn) && npc.Faction == realm && !npc.Dead && !npc.Destroyed && !npc.Downed && npc.Awake() &&
            !npc.InMentalState && !npc.IsPrisoner && !npc.IsSlave && npc.HostFaction == null && !npc.HasExtraHomeFaction() && !npc.HasExtraMiniFaction() && npc.GetExtraHostFaction() == null &&
            npc.ageTracker != null && npc.ageTracker.AgeBiologicalYears >= 18 && PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Count(p => p == npc) == 1 &&
            (migrated || factions.All(f => f.leader != npc)) && Find.QuestManager != null && !Find.QuestManager.IsReservedByAnyQuest(npc) && !Find.QuestManager.IsReservedByAnyQuest(realm),
            realm.HasGoodwill && !realm.HostileTo(Faction.OfPlayer) && !Faction.OfPlayer.HostileTo(realm) && realm.GoodwillWith(Faction.OfPlayer) >= 50,
            !UnionWarCouncil.HasExternalProvider) { CurrentRealm = migrated ? realm.GetUniqueLoadID() : null, PersonalRegime = regime, RegisteredPersonalRealm = registered && !ordinary };
        if (!(decision.RegisteredOrdinaryRealm || decision.RegisteredPersonalRealm) || !decision.OriginalActorCapable || !decision.Willing || !decision.Independent || npc.Faction != realm || !Subject(player)) throw new ArgumentException("Original person's own release decision is unavailable or changed during review.");
        return decision;
    }
    internal void Succeed(string id, bool subjectSide)
    {
        Context(); ObserveDeaths(id); var s = Book.Oaths.Single(x => x.Id == id); var realm = Realm(s.Realm);
        var decision = NpcDecisionAuthority.Observe(realm); Pawn old, next;
        bool playerSide = subjectSide != s.PlayerLiege;
        if (playerSide)
        {
            old = OptionalActor(subjectSide ? s.Subject : s.Liege); next = Actor(s.Heir);
            if (!VerifiedDeath(s, subjectSide) || !Subject(next) || realm.leader != Actor(s.PlayerLiege ? s.Subject : s.Liege)) throw new ArgumentException("Original death, designated heir and unchanged NPC party required.");
        }
        else
        {
            old = OptionalActor(subjectSide ? s.Subject : s.Liege); next = realm.leader;
            var player = OptionalActor(s.PlayerLiege ? s.Liege : s.Subject);
            if (!VerifiedDeath(s, subjectSide) || !(Subject(player) || VerifiedDeath(s, !s.PlayerLiege) && s.Heir != null && Subject(Actor(s.Heir))) || decision.Representative != next.GetUniqueLoadID()) throw new ArgumentException("Dead original NPC party and actual successor required.");
        }
        Commit(new PersonalOathEvent(subjectSide ? PersonalOathAction.SubjectSuccession : PersonalOathAction.LiegeSuccession, id, subjectSide ? s.Subject : s.Liege, next.GetUniqueLoadID(), s.Realm, Find.TickManager.TicksGame, decision) { PlayerLiege = s.PlayerLiege, DeathWitnessV1 = true }, old, next, realm);
    }
    private Pawn OptionalActor(string id) => references.SingleOrDefault(r => r.Id == id)?.Pawn;
    private bool VerifiedDeath(PersonalOathState s, bool subjectSide)
    {
        string id = subjectSide ? s.Subject : s.Liege; var original = OptionalActor(id);
        return (subjectSide ? s.SubjectDeathTick : s.LiegeDeathTick).HasValue && (original == null || original.Dead) &&
            !PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Any(p => p.GetUniqueLoadID() == id && !p.Dead);
    }
    private void ObserveDeaths(string id)
    {
        Context();
        foreach (bool side in new[] { true, false })
        {
            var s = Book.Oaths.Single(x => x.Id == id);
            if (s.EndedTick.HasValue || (side ? s.SubjectDeathTick : s.LiegeDeathTick).HasValue) continue;
            var actor = OptionalActor(side ? s.Subject : s.Liege); var other = OptionalActor(side ? s.Liege : s.Subject);
            if (actor == null || !actor.Dead) continue;
            Commit(new PersonalOathEvent(PersonalOathAction.DeathObserved, id, side ? s.Subject : s.Liege, side ? s.Liege : s.Subject, s.Realm, Find.TickManager.TicksGame) { PlayerLiege = s.PlayerLiege }, actor, other, Realm(s.Realm));
        }
    }
    public override void GameComponentTick()
    {
        if (RestoreFailed || Current.Game != game || Current.ProgramState != ProgramState.Playing || Find.TickManager.TicksGame % 250 != 0 || UnionWarCouncil.HasExternalProvider) return;
        foreach (var id in Book.Oaths.Where(s => !s.EndedTick.HasValue).Select(s => s.Id).ToArray())
        {
            try
            {
                var s = Book.Oaths.Single(x => x.Id == id);
                ObserveDeaths(id); s = Book.Oaths.Single(x => x.Id == id);
                // Inherit an actual NPC office, never select or generate its successor ourselves.
                if (VerifiedDeath(s, s.PlayerLiege)) Succeed(id, s.PlayerLiege);
                s = Book.Oaths.Single(x => x.Id == id);
                if (VerifiedDeath(s, !s.PlayerLiege) && s.Heir != null) Succeed(id, !s.PlayerLiege);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            { /* Keep the original pledge; its existing explicit recovery actions remain available. */ }
        }
    }
    internal string Label(string id) => references.SingleOrDefault(r => r.Id == id)?.Pawn?.LabelShort ?? id;
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class PersonalFealtyCommand
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;
        if (__instance.Faction == Faction.OfPlayer && __instance.IsColonist)
            yield return new Command_Action { defaultLabel = "PF_Open".Translate(), defaultDesc = "PF_Scope".Translate(), action = () => Find.WindowStack.Add(new PersonalFealtyWindow(__instance)) };
    }
}

internal sealed class PersonalFealtyWindow : Window
{
    private readonly Pawn pawn;
    private readonly Game game = Current.Game;
    private Vector2 scroll;
    private float height = 900;
    public override Vector2 InitialSize => new(800, 700);
    internal PersonalFealtyWindow(Pawn pawn) { this.pawn = pawn; forcePause = true; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true; }
    private void Run(Action action)
    {
        try { if (game != Current.Game) throw new ArgumentException(); action(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        { Messages.Message("PF_Blocked".Translate(), MessageTypeDefOf.RejectInput); }
    }
    private void Confirm(string text, Action action)
    {
        var before = game.GetComponent<PersonalFealtyLedger>().Book.Export();
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Run(() => {
            if (game.GetComponent<PersonalFealtyLedger>().Book.Export() != before) throw new ArgumentException("Personal oath changed after review.");
            action();
        })));
    }
    public override void DoWindowContents(Rect rect)
    {
        if (game != Current.Game) return;
        var ledger = game.GetComponent<PersonalFealtyLedger>();
        var area = new Rect(0, 0, rect.width, rect.height - 45);
        Widgets.BeginScrollView(area, ref scroll, new Rect(0, 0, area.width - 20, height));
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 24, 20000)); list.Label("PF_Scope".Translate()); list.Label("PF_Mandates".Translate()); list.Label("CO_Mandates".Translate());
        if (ledger.RestoreFailed) list.Label("PF_Blocked".Translate());
        else
        {
            var own = ledger.Book.Oaths.SingleOrDefault(s => !s.EndedTick.HasValue && s.Subject == pawn.GetUniqueLoadID());
            if (own == null && list.ButtonText("PF_Swear".Translate()))
                Find.WindowStack.Add(new FloatMenu(ledger.Realms().Select(f => {
                    var ruler = f.leader;
                    return new FloatMenuOption(f.Name + " / " + ruler.LabelShort, () => Confirm("PF_Confirm".Translate(pawn.LabelShort, ruler.LabelShort), () => ledger.Swear(pawn, f, ruler)));
                }).ToList()));
            if (list.ButtonText("PF_AcceptVassal".Translate()))
                Find.WindowStack.Add(new FloatMenu(ledger.Realms().Select(f => {
                    var ruler = f.leader;
                    return new FloatMenuOption(f.Name + " / " + ruler.LabelShort, () => Confirm("PF_AcceptConfirm".Translate(ruler.LabelShort, pawn.LabelShort), () => ledger.AcceptVassal(pawn, f, ruler)));
                }).ToList()));
            foreach (var s in ledger.Book.Oaths.Where(s => s.Subject == pawn.GetUniqueLoadID() || s.Liege == pawn.GetUniqueLoadID() || s.Heir == pawn.GetUniqueLoadID()))
            {
                list.Gap(); list.Label("PF_State".Translate(ledger.Label(s.Subject), ledger.Label(s.Liege), s.Heir == null ? "—" : ledger.Label(s.Heir)));
                if (s.EndedTick.HasValue) list.Label("PF_Ended".Translate(s.EndedTick.Value));
                else
                {
                if ((s.PlayerLiege ? s.Liege : s.Subject) == pawn.GetUniqueLoadID() && list.ButtonText("PF_Heir".Translate()))
                    Find.WindowStack.Add(new FloatMenu(ledger.Candidates().Where(p => p != pawn).Select(p => new FloatMenuOption(p.LabelShort,
                        () => Confirm("PF_HeirConfirm".Translate(p.LabelShort), () => ledger.Heir(s.Id, pawn, p)))).ToList()));
                if (list.ButtonText("PF_PlayerSuccession".Translate())) Confirm("PF_Recovery".Translate(), () => ledger.Succeed(s.Id, !s.PlayerLiege));
                if (list.ButtonText("PF_NpcSuccession".Translate())) Confirm("PF_Recovery".Translate(), () => ledger.Succeed(s.Id, s.PlayerLiege));
                if ((s.PlayerLiege ? s.Liege : s.Subject) == pawn.GetUniqueLoadID() && list.ButtonText("PF_Release".Translate())) Confirm("PF_ReleaseConfirm".Translate(), () => ledger.Release(s.Id, pawn));
                }
                foreach (var e in ledger.Book.Events.Where(e => e.Oath == s.Id)) list.Label("PF_Event".Translate(("PF_Action" + (int)e.Action).Translate(), e.Tick, ledger.Label(e.Actor), ledger.Label(e.Other)));
            }
        }
        height = list.CurHeight + 25; list.End(); Widgets.EndScrollView();
    }
}
