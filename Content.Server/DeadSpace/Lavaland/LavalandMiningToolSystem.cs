using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared.Maps;
using Content.Shared.Mining.Components;
using Content.Shared.Wall;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandMiningToolSystem : EntitySystem
{
    private static readonly Vector2i[] CardinalOffsets =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly LavalandSonicJackhammerSystem _sonicJackhammer = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly HashSet<EntityUid> _entities = new();
    private readonly List<CleaveTarget> _cleaveTargets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GatherableComponent, GatherableGatherAttemptEvent>(OnGatherAttempt);
    }

    private void OnGatherAttempt(Entity<GatherableComponent> ent, ref GatherableGatherAttemptEvent args)
    {
        _sonicJackhammer.TryHandleGatherAttempt(ent, ref args);
        if (args.Cancelled)
            return;

        if (!TryComp<LavalandMiningToolComponent>(args.Used, out var tool) ||
            !IsMineableWall(ent, args.Used))
        {
            return;
        }

        ApplyYieldMultiplier(ent.Owner, tool.YieldMultiplier);

        if (tool.CleaveTargets <= 0 ||
            !_turf.TryGetTileRef(Transform(ent.Owner).Coordinates, out var tileRef) ||
            !TryComp<MapGridComponent>(tileRef.Value.GridUid, out var grid))
        {
            return;
        }

        GatherCleaveTargets(tileRef.Value, grid, args.Used, args.User, tool);
    }

    private void GatherCleaveTargets(
        TileRef origin,
        MapGridComponent grid,
        EntityUid toolUid,
        EntityUid user,
        LavalandMiningToolComponent tool)
    {
        _cleaveTargets.Clear();

        foreach (var offset in CardinalOffsets)
        {
            if (!_map.TryGetTileRef(origin.GridUid, grid, origin.GridIndices + offset, out var tileRef) ||
                tileRef.Tile.IsEmpty ||
                !TryGetMineableWallOnTile(tileRef, toolUid, out var target, out var gatherable))
            {
                continue;
            }

            _cleaveTargets.Add(new CleaveTarget(target, gatherable, HasOre(target)));
        }

        if (_cleaveTargets.Count == 0)
            return;

        _cleaveTargets.Sort(static (a, b) => b.HasOre.CompareTo(a.HasOre));

        var gathered = 0;
        foreach (var target in _cleaveTargets)
        {
            if (gathered >= tool.CleaveTargets)
                break;

            if (TerminatingOrDeleted(target.Uid))
                continue;

            ApplyYieldMultiplier(target.Uid, tool.CleaveYieldMultiplier);
            _gatherable.Gather(target.Uid, user, target.Gatherable);
            gathered++;
        }
    }

    private bool IsMineableWall(Entity<GatherableComponent> ent, EntityUid toolUid)
    {
        return !TerminatingOrDeleted(ent.Owner) &&
               HasComp<WallComponent>(ent.Owner) &&
               !_whitelist.IsWhitelistFailOrNull(ent.Comp.ToolWhitelist, toolUid);
    }

    private bool TryGetMineableWallOnTile(
        TileRef tileRef,
        EntityUid toolUid,
        out EntityUid wall,
        out GatherableComponent gatherable)
    {
        _entities.Clear();
        _lookup.GetEntitiesInTile(tileRef, _entities, LookupFlags.Static);

        foreach (var uid in _entities)
        {
            if (TerminatingOrDeleted(uid) ||
                !HasComp<WallComponent>(uid) ||
                !TryComp<GatherableComponent>(uid, out var gatherableComp) ||
                _whitelist.IsWhitelistFailOrNull(gatherableComp.ToolWhitelist, toolUid))
            {
                continue;
            }

            wall = uid;
            gatherable = gatherableComp;
            return true;
        }

        wall = EntityUid.Invalid;
        gatherable = null!;
        return false;
    }

    private void ApplyYieldMultiplier(EntityUid uid, int multiplier)
    {
        if (multiplier <= 1 ||
            !TryComp<OreVeinComponent>(uid, out var ore) ||
            ore.CurrentOre == null)
        {
            return;
        }

        ore.YieldMultiplier = Math.Max(ore.YieldMultiplier, multiplier);
    }

    private bool HasOre(EntityUid uid)
    {
        return TryComp<OreVeinComponent>(uid, out var ore) && ore.CurrentOre != null;
    }

    private readonly record struct CleaveTarget(EntityUid Uid, GatherableComponent Gatherable, bool HasOre);
}
