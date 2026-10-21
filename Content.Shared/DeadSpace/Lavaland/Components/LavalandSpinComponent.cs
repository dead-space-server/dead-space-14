// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// The sprite turns round and round while <see cref="Spinning"/> is on (the Legion rolling at its target).
/// Drawn by the client, the entity itself does not rotate.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandSpinComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Spinning;

    /// <summary>Radians per second.</summary>
    [DataField, AutoNetworkedField]
    public float Speed = 5f;
}
