// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// Ore a bluespace well can pull in when it lies loose on lavaland.
/// Wells look only through entities with this marker instead of every stack in the game.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandWellOreComponent : Component;
