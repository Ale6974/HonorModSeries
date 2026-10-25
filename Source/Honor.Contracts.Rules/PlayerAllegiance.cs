using System.Text.Json;

namespace Honor.Contracts.Rules;

public enum PlayerAllegianceMode { Independent, Union, Vassal }
public enum AllegianceAction { JoinUnion, UnionExitNotice, BindLiege, Suspend, Reaffirm }
public sealed record AllegianceCommand(AllegianceAction Action, long Tick, string Authority = null, string Contract = null,
    bool Consent = false, bool NoActiveFeudalContract = false, bool OldContractReleased = false,
    bool UnionDuesSettled = false, long NoticeTicks = 0);
public sealed record PlayerAllegianceState(PlayerAllegianceMode Mode = PlayerAllegianceMode.Independent, string Authority = null,
    string Contract = null, bool Suspended = false, long? UnionNoticeUntil = null);
public sealed record PlayerAllegianceSave(int Version, string NativePlayer, AllegianceCommand[] Commands);

/// <summary>Verified inputs come from the live adapter. Native player ownership is never an output.</summary>
public sealed class PlayerAllegiance
{
    public const int Version = 1;
    public string NativePlayer { get; }
    public PlayerAllegianceState State { get; private set; } = new();
    public long LastTick { get; private set; }
    private readonly List<AllegianceCommand> commands = [];
    public PlayerAllegiance(string nativePlayer) { Key(nativePlayer); NativePlayer = nativePlayer; }
    private static void Require(bool value, string reason) { if (!value) throw new ArgumentException(reason); }
    private static void Key(string id) => Require(!string.IsNullOrWhiteSpace(id) && id.Length <= 120 && !id.Any(char.IsControl), "Invalid allegiance identity.");
    public void Apply(AllegianceCommand command)
    {
        RuntimeCompat.NotNull(command);
        Require(RuntimeCompat.IsDefined(command.Action) && command.Tick >= LastTick && command.Tick <= int.MaxValue && commands.Count < 1000,
            "Invalid allegiance command, clock or capacity.");
        var next = State;
        switch (command.Action)
        {
            case AllegianceAction.JoinUnion:
                Key(command.Authority);
                Require(command.Authority != NativePlayer && command.Consent && command.NoActiveFeudalContract &&
                    (State.Mode != PlayerAllegianceMode.Vassal || command.OldContractReleased) &&
                    State.Mode != PlayerAllegianceMode.Union, "Union entry requires current admission and released prior ties.");
                next = new(PlayerAllegianceMode.Union, command.Authority); break;
            case AllegianceAction.UnionExitNotice:
                Require(State.Mode == PlayerAllegianceMode.Union && !State.Suspended && !State.UnionNoticeUntil.HasValue &&
                    command.NoticeTicks is >= PoliticalWorld.Day and <= PoliticalWorld.Day * 30 && command.Tick <= int.MaxValue - command.NoticeTicks,
                    "Deliver a bounded notice while actively in the union.");
                next = State with { UnionNoticeUntil = command.Tick + command.NoticeTicks }; break;
            case AllegianceAction.BindLiege:
                Key(command.Authority); Key(command.Contract);
                Require(command.Authority != NativePlayer && command.Consent && State.Contract != command.Contract &&
                    (State.Mode != PlayerAllegianceMode.Vassal || command.OldContractReleased) &&
                    (State.Mode != PlayerAllegianceMode.Union || !State.Suspended && State.UnionNoticeUntil.HasValue &&
                        command.Tick >= State.UnionNoticeUntil.Value && command.UnionDuesSettled),
                    "Live contract, prior release and union notice/settlement are required before changing superior.");
                next = new(PlayerAllegianceMode.Vassal, command.Authority, command.Contract); break;
            case AllegianceAction.Suspend:
                Require(State.Mode != PlayerAllegianceMode.Independent && !State.Suspended, "No active shared diplomacy to suspend.");
                next = State with { Suspended = true }; break;
            case AllegianceAction.Reaffirm:
                Require(State.Mode != PlayerAllegianceMode.Independent && State.Suspended && command.Consent && command.Authority == State.Authority,
                    "Reaffirmation requires fresh consent from the recorded authority.");
                next = State with { Suspended = false }; break;
        }
        State = next; LastTick = command.Tick; commands.Add(command);
    }
    public string Export() => JsonSerializer.Serialize(new PlayerAllegianceSave(Version, NativePlayer, commands.ToArray()));
    public static PlayerAllegiance Restore(string json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && json.Length <= 1000000, "Invalid allegiance snapshot.");
        var save = JsonSerializer.Deserialize<PlayerAllegianceSave>(json);
        Require(save != null && save.Version == Version && save.Commands != null && save.Commands.Length <= 1000, "Unsupported allegiance snapshot.");
        var result = new PlayerAllegiance(save.NativePlayer);
        foreach (var command in save.Commands) result.Apply(command);
        return result;
    }
}

public enum SharedRelationKind { Neutral, Ally, Hostile }
public sealed record SharedRelation(string Other, int BaseGoodwill, SharedRelationKind Kind);
public static class SharedDiplomacyPlan
{
    /// <summary>Joining a shared realm cannot silently make peace with the applicant's existing enemies.</summary>
    public static IReadOnlyList<string> AdmissionConflicts(string player, string authority,
        IEnumerable<SharedRelation> applicant, IEnumerable<SharedRelation> destination)
    {
        var source = Build(player, authority, applicant);
        var target = Build(player, authority, destination);
        if (!source.Select(r => r.Other).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(target.Select(r => r.Other).OrderBy(id => id, StringComparer.Ordinal)))
            throw new ArgumentException("Admission requires the same complete external relation scope.");
        return Array.AsReadOnly(source.Where(r => r.Kind == SharedRelationKind.Hostile &&
                target.Single(t => t.Other == r.Other).Kind != SharedRelationKind.Hostile)
            .Select(r => r.Other).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }
    /// <summary>Copy value snapshots, never alias a mutable game FactionRelation or change ownership.</summary>
    public static IReadOnlyList<SharedRelation> Build(string player, string authority, IEnumerable<SharedRelation> relations)
    {
        if (string.IsNullOrWhiteSpace(player) || string.IsNullOrWhiteSpace(authority) || player == authority) throw new ArgumentException("Invalid diplomacy endpoints.");
        RuntimeCompat.NotNull(relations); var copy = relations.ToArray();
        if (copy.Any(r => r == null || string.IsNullOrWhiteSpace(r.Other) || r.BaseGoodwill is < -100 or > 100 || !RuntimeCompat.IsDefined(r.Kind)) ||
            copy.Select(r => r.Other).Distinct(StringComparer.Ordinal).Count() != copy.Length) throw new ArgumentException("Invalid source diplomacy.");
        return Array.AsReadOnly(copy.Where(r => r.Other != player && r.Other != authority).ToArray());
    }
}
