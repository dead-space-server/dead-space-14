// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Inventory;
using Content.Shared.Weather;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    /// <summary>
    /// DS14: a storm visor over the local player's eyes lets almost all of the storm through: its weather is drawn
    /// at a small fraction of its opacity. A creature built to see through the storm (a borg with storm optics)
    /// carries the visor itself. 1 without one.
    /// </summary>
    private float GetStormVisorOpacity(ProtoId<WeatherPrototype> weather)
    {
        if (_player.LocalEntity is not { } player)
            return 1f;

        if (!_entManager.TryGetComponent(player, out LavalandStormVisorComponent? visor) &&
            (!_entManager.System<InventorySystem>().TryGetSlotEntity(player, "eyes", out var eyes) ||
             !_entManager.TryGetComponent(eyes, out visor)))
        {
            return 1f;
        }

        if (visor.Weathers.Count > 0 && !visor.Weathers.Contains(weather))
            return 1f;

        return Math.Clamp(visor.StormOpacity, 0f, 1f);
    }
}
