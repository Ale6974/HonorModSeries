namespace Honor.Contracts.Rules;

public enum CharterOfferDecision { Accepted, GoodwillTooLow, ValueTooLow, ValueTooHigh, GraceTooLong, PriorTributeOverdue, HistoryIncomplete }
public sealed record CharterOfferAssessment(CharterOfferDecision Decision, int MinimumGoodwill, decimal DailyValue, decimal Benchmark)
{
    public bool Accepted => Decision == CharterOfferDecision.Accepted;
}

/// <summary>Editable proposal only. Existing signed terms and payment accounts are never amended in place.</summary>
public sealed record CharterOfferDraft(decimal TributeValue = 100, int IntervalDays = 5, int GraceDays = 1, int AllowancePercent = 10)
{
    public const decimal DailyBenchmark = 18m; // Net value of the existing 100 / 5-day / 10% default proposal.
    private void Validate()
    {
        if (TributeValue is < 1 or > 10000 || IntervalDays is < 1 or > 60 || GraceDays is < 0 or > 30 || AllowancePercent is < 0 or > 50)
            throw new ArgumentException("Unsupported new charter proposal values.");
    }
    public decimal RequiredValue { get { Validate(); return TributeValue * (1 - AllowancePercent / 100m); } }
    public RecordedCharterTerms Bind(string liege, string subject)
    {
        Validate();
        if (string.IsNullOrWhiteSpace(liege) || string.IsNullOrWhiteSpace(subject) || liege == subject) throw new ArgumentException("Invalid proposal parties.");
        return new(liege, subject, TributeValue, PoliticalWorld.Day * IntervalDays, PoliticalWorld.Day * GraceDays,
            TributeTransport.SubjectDelivery, AllowancePercent / 100m);
    }
    public CharterOfferAssessment Assess(bool playerSubject, int goodwill)
    {
        Validate(); if (goodwill is < -100 or > 100) throw new ArgumentException("Invalid actual goodwill.");
        decimal daily = RequiredValue / IntervalDays; int minimum = playerSubject ? 0 : 50;
        var decision = goodwill < minimum ? CharterOfferDecision.GoodwillTooLow :
            playerSubject && GraceDays > IntervalDays ? CharterOfferDecision.GraceTooLong :
            playerSubject && daily < DailyBenchmark ? CharterOfferDecision.ValueTooLow :
            !playerSubject && daily > DailyBenchmark ? CharterOfferDecision.ValueTooHigh : CharterOfferDecision.Accepted;
        return new(decision, minimum, daily, DailyBenchmark);
    }
}

public sealed record CharterOfferResult(CharterBook Book, CharterOfferAssessment Assessment);
public static class CharterOffers
{
    public static CharterOfferAssessment Assess(CharterBook book, TributeLedger payments, string player, string npc,
        bool playerSubject, CharterOfferDraft draft, int goodwill, long now)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(draft);
        if (!book.Entity(player).Player || book.Entity(npc).Player || now < book.LastTick || now > int.MaxValue || payments != null && now < payments.LastTick)
            throw new ArgumentException("Invalid proposal parties or evidence clock.");
        var assessment = draft.Assess(playerSubject, goodwill);
        if (!assessment.Accepted || !playerSubject || payments == null) return assessment;
        var history = TributePerformanceReview.ForPair(book, payments, npc, player, now);
        return AssessHistory(assessment, history);
    }
    public static CharterOfferAssessment AssessHistory(CharterOfferAssessment assessment, TributePerformance history)
    {
        RuntimeCompat.NotNull(assessment); RuntimeCompat.NotNull(history);
        if (!assessment.Accepted) return assessment;
        return assessment with { Decision = !history.Complete ? CharterOfferDecision.HistoryIncomplete :
            history.Overdue > 0 ? CharterOfferDecision.PriorTributeOverdue : CharterOfferDecision.Accepted };
    }
    public static CharterOfferDraft Draft(RecordedCharterTerms terms)
    {
        RuntimeCompat.NotNull(terms);
        if (terms.Transport != TributeTransport.SubjectDelivery || terms.IntervalTicks % PoliticalWorld.Day != 0 ||
            terms.GraceTicks % PoliticalWorld.Day != 0 || terms.TransportRate * 100 != decimal.Truncate(terms.TransportRate * 100))
            throw new ArgumentException("These terms need their original consent policy, not an inferred editable draft.");
        var result = new CharterOfferDraft(terms.TributeValue, checked((int)(terms.IntervalTicks / PoliticalWorld.Day)),
            checked((int)(terms.GraceTicks / PoliticalWorld.Day)), checked((int)(terms.TransportRate * 100)));
        _ = result.RequiredValue; return result;
    }
    /// <summary>Fresh NPC response plus player consent on a copy. An old offer is not irrevocable NPC approval.</summary>
    public static CharterOfferResult Confirm(CharterBook original, string id, int goodwill, long now,
        IEnumerable<TerritoryObservation> observations, TributeLedger payments)
    {
        RuntimeCompat.NotNull(original); RuntimeCompat.NotNull(payments);
        RuntimeCompat.NotNull(observations);
        if (payments.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared)) throw new ArgumentException("Resolve reserved deliveries before final consent.");
        if (now < original.LastTick || now > int.MaxValue) throw new ArgumentException("Invalid final consent time.");
        var c = original.Charter(id);
        if (c.NpcAuthority != null || original.Phase(c, now) != RecordedCharterPhase.Offered || !c.NpcSignedTick.HasValue || c.PlayerSignedTick.HasValue)
            throw new ArgumentException("A current NPC-signed player offer is required.");
        bool subject = original.Entity(c.Terms.Subject).Player;
        string player = subject ? c.Terms.Subject : c.Terms.Liege, npc = subject ? c.Terms.Liege : c.Terms.Subject;
        var response = Assess(original, payments, player, npc, subject, Draft(c.Terms), goodwill, now);
        var copy = CharterBook.Restore(original.Export());
        if (response.Accepted) copy.Sign(id, true, now, observations);
        return new(copy, response);
    }
    /// <summary>Atomic proposal/NPC response on a copy; only explicit player signing can activate the contract.</summary>
    public static CharterOfferResult Propose(CharterBook original, string id, string player, string npc, bool playerSubject,
        CharterOfferDraft draft, int goodwill, long now, IEnumerable<TerritoryObservation> observations, TributeLedger payments = null)
    {
        RuntimeCompat.NotNull(original); RuntimeCompat.NotNull(draft); RuntimeCompat.NotNull(observations);
        if (payments?.Receipts.Any(r => r.Phase == TributeReceiptPhase.Prepared) == true) throw new ArgumentException("Resolve reserved deliveries before a new proposal.");
        if (!original.Entity(player).Player || original.Entity(npc).Player) throw new ArgumentException("Wrong proposal scopes.");
        var assessment = Assess(original, payments, player, npc, playerSubject, draft, goodwill, now);
        var book = CharterBook.Restore(original.Export());
        var fresh = observations.ToArray();
        var terms = draft.Bind(playerSubject ? npc : player, playerSubject ? player : npc);
        if (playerSubject) book.OfferAlternative(id, terms, now, PoliticalWorld.Day * 3, fresh);
        else book.Offer(id, terms, now, PoliticalWorld.Day * 3, fresh);
        if (assessment.Accepted) book.Sign(id, false, now, fresh);
        else book.Decline(id, now);
        return new(book, assessment);
    }
}
