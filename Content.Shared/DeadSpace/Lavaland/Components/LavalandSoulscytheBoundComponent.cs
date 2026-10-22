// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// An awakened soul scythe: nobody but its master can pick it up. Shared, so the client does not show it
/// in somebody else's hand for a moment before the server takes it back.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandSoulscytheBoundComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Master;
}
