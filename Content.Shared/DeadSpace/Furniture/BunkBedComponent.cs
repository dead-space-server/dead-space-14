using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Furniture;

/// <summary>
/// Spawns a separate top-layer sprite entity so that buckled mobs can render
/// between the two bunks of a bunk bed.
/// </summary>
/// <remarks>
/// Render order for a bunk bed, from back to front. Numbers are DrawDepth values:
/// 0 = Objects: the base sprite on the strap entity itself.
/// 5 = BelowMobs: lower occupant
/// 6 = Mobs: the upper/front frame spawned by this component
/// 7 = OverMobs: upper occupant
/// </list>
/// The lower mob is therefore drawn before the top overlay and gets hidden behind it,
/// while the upper mob is drawn after the overlay and stays fully visible.
/// <c>Strap.modifyBuckleDrawDepth</c> is disabled on the bunk bed so the generic
/// strap depth logic cannot overwrite these per-slot depths.
/// There is intentionally no layer above 7 yet: the front rail does not cover the
/// upper occupant. That would need another sprite state with depth &gt; 7.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BunkBedComponent : Component
{
    /// <summary>
    /// Prototype for the upper sprite layer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId Overlay = "BunkBedTop";
}

/// <summary>
/// Marker for the spawned top-layer entity, so map loads don't create duplicates.
/// </summary>
[RegisterComponent]
public sealed partial class BunkBedOverlayComponent : Component;
