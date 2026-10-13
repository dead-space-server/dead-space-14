// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.TheCircle.ArchitectArm;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArchitectArmComponent : Component
{
    [DataField]
    public float DashRange = 64f;

    [DataField]
    public float DashSpeed = 28f;

    [DataField]
    public TimeSpan DashWindup = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan DashDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan DashCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public int DashObjectsPerSlowdown = 5;

    [DataField]
    public float DashSpeedReduction = 5f;

    [DataField]
    public float DashMinimumSpeed = 1f;

    [DataField]
    public TimeSpan DashObstacleRestartDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public HashSet<EntProtoId> DashUnbreakablePrototypes = new();

    [DataField]
    public ProtoId<AlertPrototype> CooldownAlert = "ArchitectArmDashCooldown";

    [DataField, AutoNetworkedField]
    public TimeSpan NextDash;
}


[Serializable, NetSerializable]
public sealed class ArchitectArmDashRequestEvent(NetEntity item, NetCoordinates target) : EntityEventArgs
{
    public NetEntity Item = item;
    public NetCoordinates Target = target;
}

[Serializable, NetSerializable]
public sealed class ArchitectArmWindupEvent(NetCoordinates target, TimeSpan duration) : EntityEventArgs
{
    public NetCoordinates Target = target;
    public TimeSpan Duration = duration;
}
