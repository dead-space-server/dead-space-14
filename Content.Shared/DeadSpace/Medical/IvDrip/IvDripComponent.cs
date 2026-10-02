// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Tag;
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
    [DataField]
    public string TankSolution = string.Empty;

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
    public float MinAttachRange;

    [DataField]
    public float MaxAttachRange;

    [DataField]
    public float MaxNeedleRange;

    [DataField]
    public float TransferIntervalSeconds;

    [DataField]
    public float SlowTransfer;

    [DataField]
    public float MediumTransfer;

    [DataField]
    public float FastTransfer;

    [DataField]
    public float IdleDripTransfer;

    [DataField]
    public DamageSpecifier YankDamage = new();

    [DataField]
    public float YankSpillFraction;

    [DataField]
    public float MinYankSpill;

    [DataField]
    public float YankBlood;

    [DataField]
    public float AttachDelay;

    [DataField]
    public ProtoId<ReagentPrototype> BloodReagent;

    [DataField]
    public ProtoId<TagPrototype> BloodpackTag;

    [DataField]
    public float BloodpackFillAmount;

    [DataField]
    public Dictionary<IvDripSpeed, string> SliderStates = new();

    [DataField, AutoNetworkedField]
    public EntityUid? ActiveNeedle;

    [DataField]
    public EntProtoId NeedlePrototype;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIvDripSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class IvDripNeedleComponent : Component
{
    // Runtime link. A data-field would be written into the spawnable prototype and fail the save test.
    [AutoNetworkedField, ViewVariables]
    public EntityUid? Drip;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIvDripSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class IvDripConnectedComponent : Component
{
    [AutoNetworkedField, ViewVariables]
    public EntityUid? Drip;
}
