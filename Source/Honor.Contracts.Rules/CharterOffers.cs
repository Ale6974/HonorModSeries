namespace Honor.Contracts.Rules;

public enum CharterOfferDecision { Accepted, GoodwillTooLow, ValueTooLow, ValueTooHigh, GraceTooLong }
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
    /// <summary>Atomic proposal/NPC response on a copy; only explicit player signing can activate the contract.</summary>
    public static CharterOfferResult Propose(CharterBook original, string id, string player, string npc, bool playerSubject,
        CharterOfferDraft draft, int goodwill, long now, IEnumerable<TerritoryObservation> observations)
    {
        RuntimeCompat.NotNull(original); RuntimeCompat.NotNull(draft); RuntimeCompat.NotNull(observations);
        if (!original.Entity(player).Player || original.Entity(npc).Player) throw new ArgumentException("Wrong proposal scopes.");
        var assessment = draft.Assess(playerSubject, goodwill);
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
