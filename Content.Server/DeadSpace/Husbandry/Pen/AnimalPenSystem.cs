// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Pen;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Pen;

/// <summary>
/// Works out the pen of an animal, see <see cref="AnimalPenComponent"/>: the walls round it, but no more than a
/// square round its home. The farm asks for it when nobody is watching, so it is not worked out every time: a pen
/// that is closed in is the same for every animal inside it, so it is worked out once and given to all of them, and
/// every result is kept for the cvar <c>husbandry.pen.cache_time</c> (walls and doors change, but not often).
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
    /// Pens that are closed in, by every tile of them: whoever stands on one of the tiles has that pen.
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
        Subs.CVar(_cfg, CCVars.HusbandryPenCacheTime, value => _cacheTime = TimeSpan.FromSeconds(value), true);
    }

    private void OnShutdown(Entity<AnimalPenComponent> animal, ref ComponentShutdown args)
    {
        _open.Remove(animal);
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
    /// The pen of the animal, or null if it is not on a grid or has nowhere to stand.
    /// </summary>
    public AnimalPen? GetPen(Entity<AnimalPenComponent> animal)
    {
        var now = _timing.CurTime;
        var xform = Transform(animal);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);

        if (_enclosures.TryGetValue((gridUid, tile), out var shared) && now - shared.Time < _cacheTime)
            return shared.Pen;

        if (_open.TryGetValue(animal, out var cached) &&
            now - cached.Time < _cacheTime &&
            cached.Pen.Grid == gridUid &&
            cached.Pen.Contains(tile))
        {
            return cached.Pen;
        }

        var mask = (CollisionGroup) _physics.GetHardCollision(animal).Mask;
        Func<(int X, int Y), bool> free = cell => IsFree(gridUid, grid, cell, mask, animal.Comp.WallArea);
        var cells = new HashSet<(int X, int Y)>();

        // First the whole enclosure: the tiles up to the walls, the same for every animal inside. If it is closed
        // there is nothing more to it.
        var tooBig = PenRules.Fill((tile.X, tile.Y), free, _ => true, cells, animal.Comp.MaxTiles);
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
            var comp = animal.Comp;
            if (comp.HomeGrid != gridUid ||
                Math.Abs(tile.X - comp.Home.X) > comp.HalfSize ||
                Math.Abs(tile.Y - comp.Home.Y) > comp.HalfSize)
            {
                comp.HomeGrid = gridUid;
                comp.Home = tile;
            }

            anchor = comp.Home;
            var home = anchor;
            var half = comp.HalfSize;
            Func<(int X, int Y), bool> inSquare = cell => Math.Abs(cell.X - home.X) <= half && Math.Abs(cell.Y - home.Y) <= half;

            var open = PenRules.Fill((tile.X, tile.Y), free, inSquare, cells, animal.Comp.MaxTiles);
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

    /// <summary>
    /// What lies in the pen: the things with the component that are on the floor of one of its tiles (not in a
    /// container).
    /// </summary>
    public void GetEntitiesInPen<T>(AnimalPen pen, HashSet<Entity<T>> result) where T : IComponent
    {
        result.Clear();
        if (!TryComp<MapGridComponent>(pen.Grid, out var grid))
            return;

        var centre = _map.GridTileToWorld(pen.Grid, grid, pen.Anchor);
        // The lookup gets a set of its own for each kind, so that nothing is allocated for every call.
        var found = PenLookupCache<T>.Set;
        found.Clear();
        _lookup.GetEntitiesInRange(centre, pen.Radius, found, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);

        foreach (var entity in found)
        {
            if (_container.IsEntityInContainer(entity))
                continue;

            var xform = Transform(entity);
            if (xform.GridUid == pen.Grid && pen.Contains(_map.CoordinatesToTile(pen.Grid, grid, xform.Coordinates)))
                result.Add(entity);
        }
    }

    private static class PenLookupCache<T> where T : IComponent
    {
        public static readonly HashSet<Entity<T>> Set = new();
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
