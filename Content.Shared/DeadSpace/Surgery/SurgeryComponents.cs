// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[DataDefinition, Serializable, NetSerializable]
public partial struct SurgeryLayerState
{
    [DataField]
    public SurgeryLayerKind Kind;

    [DataField]
    public float Integrity = 1f;

    [DataField]
    public bool Retracted;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryBusyComponent : Component
{
    public EntityUid Patient;

    public TimeSpan Until;
}

[RegisterComponent]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField]
    public List<SurgeryVerbKind> Verbs = new();

    [DataField]
    public float Precision = 0.8f;

    [DataField]
    public float Slip = 0.15f;

    [DataField]
    public float Wobble = 16f;

    [DataField]
    public List<string> Games = new();
}

[RegisterComponent]
public sealed partial class SurgeryMarkerComponent : Component
{
}

[RegisterComponent]
public sealed partial class SurgerySurfaceComponent : Component
{
    [DataField]
    public ProtoId<SurgeryPlacePrototype> Place = "Table";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class SurgerySiteComponent : Component
{
    [DataField, AutoNetworkedField]
    public LimbZone Zone = LimbZone.Torso;

    [DataField, AutoNetworkedField]
    public string Part = "";

    [DataField, AutoNetworkedField]
    public string Queued = "";

    public byte Rolled = 255;

    public int RolledStep = -1;

    [DataField, AutoNetworkedField]
    public List<SurgeryLayerState> Layers = new();

    [DataField]
    public float Germs;

    [DataField]
    public bool Gel;

    [DataField]
    public float SpringBone;

    [DataField]
    public TimeSpan SpringAt;

    [DataField]
    public HashSet<LimbOrgan> Examined = new();

    [DataField]
    public int Step;

    [DataField]
    public byte Chosen = 255;

    [DataField]
    public bool Clamped;

    public float Cut;

    public float Accumulator;

    public bool Done;

    public Dictionary<string, SurgeryKept> Kept = new();
}

[Serializable, NetSerializable]
public sealed class SurgeryKept
{
    public string Part = "";
    public string Queued = "";
    public List<SurgeryLayerState> Layers = new();
    public int Step;
    public byte Chosen = 255;
    public bool Clamped;
    public float Germs;
    public bool Gel;
    public float Cut;
    public HashSet<LimbOrgan> Examined = new();
}

[RegisterComponent]
public sealed partial class OrganFindingComponent : Component
{
    [DataField]
    public string Owner = string.Empty;

    [DataField]
    public float Damage;

    [DataField]
    public bool Ruptured;

    [DataField]
    public string Organ = string.Empty;
}
