using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honor.Contracts.Rules;

public enum PersonalOathAction { Swear, DesignateHeir, SubjectSuccession, LiegeSuccession, MutualRelease, DeathObserved }
/// <summary>Consent only to ending this person's own pledge, never a realm or territorial decision.</summary>
public sealed record PersonalReleaseDecision(string Actor, string Player, string Realm, long Tick, int Policy,
    bool RegisteredOrdinaryRealm, bool OriginalActorCapable, bool Willing, bool Independent)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string CurrentRealm { get; init; }
}
public sealed record PersonalOathEvent(PersonalOathAction Action, string Oath, string Actor, string Other, string Realm, long Tick,
    NpcRealmDecisionEvidence Decision = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PlayerLiege { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DeathWitnessV1 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PersonalReleaseDecision PersonalRelease { get; init; }
}
public sealed record PersonalOathState(string Id, string OriginalSubject, string OriginalLiege, string Subject, string Liege, string Realm, string Heir)
{
    public bool PlayerLiege { get; init; }
    public long? EndedTick { get; init; }
    public long? SubjectDeathTick { get; init; }
    public long? LiegeDeathTick { get; init; }
}
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
        if (e.DeathWitnessV1 && e.Action is not (PersonalOathAction.SubjectSuccession or PersonalOathAction.LiegeSuccession))
            throw new ArgumentException("A death witness marker belongs only to succession.");
        if (e.PersonalRelease != null)
        {
            var d = e.PersonalRelease;
            if (e.Action != PersonalOathAction.MutualRelease || e.Decision != null || d.Actor != e.Other || d.Player != e.Actor ||
                d.Realm != e.Realm || d.Tick != e.Tick ||
                (d.Policy == 1 ? d.CurrentRealm != null : d.Policy != 2 || !Key(d.CurrentRealm) || d.CurrentRealm == e.Realm) ||
                !d.RegisteredOrdinaryRealm || !d.OriginalActorCapable || !d.Willing || !d.Independent)
                throw new ArgumentException("Personal release does not bind this exact original person's own decision.");
        }
        else if (e.Action is not (PersonalOathAction.DesignateHeir or PersonalOathAction.DeathObserved))
        {
            NpcRealmDecision.Consent(e.Decision);
            string representative;
            if (e.Action == PersonalOathAction.Swear) representative = e.PlayerLiege ? e.Actor : e.Other;
            else
            {
                if (!states.TryGetValue(e.Oath, out var current)) throw new ArgumentException("Missing original oath.");
                representative = e.PlayerLiege
                    ? e.Action == PersonalOathAction.SubjectSuccession ? e.Other : current.Subject
                    : e.Action == PersonalOathAction.SubjectSuccession ? current.Liege : e.Other;
            }
            if (e.Decision.Realm != e.Realm || e.Decision.Representative != representative)
                throw new ArgumentException("Original NPC decision does not match this personal oath.");
        }
        else if (e.Decision != null) throw new ArgumentException("Designation or death observation cannot fabricate an NPC decision.");
        if (e.Action == PersonalOathAction.Swear)
        {
            if (states.ContainsKey(e.Oath) || states.Values.Any(s => !s.EndedTick.HasValue && s.Subject == e.Actor)) throw new ArgumentException("Duplicate personal allegiance.");
            states.Add(e.Oath, new(e.Oath, e.Actor, e.Other, e.Actor, e.Other, e.Realm, null) { PlayerLiege = e.PlayerLiege });
        }
        else
        {
            if (!states.TryGetValue(e.Oath, out var s) || s.EndedTick.HasValue || e.Realm != s.Realm || e.PlayerLiege != s.PlayerLiege) throw new ArgumentException("Missing original oath, terminated oath or changed role.");
            switch (e.Action)
            {
                case PersonalOathAction.DesignateHeir:
                    if (e.Actor != (s.PlayerLiege ? s.Liege : s.Subject) || e.Other == s.Liege || e.Other == s.Subject ||
                        !s.PlayerLiege && states.Values.Any(x => !x.EndedTick.HasValue && x.Subject == e.Other)) throw new ArgumentException("Invalid heir.");
                    states[e.Oath] = s with { Heir = e.Other }; break;
                case PersonalOathAction.SubjectSuccession:
                    if (e.Actor != s.Subject || e.DeathWitnessV1 && !s.SubjectDeathTick.HasValue || !s.PlayerLiege && e.Other != s.Heir || states.Values.Any(x => !x.EndedTick.HasValue && x.Subject == e.Other)) throw new ArgumentException("Invalid subject successor.");
                    states[e.Oath] = s with { Subject = e.Other, Heir = s.PlayerLiege ? s.Heir : null, SubjectDeathTick = null }; break;
                case PersonalOathAction.LiegeSuccession:
                    if (e.Actor != s.Liege || e.DeathWitnessV1 && !s.LiegeDeathTick.HasValue || e.Other == s.Subject || (s.PlayerLiege ? e.Other != s.Heir : e.Other == s.Heir)) throw new ArgumentException("Invalid liege successor.");
                    states[e.Oath] = s with { Liege = e.Other, Heir = s.PlayerLiege ? null : s.Heir, LiegeDeathTick = null }; break;
                case PersonalOathAction.MutualRelease:
                    if (e.Actor != (s.PlayerLiege ? s.Liege : s.Subject) || e.Other != (s.PlayerLiege ? s.Subject : s.Liege)) throw new ArgumentException("Both original current parties must agree to release.");
                    states[e.Oath] = s with { EndedTick = e.Tick }; break;
                case PersonalOathAction.DeathObserved:
                    if (e.Actor == s.Subject && e.Other == s.Liege && !s.SubjectDeathTick.HasValue)
                        states[e.Oath] = s with { SubjectDeathTick = e.Tick };
                    else if (e.Actor == s.Liege && e.Other == s.Subject && !s.LiegeDeathTick.HasValue)
                        states[e.Oath] = s with { LiegeDeathTick = e.Tick };
                    else throw new ArgumentException("Death observation must bind an original current party exactly once.");
                    break;
            }
        }
        foreach (var s in states.Values.Where(x => !x.EndedTick.HasValue))
        {
            var seen = new HashSet<string>(StringComparer.Ordinal); var actor = s.Subject; int depth = 0;
            while (true)
            {
                if (!seen.Add(actor) || ++depth > 4) throw new ArgumentException("Personal allegiance exceeds four levels or contains a cycle.");
                var next = states.Values.SingleOrDefault(x => !x.EndedTick.HasValue && x.Subject == actor);
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
