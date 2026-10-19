// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// One tile of Bubblegum's wall of blood: a hard barrier the client draws as the Circle's blink blood - churning
/// drops, thickening in and thinning out over <see cref="Lifetime"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandBloodWallComponent : Component
{
    /// <summary>Seconds it stands; keep it the same as its timed despawn.</summary>
    [DataField, AutoNetworkedField]
    public float Lifetime = 6f;
}
