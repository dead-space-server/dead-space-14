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
/// The pens of the animals, see <see cref="AnimalPenComponent"/>.
/// <list type="bullet">
/// <item>A pen is made by the <b>devices</b> in it, the anchored feeders and troughs (not by the animals): the tiles
/// round them that can be walked, up to <c>husbandry.pen.max_tiles</c> (300). If there are walls the pen is the whole
/// room, if there are none it is the 300 tiles nearest to the devices. Devices that stand in the same place are one
/// pen, a device that is taken away or put in does not change the pen. The pen is gone with its last device.</item>
/// <item>The tiles are worked out once and again every <c>husbandry.pen.refresh_time</c> (5 minutes), nothing is done
/// per animal: an animal finds its pen by the tile it stands on. When the tiles of two pens meet, they are one pen.</item>
/// <item>An animal answers <see cref="AnimalTargetCheckEvent"/> here: it does not pick a place, a food or a water that
/// lies outside its pen. The things in the pen are looked for here and kept for a few seconds for everybody, and the way
/// to them is a breadth-first search over the tiles of the pen (<see cref="TryGetRoute"/>), no pathfinder.</item>
/// <item>An animal that is in no pen looks for the way to the nearest one (<see cref="TryGetRouteToPen"/>): doors need no
/// care, a door that is open is a free tile, a door that is shut is a wall.</item>
/// </list>
/// </summary>
public sealed class AnimalPenSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    /// <summary>
    /// Whose tile it is, for every tile of every pen.
    /// </summary>
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), AnimalPen> _tiles = new();

    private readonly List<AnimalPen> _pens = new();
    private readonly Dictionary<EntityUid, AnimalPen> _deviceOf = new();

    /// <summary>
    /// Devices that are put down but have no pen yet. The pens are made when somebody asks for one, not while the map
    /// is loading and the walls are not there yet.
    /// </summary>
    private readonly HashSet<EntityUid> _unplaced = new();

    // Scratch, so that a flood allocates nothing but the pen.
    private readonly List<EntityUid> _batch = new();
    private readonly HashSet<(int X, int Y)> _cells = new();
    private readonly List<(int X, int Y)> _seeds = new();
    private readonly HashSet<AnimalPen> _overlapping = new();

    private TimeSpan _itemsCacheTime = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnimalPenComponent, AnimalTargetCheckEvent>(OnTargetCheck);

        SubscribeLocalEvent<AnimalFeederComponent, ComponentStartup>((uid, _, _) => Register(uid));
        SubscribeLocalEvent<AnimalFeederComponent, ComponentShutdown>((uid, _, _) => Unregister(uid));
        SubscribeLocalEvent<AnimalFeederComponent, AnchorStateChangedEvent>(OnFeederAnchorChanged);
        SubscribeLocalEvent<WaterSourceComponent, ComponentStartup>((uid, _, _) => Register(uid));
        SubscribeLocalEvent<WaterSourceComponent, ComponentShutdown>((uid, _, _) => Unregister(uid));
        SubscribeLocalEvent<WaterSourceComponent, AnchorStateChangedEvent>(OnTroughAnchorChanged);

        Subs.CVar(_cfg, CCVars.HusbandryPenItemsCacheTime, value => _itemsCacheTime = TimeSpan.FromSeconds(value), true);
    }

    #region Devices

    private void OnFeederAnchorChanged(Entity<AnimalFeederComponent> device, ref AnchorStateChangedEvent args)
    {
        OnAnchorChanged(device.Owner, args.Anchored);
    }

    private void OnTroughAnchorChanged(Entity<WaterSourceComponent> device, ref AnchorStateChangedEvent args)
    {
        OnAnchorChanged(device.Owner, args.Anchored);
    }

    private void OnAnchorChanged(EntityUid device, bool anchored)
    {
        if (anchored)
            Register(device);
        else
            Unregister(device);
    }

    private void Register(EntityUid device)
    {
        if (_deviceOf.ContainsKey(device) || TerminatingOrDeleted(device))
            return;

        var xform = Transform(device);
        if (xform.Anchored && xform.GridUid != null)
            _unplaced.Add(device);
    }

    private void Unregister(EntityUid device)
    {
        _unplaced.Remove(device);

        if (!_deviceOf.Remove(device, out var pen))
            return;

        // The pen does not change by itself, it lives while there is a device.
        pen.Devices.Remove(device);
        if (pen.Devices.Count == 0)
            Dissolve(pen);
    }

    /// <summary>
    /// Gives the devices that were put down a pen: the one that stands round them, or a new one.
    /// </summary>
    private void ResolveDevices()
    {
        if (_unplaced.Count == 0)
            return;

        _batch.Clear();
        _batch.AddRange(_unplaced);
        _unplaced.Clear();

        foreach (var device in _batch)
        {
            Place(device);
        }
    }

    private void Place(EntityUid device)
    {
        if (TerminatingOrDeleted(device) || _deviceOf.ContainsKey(device))
            return;

        var xform = Transform(device);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        var pen = TileOwner(gridUid, tile) ??
                  TileOwner(gridUid, tile + new Vector2i(1, 0)) ??
                  TileOwner(gridUid, tile + new Vector2i(-1, 0)) ??
                  TileOwner(gridUid, tile + new Vector2i(0, 1)) ??
                  TileOwner(gridUid, tile + new Vector2i(0, -1));

        if (pen != null)
        {
            // It stands in a pen that is there: it joins it, the pen itself stays as it is.
            pen.Devices.Add(device);
            _deviceOf[device] = pen;
            return;
        }

        pen = new AnimalPen(gridUid);
        pen.Devices.Add(device);
        _deviceOf[device] = pen;
        _pens.Add(pen);
        Rebuild(pen);
    }

    private AnimalPen? TileOwner(EntityUid grid, Vector2i tile)
    {
        return _tiles.TryGetValue((grid, tile), out var pen) && !pen.Removed ? pen : null;
    }

    private void Dissolve(AnimalPen pen)
    {
        pen.Removed = true;
        RemoveTiles(pen);
        _pens.Remove(pen);

        foreach (var device in pen.Devices)
        {
            _deviceOf.Remove(device);
        }

        pen.Devices.Clear();
    }

    private void RemoveTiles(AnimalPen pen)
    {
        foreach (var tile in pen.Tiles)
        {
            if (_tiles.TryGetValue((pen.Grid, tile), out var owner) && owner == pen)
                _tiles.Remove((pen.Grid, tile));
        }

        pen.Tiles = new HashSet<Vector2i>();
    }

    private void Merge(AnimalPen pen, AnimalPen other)
    {
        pen.Devices.UnionWith(other.Devices);
        foreach (var device in other.Devices)
        {
            _deviceOf[device] = pen;
        }

        other.Devices.Clear();
        other.Removed = true;
        other.MergedInto = pen;
        RemoveTiles(other);
        _pens.Remove(other);
    }

    #endregion

    #region Tiles

    /// <summary>
    /// Works the tiles of the pen out again: a flood from the devices over the tiles that can be walked, cut at the
    /// limit. A pen that the flood runs into is taken in, the devices of both make one pen.
    /// </summary>
    private void Rebuild(AnimalPen pen)
    {
        if (!TryComp<MapGridComponent>(pen.Grid, out var grid))
        {
            Dissolve(pen);
            return;
        }

        var maxTiles = Math.Max(1, _cfg.GetCVar(CCVars.HusbandryPenMaxTiles));
        var wallArea = _cfg.GetCVar(CCVars.HusbandryPenWallArea);
        Func<(int X, int Y), bool> free = cell => IsFree(pen.Grid, grid, cell, CollisionGroup.MobMask, wallArea);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            CollectSeeds(pen, grid, free);
            if (pen.Devices.Count == 0)
            {
                Dissolve(pen);
                return;
            }

            pen.Truncated = PenRules.Flood(_seeds, free, _cells, maxTiles);

            _overlapping.Clear();
            foreach (var (x, y) in _cells)
            {
                if (_tiles.TryGetValue((pen.Grid, new Vector2i(x, y)), out var other) && other != pen && !other.Removed)
                    _overlapping.Add(other);
            }

            if (_overlapping.Count == 0)
                break;

            foreach (var other in _overlapping)
            {
                Merge(pen, other);
            }
        }

        RemoveTiles(pen);

        var tiles = new HashSet<Vector2i>(_cells.Count);
        long sumX = 0;
        long sumY = 0;
        foreach (var (x, y) in _cells)
        {
            var tile = new Vector2i(x, y);
            tiles.Add(tile);
            _tiles[(pen.Grid, tile)] = pen;
            sumX += x;
            sumY += y;
        }

        pen.Tiles = tiles;
        pen.Found.Clear();

        if (tiles.Count > 0)
        {
            pen.Anchor = new Vector2i((int) Math.Round((double) sumX / tiles.Count), (int) Math.Round((double) sumY / tiles.Count));

            var farthest = 0f;
            foreach (var tile in tiles)
            {
                farthest = Math.Max(farthest, Vector2.Distance(new Vector2(tile.X, tile.Y), new Vector2(pen.Anchor.X, pen.Anchor.Y)));
            }

            pen.Radius = (farthest + 1.5f) * grid.TileSize;
        }

        // Not all the pens at the same moment.
        var refresh = Math.Max(1f, _cfg.GetCVar(CCVars.HusbandryPenRefreshTime));
        pen.RefreshAt = _timing.CurTime + TimeSpan.FromSeconds(refresh * (1f + _random.NextFloat(0f, 0.2f)));
    }

    /// <summary>
    /// Where the flood starts: the tile of every device, or, if the device itself blocks it, the free tiles next to it.
    /// A device that is gone or not fixed any more is dropped.
    /// </summary>
    private void CollectSeeds(AnimalPen pen, MapGridComponent grid, Func<(int X, int Y), bool> free)
    {
        _seeds.Clear();
        List<EntityUid>? gone = null;

        foreach (var device in pen.Devices)
        {
            if (TerminatingOrDeleted(device) || Transform(device) is not { Anchored: true } xform || xform.GridUid != pen.Grid)
            {
                (gone ??= new()).Add(device);
                continue;
            }

            var tile = _map.CoordinatesToTile(pen.Grid, grid, xform.Coordinates);
            var cell = (tile.X, tile.Y);
            if (free(cell))
            {
                _seeds.Add(cell);
                continue;
            }

            foreach (var next in new[] { (cell.X + 1, cell.Y), (cell.X - 1, cell.Y), (cell.X, cell.Y + 1), (cell.X, cell.Y - 1) })
            {
                if (free(next))
                    _seeds.Add(next);
            }
        }

        if (gone == null)
            return;

        foreach (var device in gone)
        {
            pen.Devices.Remove(device);
            _deviceOf.Remove(device);
        }
    }

    /// <summary>
    /// The pen as it is now: what it was merged into, refreshed if its time is up. Null if it is gone.
    /// </summary>
    private AnimalPen? Live(AnimalPen? pen)
    {
        pen = Follow(pen);
        if (pen == null)
            return null;

        if (_timing.CurTime >= pen.RefreshAt)
        {
            Rebuild(pen);
            pen = Follow(pen);
        }

        return pen;
    }

    private static AnimalPen? Follow(AnimalPen? pen)
    {
        while (pen is { Removed: true, MergedInto: { } merged })
        {
            pen = merged;
        }

        return pen == null || pen.Removed ? null : pen;
    }

    /// <summary>
    /// The pen that has this tile, if there is one.
    /// </summary>
    public AnimalPen? PenAt(EntityUid grid, Vector2i tile)
    {
        ResolveDevices();
        if (!_tiles.TryGetValue((grid, tile), out var pen))
            return null;

        // The pen may be worked out again just now, and the tile may not be a part of it any more.
        var live = Live(pen);
        return live != null && live.Contains(tile) ? live : null;
    }

    #endregion

    /// <summary>
    /// A place, a food or a water that is not in the pen is not allowed: the animal does not go there. A thing that
    /// stands next to the pen (a feeder in the wall, say) is, it is reached from the pen. An animal without a pen is
    /// not held.
    /// </summary>
    private void OnTargetCheck(Entity<AnimalPenComponent> animal, ref AnimalTargetCheckEvent args)
    {
        if (!args.Allowed || GetPen(animal) is not { } pen || !TryComp<MapGridComponent>(pen.Grid, out var grid))
            return;

        var tile = _map.TileIndicesFor(pen.Grid, grid, args.Target);
        if (!pen.Contains(tile) && !(args.Reach > 0f && TouchesPen(pen, tile)))
            args.Allowed = false;
    }

    /// <summary>
    /// The pen of the animal: the one it stands in, or else the one it was taken out of a short while ago. Null if it is
    /// in no pen. Nothing is worked out for the animal, it is a lookup by its tile.
    /// </summary>
    public AnimalPen? GetPen(Entity<AnimalPenComponent> animal)
    {
        var comp = animal.Comp;
        var xform = Transform(animal);

        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var tile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            if (PenAt(gridUid, tile) is { } here)
            {
                comp.Pen = here;
                comp.OutsideSince = null;
                return here;
            }
        }

        // In no pen. For a moment (it is pushed, pulled, it is crossing something) that does not change anything, but an
        // animal that was carried away and left there forgets its pen after a while.
        var own = Live(comp.Pen);
        if (own == null)
        {
            comp.Pen = null;
            comp.OutsideSince = null;
            return null;
        }

        comp.Pen = own;
        var now = _timing.CurTime;
        comp.OutsideSince ??= now;
        if (now - comp.OutsideSince >= comp.RehomeTime)
        {
            comp.Pen = null;
            comp.OutsideSince = null;
            return null;
        }

        return own;
    }

    /// <summary>
    /// Whether the animal stands in the tiles of its pen.
    /// </summary>
    public bool IsInPen(Entity<AnimalPenComponent> animal, AnimalPen pen)
    {
        var xform = Transform(animal);
        return xform.GridUid == pen.Grid &&
               TryComp<MapGridComponent>(pen.Grid, out var grid) &&
               pen.Contains(_map.CoordinatesToTile(pen.Grid, grid, xform.Coordinates));
    }

    private static bool TouchesPen(AnimalPen pen, Vector2i tile)
    {
        return pen.Contains(tile + new Vector2i(1, 0)) ||
               pen.Contains(tile + new Vector2i(-1, 0)) ||
               pen.Contains(tile + new Vector2i(0, 1)) ||
               pen.Contains(tile + new Vector2i(0, -1));
    }

    /// <summary>
    /// The feeders or the troughs of the pen, from the list of the pen: no lookup.
    /// </summary>
    public void GetDevices<T>(AnimalPen pen, HashSet<Entity<T>> result) where T : IComponent
    {
        result.Clear();
        foreach (var device in pen.Devices)
        {
            if (!TerminatingOrDeleted(device) && TryComp<T>(device, out var comp))
                result.Add((device, comp));
        }
    }

    /// <summary>
    /// What lies in the pen: the things with the component that are on the floor of one of its tiles (not in a
    /// container). With <paramref name="adjacent"/> also the things on a tile next to the pen. The lookup of an area is
    /// made once for everybody and kept for <c>husbandry.pen.items_cache_time</c>; what was found is checked again each
    /// time (it may be picked up or gone by now).
    /// </summary>
    public void GetEntitiesInPen<T>(AnimalPen pen, HashSet<Entity<T>> result, bool adjacent = false) where T : IComponent
    {
        result.Clear();
        if (pen.Removed || !TryComp<MapGridComponent>(pen.Grid, out var grid))
            return;

        var now = _timing.CurTime;
        var key = (typeof(T), adjacent);
        if (!pen.Found.TryGetValue(key, out var found))
        {
            found = new PenFound();
            pen.Found[key] = found;
        }

        if (!found.Fetched || now - found.Time >= _itemsCacheTime)
        {
            found.Fetched = true;
            found.Time = now;
            found.Items.Clear();

            var centre = _map.GridTileToWorld(pen.Grid, grid, pen.Anchor);
            // The lookup gets a set of its own for each kind, so that nothing is allocated for every call.
            var set = PenLookupCache<T>.Set;
            set.Clear();
            _lookup.GetEntitiesInRange(centre, pen.Radius + (adjacent ? grid.TileSize : 0f), set, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);

            foreach (var entity in set)
            {
                if (_container.IsEntityInContainer(entity))
                    continue;

                var xform = Transform(entity);
                if (xform.GridUid != pen.Grid)
                    continue;

                var tile = _map.CoordinatesToTile(pen.Grid, grid, xform.Coordinates);
                if (pen.Contains(tile) || (adjacent && TouchesPen(pen, tile)))
                    found.Items.Add(entity);
            }
        }

        foreach (var uid in found.Items)
        {
            if (TerminatingOrDeleted(uid) || _container.IsEntityInContainer(uid) || !TryComp<T>(uid, out var comp))
                continue;

            result.Add((uid, comp));
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
