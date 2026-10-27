using System.Text;
using System.Text.Json;
using Honor.Contracts.Rules;

internal static class LawfulExitPreparationTests
{
    public static int Run()
    {
        int count = 0;
        void Check(bool ok, string why) { count++; if (!ok) throw new Exception("Lawful exit: " + why); }
        void Reject(Action action, string why) { bool bad = false; try { action(); } catch (ArgumentException) { bad = true; } Check(bad, why); }
        string Hash(string s) => RuntimeCompat.Hex(RuntimeCompat.Sha256(Encoding.UTF8.GetBytes(s)));
        var b = new CharterBook(); b.Register(new("p", "player", true), 0); b.Register(new("n", "realm", false), 0); b.Register(new("town", "realm", false), 0);
        var obs = b.Entities.Select(e => new TerritoryObservation(e.Id, e.NativeOwner, true, e.Player, true)).ToArray();
        RecordedCharterTerms Terms(string l, string s) => new(l, s, 100, PoliticalWorld.Day, 0, TributeTransport.SubjectDelivery, 0);
        b.Offer("c", Terms("n", "p"), 0, PoliticalWorld.Day, obs); b.Sign("c", true, 0, obs); b.Sign("c", false, 0, obs);
        b.Offer("child", Terms("p", "town"), 0, PoliticalWorld.Day, obs); b.Sign("child", true, 0, obs); b.Sign("child", false, 0, obs);
        var politics = new PlayerAllegiance("player"); politics.Apply(new(AllegianceAction.BindLiege, 0, "realm", "c", Consent: true));
        var payments = new TributeLedger(); payments.Enable(b, "c", 0);
        payments.Prepare("paid", "c", 1, [new("silver", 100, 1)], PoliticalWorld.Day);
        payments.Commit("paid", [new("silver", 100, 1)], PoliticalWorld.Day);
        long now = PoliticalWorld.Day * 2 + 10;
        var decision = new NpcRealmDecisionEvidence("realm", "ruler", NpcDecisionRegime.Ordinary, true, true, true, true, true, true);
        var aid = new CharterAidPlan("refusal", "c", "realm", "ruler", "map", ["enemy"], ["colonist", "enemy"], now, Hash(b.Export()), Hash(politics.Export()), decision);
        var agreement = new CharterProtection().Agree(b, "c", decision, 75, true, 1);
        var protection = agreement.Refuse(b, aid, decision, true, "Honor_Vile", 1, 120, now);
        var transfers = new TerritoryTransferLedger(); string beforeBook = b.Export(), beforePayment = payments.Export(), beforePolitics = politics.Export();
        var p = LawfulExitPreparation.Prepare(b, payments, politics, transfers, protection, "refusal", now, obs);
        var after = CharterBook.Restore(p.AfterBook); var settled = TributeLedger.Restore(p.AfterPayments); var c = after.Charter("c");
        Check(c.Phase == RecordedCharterPhase.LawfullyEnded && c.LawfulExit.Request == "refusal" && c.LawfulExit.Proof == p.Proof && c.LawfulExit.Tick == now,
            "one original verified refusal stages an immediate distinct unilateral closure without cure delay");
        Check(!c.PlayerReleaseTick.HasValue && !c.NpcReleaseTick.HasValue && c.Terms == b.Charter("c").Terms &&
            c.PlayerSignedTick == b.Charter("c").PlayerSignedTick && c.NpcSignedTick == b.Charter("c").NpcSignedTick,
            "lawful closure never invents mutual release signatures or overwrites original consent/terms");
        Check(after.Charter("child") == b.Charter("child") && after.PlayerFollowers().Select(x => x.Id).SequenceEqual(b.PlayerFollowers().Select(x => x.Id)) &&
            after.Entities.SequenceEqual(b.Entities), "all subordinate contracts and recorded native identities remain intact");
        Check(settled.Account("c").ClosedTick == now && JsonSerializer.Serialize(settled.Receipts.Single()) == JsonSerializer.Serialize(payments.Receipts.Single()), "only the account cutoff changes; old delivered goods and receipt identities remain");
        var debt = TributePerformanceReview.ForCharter(after, settled, "c", PoliticalWorld.Day * 10);
        Check(debt.Due == 2 && debt.PaidWithinGrace == 1 && debt.OutstandingValue == 100 && debt.Overdue == 1, "unpaid pre-exit debt survives without new post-exit tribute");
        Reject(() => settled.Bill("c", 3), "future period is not billed after the actual unilateral cutoff");
        Check(b.Export() == beforeBook && payments.Export() == beforePayment && politics.Export() == beforePolitics && transfers.Transfers.Count == 0,
            "preparation never partially changes live book, goods, allegiance or physical journals");
        Check(LawfulExitPreparation.Restore(p.Export()).Export() == p.Export(), "original observations and paired closure/payment outputs round trip exactly");
        p.CheckBefore(b, payments, politics, transfers, protection, now, obs); Check(true, "fresh exact current-model/observation confirmation succeeds");
        p.VerifyPair(after, protection); Check(true, "termination stays paired with the exact original live NPC refusal and compact");
        Reject(() => p.CheckBefore(b, payments, politics, transfers, protection, now + 1, obs), "later confirmation needs a fresh review rather than reusing old time");
        Reject(() => p.CheckBefore(b, payments, politics, transfers, protection, now, obs.Where(o => o.Id != "town")), "missing subordinate cannot authorize closure");
        Reject(() => p.CheckBefore(b, payments, politics, transfers, protection, now, obs.Select(o => o.Id == "town" ? o with { NativeOwner = "foreign" } : o)), "changed actual child owner blocks closure");
        Reject(() => p.VerifyPair(after, agreement), "a standalone termination marker cannot replace the original live refusal archive");
        Reject(() => LawfulExitPreparation.Restore(p.Export().Replace(p.Proof, new string('F', 64))), "changed claim/marker proof rejected");
        Reject(() => LawfulExitPreparation.Restore(p.Export().Replace(JsonSerializer.Serialize(p.AfterPayments), JsonSerializer.Serialize(beforePayment))), "omitted debt cutoff or altered transaction output rejected");
        Reject(() => LawfulExitPreparation.Prepare(b, payments, politics, transfers, agreement, "refusal", now, obs), "no caller flag can replace the positive original refusal");
        var failed = agreement.Begin(b, aid, decision, true, now).Returned("refusal", false, [], now);
        Reject(() => LawfulExitPreparation.Prepare(b, payments, politics, transfers, failed, "refusal", now, obs), "support inability/failure is not an independence cause");
        var reserved = TributeLedger.Restore(beforePayment); reserved.Prepare("held", "c", 2, [new("silver2", 100, 1)], now);
        Reject(() => LawfulExitPreparation.Prepare(b, reserved, politics, transfers, protection, "refusal", now, obs), "reserved original goods must be resolved before staging closure");
        var changed = CharterBook.Restore(beforeBook); changed.Register(new("other", "realm", false), now);
        Reject(() => LawfulExitPreparation.Prepare(changed, payments, politics, transfers, protection, "refusal", now, obs), "this bounded path never fabricates lineage through a changed original book");
        Reject(() => LawfulExitPreparation.Prepare(b, payments, new PlayerAllegiance("player"), transfers, protection, "refusal", now, obs), "an unrelated current allegiance cannot exercise the old vassal right");
        var join = new AllegianceCommand(AllegianceAction.JoinUnion, now, "union", Consent: true, NoActiveFeudalContract: true, OldContractReleased: true);
        Reject(() => AllegianceChangeSource.Validate(after, politics.State, join, settled), "legacy mutual-release APIs cannot authorize the new unilateral migration mode");
        Check(AllegianceChangeSource.ReleasedMigrationSource(after, politics.State, "union", now) == null, "a legal-exit marker cannot silently enter old native migration authority");
        var raw = JsonSerializer.Deserialize<CharterBookSave>(p.AfterBook);
        foreach (var altered in new[] {
            c with { LawfulExit = null }, c with { Phase = RecordedCharterPhase.Active }, c with { NpcReleaseTick = now },
            c with { LawfulExit = c.LawfulExit with { Tick = now + 1 } } })
            Reject(() => CharterBook.Restore(JsonSerializer.Serialize(raw with { Charters = raw.Charters.Select(x => x.Id == c.Id ? altered : x).ToArray() })),
                "missing/misplaced/future unilateral evidence or fake NPC release cannot restore");
        var forged = CharterBook.Restore(JsonSerializer.Serialize(raw with { Charters = raw.Charters.Select(x => x.Id == c.Id ? x with { LawfulExit = x.LawfulExit with { Proof = new string('E', 64) } } : x).ToArray() }));
        Reject(() => p.VerifyPair(forged, protection), "structurally valid marker alone is not the paired original legal claim");
        var laterAid = protection.Begin(b, aid with { Id = "later", Tick = now + PoliticalWorld.Day }, decision, true, now + PoliticalWorld.Day);
        Reject(() => LawfulExitPreparation.Prepare(b, payments, politics, transfers, laterAid, "refusal", now + PoliticalWorld.Day, obs),
            "a newer unknown aid call must be resolved rather than overwritten by old refusal exercise");
        Check(!beforeBook.Contains("LawfulExit") && (int)RecordedCharterPhase.Replaced == 6 && (int)RecordedCharterPhase.LawfullyEnded == 7,
            "legacy null bytes and old enum values remain unchanged");
        return count;
    }
}
