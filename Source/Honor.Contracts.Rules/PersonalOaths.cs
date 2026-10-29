using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum PersonalOathAction { Swear, DesignateHeir, SubjectSuccession, LiegeSuccession }
public sealed record PersonalOathEvent(PersonalOathAction Action, string Oath, string Actor, string Other, string Realm, long Tick,
    NpcRealmDecisionEvidence Decision = null);
public sealed record PersonalOathState(string Id, string OriginalSubject, string OriginalLiege, string Subject, string Liege, string Realm, string Heir);
public sealed record PersonalOathSave(int Version, PersonalOathEvent[] Events);

/// <summary>Append-only personal allegiance. Territorial accounts and duties are never copied, reset or inferred.</summary>
public sealed class PersonalOathBook
{
    private readonly List<PersonalOathEvent> events = [];
    private readonly Dictionary<string, PersonalOathState> states = new(StringComparer.Ordinal);
    public IReadOnlyList<PersonalOathState> Oaths => states.Values.ToArray();
    public IReadOnlyList<PersonalOathEvent> Events => events.ToArray();
    private static bool Key(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 120 && !s.Any(char.IsControl);
    public PersonalOathBook Append(PersonalOathEvent e)
    {
        var copy = Restore(Export()); copy.Apply(e);
        if (copy.Export().Length > 4000000) throw new ArgumentException("Personal oath history is full.");
        return copy;
    }
    private void Apply(PersonalOathEvent e)
    {
        if (e == null || !RuntimeCompat.IsDefined(e.Action) || !Key(e.Oath) || !Key(e.Actor) || !Key(e.Other) || !Key(e.Realm) ||
            e.Actor == e.Other || e.Tick < 0 || e.Tick > int.MaxValue || events.Count >= 4096 || events.Count > 0 && e.Tick < events[events.Count - 1].Tick)
            throw new ArgumentException("Invalid personal oath event.");
        if (e.Action != PersonalOathAction.DesignateHeir)
        {
            NpcRealmDecision.Consent(e.Decision);
            if (e.Decision.Realm != e.Realm || e.Decision.Representative !=
                (e.Action == PersonalOathAction.SubjectSuccession && states.TryGetValue(e.Oath, out var current) ? current.Liege : e.Other))
                throw new ArgumentException("Original NPC decision does not match this personal oath.");
        }
        else if (e.Decision != null) throw new ArgumentException("Heir designation cannot fabricate an NPC decision.");
        if (e.Action == PersonalOathAction.Swear)
        {
            if (states.ContainsKey(e.Oath) || states.Values.Any(s => s.Subject == e.Actor)) throw new ArgumentException("Duplicate personal allegiance.");
            states.Add(e.Oath, new(e.Oath, e.Actor, e.Other, e.Actor, e.Other, e.Realm, null));
        }
        else
        {
            if (!states.TryGetValue(e.Oath, out var s) || e.Realm != s.Realm) throw new ArgumentException("Missing original oath.");
            switch (e.Action)
            {
                case PersonalOathAction.DesignateHeir:
                    if (e.Actor != s.Subject || e.Other == s.Liege || states.Values.Any(x => x.Subject == e.Other)) throw new ArgumentException("Invalid heir.");
                    states[e.Oath] = s with { Heir = e.Other }; break;
                case PersonalOathAction.SubjectSuccession:
                    if (e.Actor != s.Subject || e.Other != s.Heir || states.Values.Any(x => x.Subject == e.Other)) throw new ArgumentException("Only the original designated heir may inherit.");
                    states[e.Oath] = s with { Subject = e.Other, Heir = null }; break;
                case PersonalOathAction.LiegeSuccession:
                    if (e.Actor != s.Liege || e.Other == s.Subject || e.Other == s.Heir) throw new ArgumentException("Invalid liege successor.");
                    states[e.Oath] = s with { Liege = e.Other }; break;
            }
        }
        foreach (var s in states.Values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal); var actor = s.Subject; int depth = 0;
            while (true)
            {
                if (!seen.Add(actor) || ++depth > 4) throw new ArgumentException("Personal allegiance exceeds four levels or contains a cycle.");
                var next = states.Values.SingleOrDefault(x => x.Subject == actor);
                if (next == null) break;
                actor = next.Liege;
            }
        }
        events.Add(e);
    }
    public string Export() => JsonSerializer.Serialize(new PersonalOathSave(1, events.ToArray()));
    public static PersonalOathBook Restore(string json)
    {
        if (json == null || json.Length > 4000000) throw new ArgumentException("Invalid personal oath snapshot.");
        var save = JsonSerializer.Deserialize<PersonalOathSave>(json);
        if (save?.Version != 1 || save.Events == null || save.Events.Length > 4096) throw new ArgumentException("Invalid personal oath history.");
        var book = new PersonalOathBook(); foreach (var e in save.Events) book.Apply(e); return book;
    }
}
