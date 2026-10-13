using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Smokables;

/// <summary>
/// Replicated presentation of a deployed shisha hose. Removed when the hose docks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShishaHoseVisualsComponent : Component
{
    /// <summary>
    /// The sprite to use for the line.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SpriteSpecifier Sprite = default!;

    /// <summary>
    /// The line is drawn between this target and the entity owning the component.
    /// </summary>
    /// <summary>
    /// TODO: WeakEntityReference.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Offset from Body A.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 OffsetA;

    /// <summary>
    /// Offset from Body B.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 OffsetB;

    /// <summary>World-space downward sag for a curved line. Zero retains the straight renderer.</summary>
    [DataField, AutoNetworkedField]
    public float Sag;
}
