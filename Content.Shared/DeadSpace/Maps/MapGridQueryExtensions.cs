// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.DeadSpace.Maps;

/// <summary>
/// The engine's grid queries fill a caller's list instead of returning one; these hand back a fresh list for the
/// rare, non-hot places that only want to walk the grids once.
/// </summary>
public static class MapGridQueryExtensions
{
    public static List<Entity<MapGridComponent>> GridsIntersecting(this SharedMapSystem map, MapId mapId, Box2 worldAABB)
    {
        var grids = new List<Entity<MapGridComponent>>();
        map.FindGridsIntersecting(mapId, worldAABB, ref grids);
        return grids;
    }
}
