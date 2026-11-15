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
/// <item>An animal that is in no pen walks straight towards the middle of its own pen, or of the nearest one
/// (<see cref="TryGetRouteToPen"/>): no search for a way, if a wall or a shut door is in the way it stops there.</item>
/// </list>
/// </summary>
public sealed partial class AnimalPenSystem : EntitySystem
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
}
