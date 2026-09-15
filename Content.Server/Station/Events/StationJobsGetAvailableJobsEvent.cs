using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Events;

[ByRefEvent]
public readonly record struct StationJobsGetAvailableJobsEvent(
    EntityUid Station,
    IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> Profiles,
    Dictionary<ProtoId<JobPrototype>, int?> Jobs);
