using Content.Shared.Chat.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Wagging;

/// <summary>
/// An emoting wag for markings.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(WaggingSystem))]
public sealed partial class WaggingComponent : Component
{
    /// <summary>
    /// The prototype id of the wagging action.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionToggleWagging";

    /// <summary>
    /// Reference to the action entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// The suffix to add to get the animated marking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Suffix = "Animated";

    /// <summary>
    /// Whether the entity is currently wagging.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Wagging = false;
}
