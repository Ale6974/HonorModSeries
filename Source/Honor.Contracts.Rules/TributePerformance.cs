namespace Honor.Contracts.Rules;

public sealed record TributePerformance(int Due, int PaidWithinGrace, int PaidLate, int WithinGrace,
    int Overdue, int Reserved, decimal FulfilledValue, decimal OutstandingValue, bool Complete = true, int Accounts = 0);

/// <summary>Read actual opt-in obligations and receipts; never create bills, alter debts or assign culpability.</summary>
public static class TributePerformanceReview
{
    public static TributePerformance ForPair(CharterBook book, TributeLedger payments, string liege, string subject, long now)
    {
        RuntimeCompat.NotNull(book);
        if (book.Entity(liege).Player || !book.Entity(subject).Player) throw new ArgumentException("Only actual outgoing player tribute is measured.");
        return Measure(book, payments, now, a => a.Terms.Liege == liege && a.Terms.Subject == subject);
    }
    public static TributePerformance ForCharter(CharterBook book, TributeLedger payments, string charter, long now)
    { RuntimeCompat.NotNull(book); _ = book.Charter(charter); return Measure(book, payments, now, a => a.CharterId == charter); }
    private static TributePerformance Measure(CharterBook book, TributeLedger payments, long now, Func<TributeAccount, bool> include)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(payments);
        if (now < book.LastTick || now < payments.LastTick || now > int.MaxValue) throw new ArgumentException("Invalid performance review time.");
        int due = 0, paid = 0, late = 0, grace = 0, overdue = 0, reserved = 0;
        decimal fulfilled = 0, outstanding = 0; bool complete = true; int accounts = 0;
        var receipts = payments.Receipts.Where(r => r.Phase != TributeReceiptPhase.Cancelled)
            .ToLookup(r => (r.CharterId, r.Period));
        foreach (var account in payments.Accounts.Where(include))
        {
            accounts++;
            var charter = book.Charter(account.CharterId);
            if (charter.Terms != account.Terms || !book.Entity(charter.Terms.Subject).Player ||
                charter.Phase is not (RecordedCharterPhase.Active or RecordedCharterPhase.Released or RecordedCharterPhase.LawfullyEnded) ||
                !charter.PlayerSignedTick.HasValue || !charter.NpcSignedTick.HasValue ||
                account.StartedTick < Math.Max(charter.PlayerSignedTick.Value, charter.NpcSignedTick.Value))
                throw new ArgumentException("Payment history does not match actual signed terms.");
            long cutoff = now;
            if (charter.Phase is RecordedCharterPhase.Released or RecordedCharterPhase.LawfullyEnded)
            {
                long closed = charter.LawfulExit?.Tick ?? Math.Max(charter.PlayerReleaseTick.Value, charter.NpcReleaseTick.Value);
                if (closed < account.StartedTick || account.ClosedTick.HasValue && account.ClosedTick != closed)
                    throw new ArgumentException("Payment closure differs from actual release.");
                cutoff = Math.Min(now, closed); // Read-only closure even before the physical adapter synchronizes its account.
            }
            else if (account.ClosedTick.HasValue) throw new ArgumentException("An active charter cannot have a closed payment account.");
            long count = (cutoff - account.StartedTick) / account.Terms.IntervalTicks;
            if (count > TributeLedger.MaxPeriods) complete = false;
            for (int period = 1; period <= Math.Min(count, TributeLedger.MaxPeriods); period++)
            {
                var bill = payments.Bill(account.CharterId, period); due++;
                var evidence = receipts[(account.CharterId, period)].ToArray();
                if (evidence.Length > 1) throw new ArgumentException("Ambiguous obligation evidence.");
                var receipt = evidence.SingleOrDefault();
                if (receipt?.Phase == TributeReceiptPhase.Committed)
                {
                    if (receipt.ResolvedTick <= bill.GraceUntil) paid++; else late++;
                    fulfilled += bill.RequiredValue; // Performance is capped at the actual obligation, never overpayment.
                }
                else
                {
                    outstanding += bill.RequiredValue;
                    if (receipt?.Phase == TributeReceiptPhase.Prepared) reserved++;
                    else if (now > bill.GraceUntil) overdue++;
                    else grace++;
                }
            }
        }
        return new(due, paid, late, grace, overdue, reserved, fulfilled, outstanding, complete, accounts);
    }
}
