using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Updates a sprite layer based on whether an entity is moving via input or not.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SpriteMovementComponent : Component
{
    /// <summary>
    /// Layer and sprite state to use when moving.
    /// </summary>
    [DataField]
    public Dictionary<string, PrototypeLayerData> MovementLayers = new();

    /// <summary>
    /// Layer and sprite state to use when not moving.
    /// </summary>
    [DataField]
    public Dictionary<string, PrototypeLayerData> NoMovementLayers = new();

    // DS14-start
    /// <summary>
    /// Opts equipped states ending in idle/walk into movement animation, synchronized with this body layer.
    /// </summary>
    [DataField]
    public string? EquipmentAnimationLayer;
    // DS14-end

    [DataField, AutoNetworkedField]
    public bool IsMoving;
}
