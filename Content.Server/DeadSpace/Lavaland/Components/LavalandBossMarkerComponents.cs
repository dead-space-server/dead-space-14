// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// Mapping marker on a boss arena grid (an arena with a grid path): the arena boss appears on this tile
/// instead of the middle of the floor. The marker is removed when the arena is built.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBossSpawnPointComponent : Component;

/// <summary>
/// Mapping marker: spawns a lavaland boss when the map loads, outside of any boss arena, then removes itself.
/// The Legion fights on its own after that; other bosses need an arena for their attacks.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBossSpawnerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Boss;

    /// <summary>
    /// The boss stands still and does not attack until woken: a Legion gate nearby opening or being hit.
    /// </summary>
    [DataField]
    public bool Dormant = true;
}

/// <summary>A boss living outside any arena: on death it still drops its rewards and leaves like an arena boss.</summary>
[RegisterComponent]
public sealed partial class LavalandFreeBossComponent : Component;
