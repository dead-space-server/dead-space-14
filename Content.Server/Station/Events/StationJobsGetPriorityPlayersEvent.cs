using Robust.Shared.Network;

namespace Content.Server.Station.Events;

[ByRefEvent]
public readonly record struct StationJobsGetPriorityPlayersEvent(
    IReadOnlyCollection<NetUserId> Players,
    HashSet<NetUserId> PriorityPlayers);
