// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// While the item is held, Alt + right click performs one of its world-target actions at the cursor,
/// with the action's own cooldown and range.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandAltUseActionComponent : Component
{
    /// <summary>
    /// Prototype of the item's action to perform. Null: the item handles Alt + right click itself
    /// (<see cref="LavalandAltUseEvent"/>), without any action button.
    /// </summary>
    [DataField]
    public EntProtoId? Action;
}
