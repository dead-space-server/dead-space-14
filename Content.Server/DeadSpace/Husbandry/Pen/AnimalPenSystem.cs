// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
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
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Pen;

/// <summary>
/// The pen of an animal, see <see cref="AnimalPenComponent"/>, and what the animals do with it:
/// <list type="bullet">
/// <item>The pen is worked out from the <b>home</b> of the animal, not from where it stands now: the walls round the
/// home, but no more than a square round it. So an animal that was pushed out of its pen still belongs to it (it
/// is taken for a new home only after it has stood outside for <see cref="AnimalPenComponent.RehomeTime"/>).</item>
/// <item>The animal answers <see cref="AnimalTargetCheckEvent"/> here: it does not pick a place, a food or a water
/// that lies outside its pen, so it does not walk away from it.</item>
/// <item>The things in the pen (food, water, feeders) are looked for here, and the way to them is a breadth-first
/// search over the tiles of the pen (<see cref="TryGetRoute"/>): one run finds the nearest of all of them, and
/// there is no pathfinder job for the AI.</item>
/// </list>
/// A pen is not worked out every time: a pen that is closed in is the same for every animal inside it, so it is
/// worked out once and given to all of them, and every result is kept for the cvar
/// <c>husbandry.pen.cache_time</c> (walls and doors change, but not often).
/// </summary>
public sealed class AnimalPenSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    /// <summary>
    /// Pens that are closed in, by every tile of them: whoever has a home on one of the tiles has that pen.
    /// </summary>
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), (AnimalPen Pen, TimeSpan Time)> _enclosures = new();

    /// <summary>
    /// Pens of animals of an open pen, one for each of them.
    /// </summary>
    private readonly Dictionary<EntityUid, (AnimalPen Pen, TimeSpan Time)> _open = new();

    private TimeSpan _nextSweep;
    private TimeSpan _cacheTime = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalPenComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AnimalPenComponent, AnimalTargetCheckEvent>(OnTargetCheck);
        Subs.CVar(_cfg, CCVars.HusbandryPenCacheTime, value => _cacheTime = TimeSpan.FromSeconds(value), true);
    }

    private void OnShutdown(Entity<AnimalPenComponent> animal, ref ComponentShutdown args)
    {
        _open.Remove(animal);
    }

    /// <summary>
    /// A place, a food or a water that is not in the pen is not allowed: the animal does not go there. A thing that
    /// stands next to the pen (a feeder in the wall, say) is, it is reached from the pen.
    /// </summary>
    private void OnTargetCheck(Entity<AnimalPenComponent> animal, ref AnimalTargetCheckEvent args)
    {
        if (!args.Allowed || GetPen(animal) is not { } pen || !TryComp<MapGridComponent>(pen.Grid, out var grid))
            return;

        var tile = _map.TileIndicesFor(pen.Grid, grid, args.Target);
        if (!pen.Contains(tile) && !(args.Reach > 0f && TouchesPen(pen, tile)))
            args.Allowed = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Forget what is old, so that a deleted grid or a lot of old tiles are not kept forever.
        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;

        _nextSweep = now + _cacheTime;

        List<(EntityUid, Vector2i)>? oldTiles = null;
        foreach (var (key, value) in _enclosures)
        {
            if (now - value.Time >= _cacheTime || TerminatingOrDeleted(key.Grid))
                (oldTiles ??= new()).Add(key);
        }

        if (oldTiles != null)
        {
            foreach (var key in oldTiles)
            {
                _enclosures.Remove(key);
            }
        }

        List<EntityUid>? oldAnimals = null;
        foreach (var (key, value) in _open)
        {
            if (now - value.Time >= _cacheTime || TerminatingOrDeleted(key))
                (oldAnimals ??= new()).Add(key);
        }

        if (oldAnimals != null)
        {
            foreach (var key in oldAnimals)
            {
                _open.Remove(key);
            }
        }
    }

    /// <summary>
    /// The pen of the animal: the one round its home. Null if it has no home yet and does not stand on a grid, or
    /// has nowhere to stand.
    /// </summary>
    public AnimalPen? GetPen(Entity<AnimalPenComponent> animal)
    {
        var now = _timing.CurTime;
        var comp = animal.Comp;
        var xform = Transform(animal);

        Vector2i? tileNow = null;
        EntityUid? gridNow = null;
        if (xform.GridUid is { } g && TryComp<MapGridComponent>(g, out var gridComp))
        {
            gridNow = g;
            tileNow = _map.CoordinatesToTile(g, gridComp, xform.Coordinates);
        }

        // The home is where the animal was when its pen was asked for the first time.
        if (comp.HomeGrid == null || TerminatingOrDeleted(comp.HomeGrid.Value))
        {
            if (gridNow == null || tileNow == null)
                return null;

            Rehome(comp, gridNow.Value, tileNow.Value);
        }

        var pen = PenAround(animal, comp.HomeGrid!.Value, comp.Home);

        // The home itself became a wall or a hole: the animal takes the place where it stands for home.
        if (pen == null)
        {
            if (gridNow == null || tileNow == null)
                return null;

            Rehome(comp, gridNow.Value, tileNow.Value);
            pen = PenAround(animal, gridNow.Value, tileNow.Value);
            if (pen == null)
                return null;
        }

        if (gridNow == pen.Grid && tileNow is { } tile && pen.Contains(tile))
        {
            comp.OutsideSince = null;
            return pen;
        }

        // Outside of its pen. For a moment (it is pushed, pulled, it is crossing something) that does not change
        // anything, but an animal that was carried away and left there has a new home after a while.
        if (gridNow != null && tileNow != null)
        {
            comp.OutsideSince ??= now;
            if (now - comp.OutsideSince >= comp.RehomeTime)
            {
                Rehome(comp, gridNow.Value, tileNow.Value);
                pen = PenAround(animal, gridNow.Value, tileNow.Value) ?? pen;
            }
        }

        return pen;
    }

    private static void Rehome(AnimalPenComponent comp, EntityUid grid, Vector2i tile)
    {
        comp.HomeGrid = grid;
        comp.Home = tile;
        comp.OutsideSince = null;
    }

    /// <summary>
    /// The pen with the tile in it: the whole enclosure if it is closed in, otherwise the square round the tile as far
    /// as the walls go.
    /// </summary>
    private AnimalPen? PenAround(Entity<AnimalPenComponent> animal, EntityUid gridUid, Vector2i start)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var now = _timing.CurTime;

        if (_enclosures.TryGetValue((gridUid, start), out var shared) && now - shared.Time < _cacheTime)
            return shared.Pen;

        if (_open.TryGetValue(animal, out var cached) &&
            now - cached.Time < _cacheTime &&
            cached.Pen.Grid == gridUid &&
            cached.Pen.Anchor == start)
        {
            return cached.Pen;
        }

        var comp = animal.Comp;
        var mask = (CollisionGroup) _physics.GetHardCollision(animal).Mask;
        Func<(int X, int Y), bool> free = cell => IsFree(gridUid, grid, cell, mask, comp.WallArea);
        var cells = new HashSet<(int X, int Y)>();

        // First the whole enclosure: the tiles up to the walls, the same for every animal inside. If it is closed
        // there is nothing more to it.
        var tooBig = PenRules.Fill((start.X, start.Y), free, _ => true, cells, comp.MaxTiles);
        Vector2i anchor;
        PenKind kind;

        if (cells.Count > 0 && !tooBig)
        {
            kind = PenKind.Enclosed;
            anchor = Centre(cells);
        }
        else
        {
            // Open (or too big to be a pen): the square round the home, as far as the walls go.
            anchor = start;
            var half = comp.HalfSize;
            Func<(int X, int Y), bool> inSquare = cell => Math.Abs(cell.X - start.X) <= half && Math.Abs(cell.Y - start.Y) <= half;

            var open = PenRules.Fill((start.X, start.Y), free, inSquare, cells, comp.MaxTiles);
            kind = open ? PenKind.Open : PenKind.Enclosed;
        }

        if (cells.Count == 0)
            return null;

        var tiles = new HashSet<Vector2i>(cells.Count);
        foreach (var (x, y) in cells)
        {
            tiles.Add(new Vector2i(x, y));
        }

        var farthest = 0f;
        foreach (var cell in tiles)
        {
            farthest = Math.Max(farthest, Vector2.Distance(new Vector2(cell.X, cell.Y), new Vector2(anchor.X, anchor.Y)));
        }

        var pen = new AnimalPen(gridUid, tiles, kind, anchor, (farthest + 1.5f) * grid.TileSize);

        if (kind == PenKind.Enclosed)
        {
            foreach (var cell in tiles)
            {
                _enclosures[(gridUid, cell)] = (pen, now);
            }
        }
        else
        {
            _open[animal] = (pen, now);
        }

        return pen;
    }

    /// <summary>
    /// The middle of a set of tiles, as a tile: the same for the same set, so every animal of an enclosure has it.
    /// </summary>
    private static Vector2i Centre(HashSet<(int X, int Y)> cells)
    {
        long x = 0;
        long y = 0;
        foreach (var (cx, cy) in cells)
        {
            x += cx;
            y += cy;
        }

        return new Vector2i((int) Math.Round((double) x / cells.Count), (int) Math.Round((double) y / cells.Count));
    }

    private static bool TouchesPen(AnimalPen pen, Vector2i tile)
    {
        return pen.Contains(tile + new Vector2i(1, 0)) ||
               pen.Contains(tile + new Vector2i(-1, 0)) ||
               pen.Contains(tile + new Vector2i(0, 1)) ||
               pen.Contains(tile + new Vector2i(0, -1));
    }

    /// <summary>
    /// What lies in the pen: the things with the component that are on the floor of one of its tiles (not in a
    /// container). With <paramref name="adjacent"/> also the things on a tile next to the pen: a feeder or a trough
    /// that stands in the wall is eaten from the pen.
    /// </summary>
    public void GetEntitiesInPen<T>(AnimalPen pen, HashSet<Entity<T>> result, bool adjacent = false) where T : IComponent
    {
        result.Clear();
        if (!TryComp<MapGridComponent>(pen.Grid, out var grid))
            return;

        var centre = _map.GridTileToWorld(pen.Grid, grid, pen.Anchor);
        // The lookup gets a set of its own for each kind, so that nothing is allocated for every call.
        var found = PenLookupCache<T>.Set;
        found.Clear();
        _lookup.GetEntitiesInRange(centre, pen.Radius + (adjacent ? grid.TileSize : 0f), found, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);

        foreach (var entity in found)
        {
            if (_container.IsEntityInContainer(entity))
                continue;

            var xform = Transform(entity);
            if (xform.GridUid != pen.Grid)
                continue;

            var tile = _map.CoordinatesToTile(pen.Grid, grid, xform.Coordinates);
            if (pen.Contains(tile) || (adjacent && TouchesPen(pen, tile)))
                result.Add(entity);
        }
    }

    private static class PenLookupCache<T> where T : IComponent
    {
        public static readonly HashSet<Entity<T>> Set = new();
    }

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

        // From the second tile: the animal stands on the first. The middles of the tiles, in the coordinates of the
        // grid, so the route stays where it is if the grid moves.
        for (var i = 1; i < cells.Count; i++)
        {
            var middle = (new Vector2(cells[i].X, cells[i].Y) + new Vector2(0.5f, 0.5f)) * grid.TileSize;
            route.Add(new EntityCoordinates(pen.Grid, middle));
        }

        route.Add(new EntityCoordinates(chosen, Vector2.Zero));
        return true;
    }

    private static void AddGoal(AnimalPen pen, Dictionary<(int X, int Y), EntityUid> goals, Vector2i tile, EntityUid target)
    {
        if (pen.Contains(tile))
            goals.TryAdd((tile.X, tile.Y), target);
    }

    /// <summary>
    /// Somebody can stand here: there is a floor, it is not space and no wall (or door) stands on it.
    /// </summary>
    private bool IsFree(EntityUid gridUid, MapGridComponent grid, (int X, int Y) cell, CollisionGroup mask, float wallArea)
    {
        var indices = new Vector2i(cell.X, cell.Y);
        var tileRef = _map.GetTileRef(gridUid, grid, indices);
        if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
            return false;

        return !IsWall(gridUid, grid, indices, mask, wallArea);
    }

    /// <summary>
    /// Something that is fixed to the tile and keeps the animal out covers most of it. Only fixed things count, the
    /// animal and other mobs standing on the tile are no walls (which is why this is not <c>TurfSystem.IsTileBlocked</c>).
    /// </summary>
    private bool IsWall(EntityUid gridUid, MapGridComponent grid, Vector2i indices, CollisionGroup mask, float wallArea)
    {
        var tileBox = Box2.FromDimensions(new Vector2(indices.X, indices.Y) * grid.TileSize, grid.TileSizeVector);
        var area = 0f;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp<FixturesComponent>(uid, out var fixtures))
                continue;

            // An anchored thing sits on the grid, so its transform is already in the coordinates of the grid.
            var xform = Transform(uid.Value);
            var shapeTransform = new Robust.Shared.Physics.Transform(xform.LocalPosition, (float) xform.LocalRotation.Theta);

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard || (fixture.CollisionLayer & (int) mask) == 0)
                    continue;

                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                {
                    var overlap = fixture.Shape.ComputeAABB(shapeTransform, i).Intersect(tileBox);
                    area += overlap.Width * overlap.Height;
                    if (area >= wallArea * grid.TileSize * grid.TileSize)
                        return true;
                }
            }
        }

        return false;
    }
}
