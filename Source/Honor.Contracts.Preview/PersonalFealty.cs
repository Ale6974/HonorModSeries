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
    }
    private Pawn Actor(string id) => references.SingleOrDefault(r => r.Id == id)?.Pawn ?? throw new ArgumentException("Original actor unavailable.");
    private Faction Realm(string id) => references.SingleOrDefault(r => r.Id == id)?.Realm ?? throw new ArgumentException("Original realm unavailable.");
    private static bool Subject(Pawn p) => p != null && p.GetType() == typeof(Pawn) && p.Faction == Faction.OfPlayer && p.IsColonist &&
        p.ageTracker != null && p.ageTracker.AgeBiologicalYears >= 18 && !p.Dead && !p.Destroyed && !p.Downed && !p.IsPrisoner && !p.IsSlave && !p.InMentalState && p.Awake() &&
        p.HostFaction == null && !p.HasExtraHomeFaction() && !p.HasExtraMiniFaction() && p.GetExtraHostFaction() == null &&
        PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Count(x => x == p) == 1 && Find.QuestManager != null && !Find.QuestManager.IsReservedByAnyQuest(p);
    internal IEnumerable<Pawn> Candidates() => Find.Maps.SelectMany(m => m.mapPawns.FreeColonistsSpawned).Where(Subject).Distinct().ToArray();
    internal IEnumerable<Faction> Realms() => Find.FactionManager.AllFactionsListForReading.Where(f => {
        try { NpcDecisionAuthority.Read(f); return true; } catch (ArgumentException) { return false; }
    }).ToArray();
    private static void Reference(Pawn p, List<PersonalOathReference> rows)
    {
        if (!rows.Any(r => r.Id == p.GetUniqueLoadID())) rows.Add(new() { Id = p.GetUniqueLoadID(), Pawn = p });
        else if (rows.Single(r => r.Id == p.GetUniqueLoadID()).Pawn != p) throw new ArgumentException("Actor identity changed.");
    }
    private void Commit(PersonalOathEvent e, Pawn first, Pawn second, Faction realm)
    {
        Context(); var next = Book.Append(e); var rows = references.ToList(); Reference(first, rows); Reference(second, rows);
        var existing = rows.SingleOrDefault(r => r.Id == realm.GetUniqueLoadID());
        if (existing == null) rows.Add(new() { Id = realm.GetUniqueLoadID(), Realm = realm });
        else if (existing.Realm != realm) throw new ArgumentException("Realm identity changed.");
        Validate(next, rows); Context(); references = rows; Book = next;
    }
    private static void Validate(PersonalOathBook book, List<PersonalOathReference> rows)
    {
        if (rows == null || rows.Count > 8192 || rows.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || r.Pawn != null && r.Realm != null ||
            r.Pawn != null && r.Id != r.Pawn.GetUniqueLoadID() || r.Realm != null && r.Id != r.Realm.GetUniqueLoadID()) ||
            rows.Select(r => r.Id).Distinct().Count() != rows.Count) throw new ArgumentException("Invalid personal oath references.");
        var required = new HashSet<string>(book.Events.SelectMany(e => new[] { e.Actor, e.Other, e.Realm }), StringComparer.Ordinal);
        if (rows.Count != required.Count || rows.Any(r => !required.Contains(r.Id))) throw new ArgumentException("Unpaired personal oath history.");
        foreach (var e in book.Events)
            if (!rows.Any(r => r.Id == e.Actor) || !rows.Any(r => r.Id == e.Other) || !rows.Any(r => r.Id == e.Realm && r.Realm != null))
                throw new ArgumentException("Missing oath references.");
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
    internal void Swear(Pawn subject, Faction realm, Pawn expectedLeader)
    {
        Context(); if (!Subject(subject)) throw new ArgumentException("Subject cannot presently swear.");
        var decision = NpcDecisionAuthority.Read(realm);
        if (realm.leader != expectedLeader || decision.Representative != expectedLeader.GetUniqueLoadID()) throw new ArgumentException("Ruler changed since review.");
        Commit(new(PersonalOathAction.Swear, Guid.NewGuid().ToString("N"), subject.GetUniqueLoadID(), expectedLeader.GetUniqueLoadID(), realm.GetUniqueLoadID(), Find.TickManager.TicksGame, decision), subject, expectedLeader, realm);
    }
    internal void Heir(string id, Pawn expectedSubject, Pawn heir)
    {
        Context(); var s = Book.Oaths.Single(x => x.Id == id);
        if (Actor(s.Subject) != expectedSubject || !Subject(expectedSubject) || !Subject(heir)) throw new ArgumentException("Only the current subject can designate an eligible heir.");
        Commit(new(PersonalOathAction.DesignateHeir, id, s.Subject, heir.GetUniqueLoadID(), s.Realm, Find.TickManager.TicksGame), expectedSubject, heir, Realm(s.Realm));
    }
    internal void Succeed(string id, bool subjectSide)
    {
        Context(); var s = Book.Oaths.Single(x => x.Id == id); var realm = Realm(s.Realm);
        var decision = NpcDecisionAuthority.Read(realm); Pawn old, next;
        if (subjectSide)
        {
            old = Actor(s.Subject); next = Actor(s.Heir);
            if (!old.Dead || !Subject(next) || realm.leader != Actor(s.Liege)) throw new ArgumentException("Original death, designated heir and unchanged ruler required.");
        }
        else
        {
            old = Actor(s.Liege); next = realm.leader;
            var subject = Actor(s.Subject);
            if (!old.Dead || !(Subject(subject) || subject.Dead && s.Heir != null && Subject(Actor(s.Heir))) || decision.Representative != next.GetUniqueLoadID()) throw new ArgumentException("Dead original ruler and actual successor required.");
        }
        Commit(new(subjectSide ? PersonalOathAction.SubjectSuccession : PersonalOathAction.LiegeSuccession, id, old.GetUniqueLoadID(), next.GetUniqueLoadID(), s.Realm, Find.TickManager.TicksGame, decision), old, next, realm);
    }
    public override void GameComponentTick()
    {
        if (RestoreFailed || Current.Game != game || Current.ProgramState != ProgramState.Playing || Find.TickManager.TicksGame % 250 != 0 || UnionWarCouncil.HasExternalProvider) return;
        foreach (var id in Book.Oaths.Select(s => s.Id).ToArray())
        {
            try
            {
                var s = Book.Oaths.Single(x => x.Id == id);
                // Inherit an actual NPC office, never select or generate its successor ourselves.
                if (Actor(s.Liege).Dead) Succeed(id, false);
                s = Book.Oaths.Single(x => x.Id == id);
                if (Actor(s.Subject).Dead && s.Heir != null) Succeed(id, true);
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
        var list = new Listing_Standard(); list.Begin(new Rect(0, 0, area.width - 24, 20000)); list.Label("PF_Scope".Translate());
        if (ledger.RestoreFailed) list.Label("PF_Blocked".Translate());
        else
        {
            var own = ledger.Book.Oaths.SingleOrDefault(s => s.Subject == pawn.GetUniqueLoadID());
            if (own == null && list.ButtonText("PF_Swear".Translate()))
                Find.WindowStack.Add(new FloatMenu(ledger.Realms().Select(f => {
                    var ruler = f.leader;
                    return new FloatMenuOption(f.Name + " / " + ruler.LabelShort, () => Confirm("PF_Confirm".Translate(pawn.LabelShort, ruler.LabelShort), () => ledger.Swear(pawn, f, ruler)));
                }).ToList()));
            if (own != null && list.ButtonText("PF_Heir".Translate()))
                Find.WindowStack.Add(new FloatMenu(ledger.Candidates().Where(p => p != pawn).Select(p => new FloatMenuOption(p.LabelShort,
                    () => Confirm("PF_HeirConfirm".Translate(p.LabelShort), () => ledger.Heir(own.Id, pawn, p)))).ToList()));
            foreach (var s in ledger.Book.Oaths.Where(s => s.Subject == pawn.GetUniqueLoadID() || s.Heir == pawn.GetUniqueLoadID()))
            {
                list.Gap(); list.Label("PF_State".Translate(ledger.Label(s.Subject), ledger.Label(s.Liege), s.Heir == null ? "—" : ledger.Label(s.Heir)));
                if (list.ButtonText("PF_SubjectSuccession".Translate())) Confirm("PF_Recovery".Translate(), () => ledger.Succeed(s.Id, true));
                if (list.ButtonText("PF_LiegeSuccession".Translate())) Confirm("PF_Recovery".Translate(), () => ledger.Succeed(s.Id, false));
                foreach (var e in ledger.Book.Events.Where(e => e.Oath == s.Id)) list.Label("PF_Event".Translate(("PF_Action" + (int)e.Action).Translate(), e.Tick, ledger.Label(e.Actor), ledger.Label(e.Other)));
            }
        }
        height = list.CurHeight + 25; list.End(); Widgets.EndScrollView();
    }
}
