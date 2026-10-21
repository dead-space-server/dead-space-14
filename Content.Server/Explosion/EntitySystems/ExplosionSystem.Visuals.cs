using System.Numerics;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Server.Explosion.EntitySystems;

// This part of the system handled send visual / overlay data to clients.
public sealed partial class ExplosionSystem
{
    public void InitVisuals()
    {
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentGetState>(OnGetState);
    }

    private void OnGetState(EntityUid uid, ExplosionVisualsComponent component, ref ComponentGetState args)
    {
        Dictionary<NetEntity, Dictionary<int, List<Vector2i>>> tileLists = new();
        foreach (var (grid, data) in component.Tiles)
        {
            tileLists.Add(GetNetEntity(grid), data);
        }

        args.State = new ExplosionVisualsState(
            component.Epicenter,
            component.ExplosionType,
            component.Intensity,
            component.SpaceTiles,
            tileLists,
            component.SpaceMatrix,
            component.SpaceTileSize);
    }

    /// <summary>
    ///     Constructor for the shared <see cref="ExplosionEvent"/> using the server-exclusive explosion classes.
    /// </summary>
    private EntityUid CreateExplosionVisualEntity(MapCoordinates epicenter, string prototype, Matrix3x2 spaceMatrix, ExplosionSpaceTileFlood? spaceData, IEnumerable<ExplosionGridTileFlood> gridData, List<float> iterationIntensity)
    {
        var explosionEntity = Spawn(null, MapCoordinates.Nullspace);
        var comp = AddComp<ExplosionVisualsComponent>(explosionEntity);

        foreach (var grid in gridData)
        {
            comp.Tiles.Add(grid.Grid.Owner, grid.TileLists);
        }

        comp.SpaceTiles = spaceData?.TileLists;
        comp.Epicenter = epicenter;
        comp.ExplosionType = prototype;
        comp.Intensity = iterationIntensity;
        comp.SpaceMatrix = spaceMatrix;
        comp.SpaceTileSize = spaceData?.TileSize ?? DefaultTileSize;
        Dirty(explosionEntity, comp);

        // Light, sound & visuals may extend well beyond normal PVS range. In principle, this should probably still be
        // restricted to something like the same map, but whatever.
        _pvsSys.AddGlobalOverride(explosionEntity);

        var appearance = AddComp<AppearanceComponent>(explosionEntity);
        _appearance.SetData(explosionEntity, ExplosionAppearanceData.Progress, 1, appearance);

        return explosionEntity;
    }

    // DS14-start
    /// <summary>
    ///     Draws an explosion exactly as a queued one would look, but touches nothing: no damage, no throwing,
    ///     no tiles. For blasts that must stay harmless somewhere.
    /// </summary>
    public void SpawnExplosionVisual(MapCoordinates epicenter, string typeId, float totalIntensity, float slope, float maxIntensity)
    {
        var results = GetExplosionTiles(epicenter, typeId, totalIntensity, slope, maxIntensity);
        if (results == null)
            return;

        var (_, iterationIntensity, spaceData, gridData, spaceMatrix) = results.Value;
        var visualEnt = CreateExplosionVisualEntity(epicenter, typeId, spaceMatrix, spaceData, gridData.Values, iterationIntensity);
        _appearance.SetData(visualEnt, ExplosionAppearanceData.Progress, int.MaxValue);
        EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(visualEnt).Lifetime =
            _cfg.GetCVar(Content.Shared.CCVar.CCVars.ExplosionPersistence);
    }
    // DS14-end
}
