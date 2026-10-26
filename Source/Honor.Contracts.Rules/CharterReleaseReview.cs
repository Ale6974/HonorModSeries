namespace Honor.Contracts.Rules;

public sealed record CharterReleaseReview(string Charter, long Tick, NpcRealmDecisionEvidence Decision,
    string Before, string PaymentsBefore, string Transfers, string After, string PaymentsAfter);

/// <summary>Stage bilateral current-authority release and actual tribute closure together; no native migration.</summary>
public static class CharterReleases
{
    public static CharterReleaseReview Review(CharterBook book, TributeLedger payments, TerritoryTransferLedger transfers,
        string id, NpcRealmDecisionEvidence decision, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(payments); RuntimeCompat.NotNull(transfers);
        RuntimeCompat.NotNull(observations); NpcRealmDecision.Consent(decision);
        var c = book.Charter(id); var subject = book.Entity(c.Terms.Subject); var liege = book.Entity(c.Terms.Liege);
        var npc = subject.Player ? liege : subject;
        if (subject.Player == liege.Player || c.NpcAuthority != null || c.Phase != RecordedCharterPhase.Active || c.NpcReleaseTick.HasValue ||
            now < book.LastTick || now < payments.LastTick || now < transfers.LastTick || now > int.MaxValue ||
            !transfers.VerifiedOwner(npc.Id, npc.NativeOwner, decision.Realm) ||
            payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) ||
            transfers.Transfers.Any(t => t.Phase == TerritoryTransferPhase.Prepared) ||
            !subject.Player && book.PlayerFollowers().Any(f => book.IsBelow(f.Terms.Subject, subject.Id)))
            throw new ArgumentException("Review a current ordinary bilateral release with resolved custody and no NPC subtree exit.");
        var actual = observations.ToArray(); book.ValidateCurrent(id, actual);
        var next = CharterBook.Restore(book.Export()); var paid = TributeLedger.Restore(payments.Export());
        if (!c.PlayerReleaseTick.HasValue) next.ConsentToRelease(id, true, now, actual);
        next.ConsentToRelease(id, false, now, actual);
        if (paid.Accounts.Any(a => a.CharterId == id)) paid.SynchronizeClosure(next, id, now);
        return new(id, now, decision, book.Export(), payments.Export(), transfers.Export(), next.Export(), paid.Export());
    }
}
