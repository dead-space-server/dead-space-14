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
/// What lies in a pen: the devices of it, and the things on its floor, looked up once for everybody.
/// </summary>
public sealed partial class AnimalPenSystem
{
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
}
