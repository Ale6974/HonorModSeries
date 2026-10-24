using System.Text.Json;
using Honor.Contracts.Rules;

internal static class PlayerAllegianceTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
        void Reject(PlayerAllegiance model, AllegianceCommand command, string name)
        {
            string before = model.Export(); bool rejected = false;
            try { model.Apply(command); } catch (ArgumentException) { rejected = true; }
            Check(rejected && before == model.Export(), name + ": reject without mutation");
        }
        var join = new AllegianceCommand(AllegianceAction.JoinUnion, 10, "union", Consent: true, NoActiveFeudalContract: true);
        var bind = new AllegianceCommand(AllegianceAction.BindLiege, 20, "liege", "bond", Consent: true);
        var model = new PlayerAllegiance("player");
        Check(model.State.Mode == PlayerAllegianceMode.Independent, "no implicit membership");
        Reject(model, join with { Consent = false }, "player must consent");
        Reject(model, join with { NoActiveFeudalContract = false }, "union cannot bypass current contract");
        Reject(model, join with { Authority = "player" }, "player cannot be own union");
        Reject(model, join with { Authority = "bad\nkey" }, "stable identity only");
        Reject(model, bind with { Contract = null }, "binding needs source contract");
        Reject(model, bind with { Consent = false }, "binding needs player consent");
        Reject(model, new(AllegianceAction.Suspend, 0), "independent has no diplomacy to suspend");
        model.Apply(join);
        Reject(model, join, "duplicate entry cannot reset notice or suspension");
        Reject(model, bind, "union exit needs notice");
        Reject(model, new(AllegianceAction.UnionExitNotice, 11, NoticeTicks: PoliticalWorld.Day - 1), "notice lower bound");
        Reject(model, new(AllegianceAction.UnionExitNotice, 11, NoticeTicks: PoliticalWorld.Day * 30 + 1), "notice upper bound");
        model.Apply(new(AllegianceAction.UnionExitNotice, 11, NoticeTicks: PoliticalWorld.Day * 3));
        long due = model.State.UnionNoticeUntil.Value;
        Check(due == 11 + PoliticalWorld.Day * 3, "notice uses actual elapsed ticks");
        Reject(model, new(AllegianceAction.UnionExitNotice, 12, NoticeTicks: PoliticalWorld.Day), "notice cannot be shortened");
        Reject(model, bind with { Tick = due - 1, UnionDuesSettled = true }, "no transfer before notice expires");
        Reject(model, bind with { Tick = due }, "pending obligations block exit");
        var reloaded = PlayerAllegiance.Restore(model.Export());
        Check(reloaded.State == model.State && reloaded.Export() == model.Export(), "notice survives replay");
        model.Apply(new(AllegianceAction.Suspend, 12));
        Reject(model, bind with { Tick = due, UnionDuesSettled = true }, "suspension cannot be bypassed by notice");
        Reject(model, join with { Tick = due }, "duplicate union entry cannot clear suspension");
        Reject(model, new(AllegianceAction.Reaffirm, 12, "other", Consent: true), "wrong authority cannot reaffirm");
        Reject(model, new(AllegianceAction.Reaffirm, 12, "union"), "fresh consent required");
        Check(PlayerAllegiance.Restore(model.Export()).State.Suspended, "suspension survives save");
        model.Apply(new(AllegianceAction.Reaffirm, 12, "union", Consent: true));
        Check(!model.State.Suspended && model.State.UnionNoticeUntil == due, "reaffirm does not reset exit clock");
        model.Apply(bind with { Tick = due, UnionDuesSettled = true });
        Check(model.State == new PlayerAllegianceState(PlayerAllegianceMode.Vassal, "liege", "bond"), "inclusive deadline permits signed transfer");
        Check(model.NativePlayer == "player", "native player identity preserved through transfer");
        Reject(model, join with { Tick = due }, "old feudal tie requires release");
        Reject(model, bind with { Tick = due, Authority = "new", Contract = "new-bond" }, "cannot install competing liege");
        model.Apply(new(AllegianceAction.Suspend, due));
        Reject(model, bind with { Tick = due, OldContractReleased = true }, "same source cannot bypass suspension");
        model.Apply(bind with { Tick = due, Authority = "new", Contract = "new-bond", OldContractReleased = true });
        Check(model.State.Authority == "new" && !model.State.Suspended, "new released and consented contract replaces old tie");
        model.Apply(join with { Tick = due, OldContractReleased = true });
        Check(model.NativePlayer == "player" && model.State.Contract == null, "released vassal can join union without changing owner");
        Reject(model, new((AllegianceAction)999, due), "unknown action");
        Reject(model, new(AllegianceAction.Suspend, due - 1), "time reversal");
        Reject(model, new(AllegianceAction.Suspend, (long)int.MaxValue + 1), "engine clock bound");
        var overflow = new PlayerAllegiance("player");
        overflow.Apply(join with { Tick = int.MaxValue - 10 });
        Reject(overflow, new(AllegianceAction.UnionExitNotice, int.MaxValue - 10, NoticeTicks: PoliticalWorld.Day), "notice overflow");
        var saved = JsonSerializer.Deserialize<PlayerAllegianceSave>(model.Export());
        void Corrupt(PlayerAllegianceSave value, string name)
        {
            bool rejected = false;
            try { PlayerAllegiance.Restore(JsonSerializer.Serialize(value)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        Corrupt(saved with { Version = 99 }, "future schema");
        Corrupt(saved with { NativePlayer = "union" }, "self authority on replay");
        Corrupt(saved with { Commands = null }, "missing journal");
        Corrupt(saved with { Commands = [null] }, "null command");
        Corrupt(saved with { Commands = [bind with { Tick = -1 }] }, "negative time");
        Corrupt(saved with { Commands = [join with { Consent = false }] }, "replay revalidates consent");
        Check(PlayerAllegiance.Restore(model.Export()).State == model.State, "complex transitions roundtrip");

        var source = new List<SharedRelation> { new("player", 100, SharedRelationKind.Ally), new("union", 100, SharedRelationKind.Ally), new("other", -80, SharedRelationKind.Hostile) };
        var plan = SharedDiplomacyPlan.Build("player", "union", source);
        source[2] = new("other", 90, SharedRelationKind.Ally); source.Clear();
        Check(plan.Count == 1 && plan[0] == new SharedRelation("other", -80, SharedRelationKind.Hostile), "snapshot independent of later source changes; internal endpoints excluded");
        bool readOnly = false;
        try { ((IList<SharedRelation>)plan)[0] = new("other", 0, SharedRelationKind.Neutral); } catch (NotSupportedException) { readOnly = true; }
        Check(readOnly, "plan cannot mutate game source or reviewed snapshot");
        foreach (var kind in RuntimeCompat.EnumValues<SharedRelationKind>())
        foreach (int goodwill in new[] { -100, -75, -1, 0, 1, 75, 100 })
            Check(SharedDiplomacyPlan.Build("p", "r", [new("x", goodwill, kind)])[0] == new SharedRelation("x", goodwill, kind), "base goodwill and source hysteresis kind preserved independently");
        void BadPlan(string player, string root, SharedRelation[] relations, string name)
        {
            bool rejected = false;
            try { SharedDiplomacyPlan.Build(player, root, relations); } catch (ArgumentException) { rejected = true; }
            Check(rejected, name);
        }
        BadPlan("p", "p", [], "same endpoints");
        BadPlan("", "r", [], "missing player");
        BadPlan("p", "r", [new("x", 0, SharedRelationKind.Neutral), new("x", 10, SharedRelationKind.Ally)], "duplicate factions");
        BadPlan("p", "r", [new("x", 101, SharedRelationKind.Ally)], "upper goodwill bound");
        BadPlan("p", "r", [new("x", -101, SharedRelationKind.Hostile)], "lower goodwill bound");
        BadPlan("p", "r", [new("x", 0, (SharedRelationKind)99)], "unknown relation");
        BadPlan("p", "r", [null], "missing relation");
        Check(SharedDiplomacyPlan.Build("p", "r", []).Count == 0, "empty external world");
        return count;
    }
}
