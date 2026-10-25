namespace Honor.Contracts.Rules;

public sealed record LiegeInvitationCandidate(string Identity, int Goodwill);

/// <summary>Candidate ranking only. The adapter owns current representation and actual consent.</summary>
public static class LiegeInvitations
{
    public const long Interval = PoliticalWorld.Day * 7;
    public const long ContactCooldown = PoliticalWorld.Day * 30;
    public static IReadOnlyList<string> Rank(CharterBook book, string player,
        IEnumerable<LiegeInvitationCandidate> candidates, long now)
    {
        RuntimeCompat.NotNull(book); RuntimeCompat.NotNull(candidates);
        if (!book.Entity(player).Player || now < book.LastTick || now > int.MaxValue)
            throw new ArgumentException("Invalid invitation player or clock.");
        var options = candidates.ToArray();
        if (options.Any(c => c == null || string.IsNullOrWhiteSpace(c.Identity) || c.Goodwill is < -100 or > 100) ||
            options.Select(c => c.Identity).Distinct(StringComparer.Ordinal).Count() != options.Length)
            throw new ArgumentException("Invalid or duplicate invitation candidate.");
        foreach (var c in options) if (book.Entity(c.Identity).Player) throw new ArgumentException("An NPC must send the invitation.");
        var history = book.Charters.Where(c => c.Terms.Subject == player).ToArray();
        if (history.Count(c => book.Phase(c, now) == RecordedCharterPhase.Offered) >= 8) return Array.Empty<string>();
        return Array.AsReadOnly(options.Where(c => c.Goodwill >= 0 &&
                !history.Any(h => h.Terms.Liege == c.Identity && (h.Phase == RecordedCharterPhase.Active ||
                    book.Phase(h, now) == RecordedCharterPhase.Offered || now - h.OfferedTick < ContactCooldown)))
            .OrderBy(c => history.Where(h => h.Terms.Liege == c.Identity).Select(h => h.OfferedTick).DefaultIfEmpty(-1).Max())
            .ThenByDescending(c => c.Goodwill).ThenBy(c => c.Identity, StringComparer.Ordinal)
            .Select(c => c.Identity).ToArray());
    }
}
