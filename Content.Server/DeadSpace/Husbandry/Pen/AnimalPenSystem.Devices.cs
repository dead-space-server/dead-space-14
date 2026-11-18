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
/// The devices of the pens: the feeders and the troughs that make a pen, and which pen they are in.
/// </summary>
public sealed partial class AnimalPenSystem
{
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

}
