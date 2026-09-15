// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Explosion.EntitySystems;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] public readonly ExplosionSystem Explosion = default!;

    public MapCoordinates GetTileWorldCoordinates(TileAtmosphere tile)
    {
        if (!TryComp(tile.GridIndex, out MapGridComponent? grid))
            return MapCoordinates.Nullspace;

        return _map.GridTileToWorld(tile.GridIndex, grid, tile.GridIndices);
    }
}
