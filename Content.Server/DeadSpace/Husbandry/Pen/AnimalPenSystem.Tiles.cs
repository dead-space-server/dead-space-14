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
/// The tiles of the pens: the flood from the devices, the refresh, the merge, and what keeps an animal out of a tile.
/// </summary>
public sealed partial class AnimalPenSystem
{
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
