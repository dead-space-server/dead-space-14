// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Pen;
using Content.Shared.DeadSpace.Husbandry.Walking;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Pen;

/// <summary>
/// The ways inside a pen, and the way to a pen for an animal that is outside of one.
/// </summary>
public sealed partial class AnimalPenSystem
{
    /// <summary>
    /// The way inside the pen from the animal to the nearest of the targets: the tiles of the pen one after another,
    /// and the target itself last. One breadth-first search for all the targets, no pathfinder. A target is reached
    /// from its own tile or, if that tile is not in the pen (it is a wall, a feeder), from a tile of the pen next to
    /// it.
    /// </summary>
    /// <returns>False if the animal is not in its pen or none of the targets can be reached inside of it.</returns>
    public bool TryGetRoute(
        Entity<AnimalPenComponent> animal,
        AnimalPen pen,
        IEnumerable<EntityUid> targets,
        out EntityUid chosen,
        out List<EntityCoordinates> route)
    {
        chosen = default;
        route = new List<EntityCoordinates>();

        if (!TryComp<MapGridComponent>(pen.Grid, out var grid))
            return false;

        var xform = Transform(animal);
        if (xform.GridUid != pen.Grid)
            return false;

        var start = _map.CoordinatesToTile(pen.Grid, grid, xform.Coordinates);
        if (!pen.Contains(start))
            return false;

        // Every tile the targets can be reached from, and which target it is.
        var goals = new Dictionary<(int X, int Y), EntityUid>();
        foreach (var target in targets)
        {
            var targetXform = Transform(target);
            if (targetXform.GridUid != pen.Grid)
                continue;

            var tile = _map.CoordinatesToTile(pen.Grid, grid, targetXform.Coordinates);
            AddGoal(pen, goals, tile, target);
            AddGoal(pen, goals, tile + new Vector2i(1, 0), target);
            AddGoal(pen, goals, tile + new Vector2i(-1, 0), target);
            AddGoal(pen, goals, tile + new Vector2i(0, 1), target);
            AddGoal(pen, goals, tile + new Vector2i(0, -1), target);
        }

        if (goals.Count == 0)
            return false;

        var cells = PenRules.FindRoute(
            (start.X, start.Y),
            cell => pen.Contains(new Vector2i(cell.X, cell.Y)),
            cell => goals.ContainsKey(cell),
            pen.Tiles.Count + 1);

        if (cells == null)
            return false;

        chosen = goals[cells[^1]];
        AddTiles(route, pen.Grid, grid, cells);
        route.Add(new EntityCoordinates(chosen, Vector2.Zero));
        return true;
    }

    /// <summary>
    /// The way from an animal that is in no pen to the nearest pen that it can get to (or to its own, if it was taken
    /// out of it): the tiles one after another, the last one is in the pen. A breadth-first search over the tiles that
    /// can be walked, no further than <c>husbandry.pen.max_tiles</c> tiles, and not again for
    /// <see cref="AnimalPenComponent.ReturnSearchDelay"/> if there is no way. Open doors and gaps are free tiles, shut
    /// doors and walls are not, so the animal finds the way in if there is one.
    /// </summary>
    public bool TryGetRouteToPen(Entity<AnimalPenComponent> animal, out List<EntityCoordinates> route)
    {
        route = new List<EntityCoordinates>();
        var comp = animal.Comp;
        var now = _timing.CurTime;
        if (now < comp.NextReturnSearch)
            return false;

        var xform = Transform(animal);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        ResolveDevices();

        var start = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var own = Live(comp.Pen);

        // Cheap before the search: is there a pen of this grid in reach at all?
        var range = _cfg.GetCVar(CCVars.HusbandryPenReturnRange);
        var inReach = false;
        foreach (var pen in _pens)
        {
            if (pen.Grid != gridUid || pen.Tiles.Count == 0 || own != null && pen != own)
                continue;

            if (Vector2.Distance(new Vector2(start.X, start.Y), new Vector2(pen.Anchor.X, pen.Anchor.Y)) <= range + pen.Radius / grid.TileSize)
            {
                inReach = true;
                break;
            }
        }

        if (!inReach)
        {
            comp.NextReturnSearch = now + comp.ReturnSearchDelay;
            return false;
        }

        var wallArea = _cfg.GetCVar(CCVars.HusbandryPenWallArea);
        var cells = PenRules.FindRoute(
            (start.X, start.Y),
            cell => IsFree(gridUid, grid, cell, CollisionGroup.MobMask, wallArea),
            cell => own != null ? own.Contains(new Vector2i(cell.X, cell.Y)) : TileOwner(gridUid, new Vector2i(cell.X, cell.Y)) != null,
            Math.Max(1, _cfg.GetCVar(CCVars.HusbandryPenMaxTiles)));

        if (cells == null || cells.Count < 2)
        {
            comp.NextReturnSearch = now + comp.ReturnSearchDelay;
            return false;
        }

        // Found: if the walk fails, not again at once.
        comp.NextReturnSearch = now + comp.ReturnRetryDelay;
        AddTiles(route, gridUid, grid, cells);
        return true;
    }

    /// <summary>
    /// From the second tile on (the animal stands on the first): the middles of the tiles, in the coordinates of the
    /// grid, so the route stays where it is if the grid moves.
    /// </summary>
    private static void AddTiles(List<EntityCoordinates> route, EntityUid gridUid, MapGridComponent grid, List<(int X, int Y)> cells)
    {
        for (var i = 1; i < cells.Count; i++)
        {
            var middle = (new Vector2(cells[i].X, cells[i].Y) + new Vector2(0.5f, 0.5f)) * grid.TileSize;
            route.Add(new EntityCoordinates(gridUid, middle));
        }
    }

    private static void AddGoal(AnimalPen pen, Dictionary<(int X, int Y), EntityUid> goals, Vector2i tile, EntityUid target)
    {
        if (pen.Contains(tile))
            goals.TryAdd((tile.X, tile.Y), target);
    }
}
