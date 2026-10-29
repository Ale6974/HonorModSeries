using Honor.Contracts.Rules;
using System.Text.Json;

internal static class InitialCharterMandateTests
{
    public static int Run()
    {
        int n = 0;
        void Check(bool x) { n++; if (!x) throw new Exception("Initial charter mandate regression."); }
        void Reject(Action f) { bool rejected = false; try { f(); } catch (ArgumentException) { rejected = true; } Check(rejected); }
        foreach (var regime in new[] { NpcDecisionRegime.Union, NpcDecisionRegime.Royal })
        foreach (bool subject in new[] { true, false })
        {
            var b = new CharterBook(); b.Register(new("player", "native-player", true), 0); b.Register(new("npc", "native-npc", false), 0);
            var obs = b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
            var payments = new TributeLedger(); var history = new InitialCharterMandates();
            var d = new NpcRealmDecisionEvidence("native-npc", "ruler", regime, true, true, true, true, true, true);
            InitialCharterMandate Witness(CharterBook before, CharterBook after, long tick, bool final)
            {
                var r = new InitialCharterMandate("offer", before.Export(), after.Export(), payments.Export(), obs, tick, final, d, null);
                return r with { Mandate = new(d.Realm, d.Representative, regime, InitialCharterMandates.Scope(r), tick, 1, 70,
                    regime == NpcDecisionRegime.Union ? [new("seat", true)] : [], regime == NpcDecisionRegime.Royal ? "Stellarch" : null, regime == NpcDecisionRegime.Royal ? 60 : 0) };
            }
            var offer = CharterOffers.Propose(b, "offer", "player", "npc", subject, new(), 70, 0, obs, payments);
            var proposed = Witness(b, offer.Book, 0, false); history = history.Append(proposed); history.ValidateBook(offer.Book);
            var confirmed = CharterOffers.Confirm(offer.Book, "offer", 70, 1, obs, payments);
            var signed = Witness(offer.Book, confirmed.Book, 1, true); history = history.Append(signed); history.ValidateBook(confirmed.Book);
            Check(history.Records.Count == 2 && confirmed.Book.Charter("offer").PlayerSignedTick == 1);
            Check(InitialCharterMandates.Restore(history.Export()).Export() == history.Export());
            var acceptances = new CharterAcceptances().Record(offer.Book, confirmed.Book, "offer", d.Realm, d.Representative, "Realm", "Ruler", 1);
            history.ValidateAcceptances(acceptances);
            Reject(() => history.ValidateAcceptances(new CharterAcceptances()));
            Reject(() => new InitialCharterMandates().Append(signed with { Mandate = proposed.Mandate }));
            Reject(() => new InitialCharterMandates().Append(signed with { Decision = d with { Representative = "replacement" } }));
            Reject(() => new InitialCharterMandates().Append(signed with { Mandate = null }));
            Reject(() => new InitialCharterMandates().Append(signed with { Final = false }));
            Reject(() => history.Append(signed));
            var save = JsonSerializer.Deserialize<InitialCharterMandateSave>(history.Export());
            Reject(() => InitialCharterMandates.Restore(JsonSerializer.Serialize(save with { Records = save.Records.Select(r => r.Final ? r with { Tick = 2 } : r).ToArray() })));
            var raw = history.Export(); if (regime == NpcDecisionRegime.Union) history.Records[0].Mandate.Ballots[0] = new("other", false); Check(history.Export() == raw);
        }
        return n;
    }
}
