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
    /// The way from an animal that is outside of a pen to the middle of one: its own, if it was taken out of it, else
    /// the nearest one (by the distance to its middle) that is within <c>husbandry.pen.return_range</c> tiles of it. The
    /// way is straight, one point, no search: the animal walks towards the pen, and if something is in the way (a wall,
    /// a shut door) it gets stuck there, which is as close as it gets. The next try is after
    /// <see cref="AnimalPenComponent.ReturnRetryDelay"/>, and if there is no pen to go to, after
    /// <see cref="AnimalPenComponent.ReturnSearchDelay"/>.
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
        var pen = Live(comp.Pen);
        if (pen == null)
        {
            // The nearest by the middle, no further than the range from the edge of it.
            var range = _cfg.GetCVar(CCVars.HusbandryPenReturnRange);
            var best = float.MaxValue;
            foreach (var other in _pens)
            {
                if (other.Grid != gridUid || other.Tiles.Count == 0)
                    continue;

                var distance = Vector2.Distance(new Vector2(start.X, start.Y), new Vector2(other.Anchor.X, other.Anchor.Y));
                if (distance - other.Radius / grid.TileSize <= range && distance < best)
                {
                    best = distance;
                    pen = other;
                }
            }
        }

        if (pen == null || pen.Tiles.Count == 0 || pen.Grid != gridUid || pen.Contains(start))
        {
            comp.NextReturnSearch = now + comp.ReturnSearchDelay;
            return false;
        }

        // In the grid coordinates, so that the point stays where it is if the grid moves.
        var middle = (new Vector2(pen.Anchor.X, pen.Anchor.Y) + new Vector2(0.5f, 0.5f)) * grid.TileSize;
        route.Add(new EntityCoordinates(gridUid, middle));

        // If the walk fails (it is stuck at a wall), not again at once.
        comp.NextReturnSearch = now + comp.ReturnRetryDelay;
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
