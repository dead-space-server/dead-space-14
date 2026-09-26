// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.Medical.IvDrip;

[Serializable, NetSerializable]
public enum IvDripSpeed : byte
{
    Off = 0,
    Slow = 1,
    Medium = 2,
    Fast = 3,
}

[Serializable, NetSerializable]
public enum IvDripVisuals : byte
{
    HasBag,
    BagColor,
    Speed,
    Folded,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(SharedIvDripSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class IvDripComponent : Component
{
    public const string TankSolutionId = "tank";

    [DataField, AutoNetworkedField]
    public bool CanRefold;

    [DataField, AutoNetworkedField]
    public IvDripSpeed Speed = IvDripSpeed.Off;

    [DataField, AutoNetworkedField]
    public EntityUid? AttachedPatient;

    [DataField, AutoNetworkedField]
    public bool PatientWasDowned;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextTransfer;

    [DataField]
    public float MinAttachRange = 0.45f;

    [DataField]
    public float MaxAttachRange = 1.6f;

    [DataField]
    public float MaxNeedleRange = 2.25f;

    [DataField]
    public float TransferIntervalSeconds = 1f;

    [DataField]
    public float SlowTransfer = 0.5f;

    [DataField]
    public float MediumTransfer = 1.5f;

    [DataField]
    public float FastTransfer = 3f;

    [DataField]
    public float IdleDripTransfer = 0.5f;

    [DataField]
    public float YankPiercing = 6f;

    [DataField]
    public float YankSlash = 4f;

    [DataField]
    public float YankSpillFraction = 0.25f;

    [DataField]
    public float BloodpackFillAmount = 20f;

    [DataField, AutoNetworkedField]
    public EntityUid? ActiveNeedle;

    [DataField]
    public EntProtoId NeedlePrototype = "IvDripNeedle";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIvDripSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class IvDripNeedleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Drip;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIvDripSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class IvDripConnectedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Drip;
}
