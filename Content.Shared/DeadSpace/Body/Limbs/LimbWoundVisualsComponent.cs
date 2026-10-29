using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LimbWoundVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<float> Thresholds = new() { 0.1f, 0.2f, 0.3f, 0.5f, 0.7f, 0.9f };

    [DataField, AutoNetworkedField]
    public List<string> States = new() { "10", "20", "30", "50", "70", "100" };

    [DataField, AutoNetworkedField]
    public Dictionary<string, LimbWoundSprite> Groups = new();

    [DataField, AutoNetworkedField]
    public LimbWoundSprite? Bandage;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class LimbWoundSprite
{
    [DataField(required: true)]
    public string Sprite = default!;

    [DataField]
    public string? Color;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LimbPartWoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public LimbZone Zone;

    [DataField, AutoNetworkedField]
    public LimbZoneState State;
}
