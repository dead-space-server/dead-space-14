using Content.Shared.Preferences;
using Robust.Shared.Player;

namespace Content.Server.GameTicking.Events;

[ByRefEvent]
public struct PlayerSpawnAttemptEvent(ICommonSession player, HumanoidCharacterProfile profile, EntityUid station, string? jobId)
{
    public readonly ICommonSession Player = player;
    public readonly HumanoidCharacterProfile Profile = profile;
    public readonly EntityUid Station = station;
    public readonly string? JobId = jobId;
    public bool Cancelled;
    public string? Reason;
}
