using Content.Server.DeadSpace.Armutant.Objectives.CreateMapObjective;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Armutant.Objectives;

[RegisterComponent, Access(typeof(ObjectiveTeleportToStationSystem))]
public sealed partial class ObjectiveTeleportToStationComponent : Component
{
    public EntityUid? StationGridId;

    public bool UsingItem = false;

    [DataField]
    public EntProtoId? SelfEffect = "OutVoidTeleportSelfEffect";
}
