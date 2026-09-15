using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared.Maps;
using Content.Shared.Mining.Components;
using Content.Shared.Timing;
using Content.Shared.Wall;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandSonicJackhammerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    private readonly HashSet<EntityUid> _entities = new();
    private readonly List<ActiveBurrow> _activeBurrows = new();

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        for (var i = _activeBurrows.Count - 1; i >= 0; i--)
        {
            var burrow = _activeBurrows[i];
            if (burrow.NextStep > curTime)
                continue;

            if (StepBurrow(ref burrow, curTime))
                _activeBurrows[i] = burrow;
            else
                _activeBurrows.RemoveAt(i);
        }
    }

    public void TryHandleGatherAttempt(Entity<GatherableComponent> ent, ref GatherableGatherAttemptEvent args)
    {
        if (!TryComp<LavalandSonicJackhammerComponent>(args.Used, out var jackhammer) ||
            !IsMineableWall(ent, args.Used))
        {
            return;
        }

        if (!TryComp<UseDelayComponent>(args.Used, out var useDelay) ||
            !_useDelay.TryResetDelay((args.Used, useDelay), checkDelayed: true))
        {
            return;
        }

        if (!_turf.TryGetTileRef(Transform(ent.Owner).Coordinates, out var origin) ||
            !TryComp<MapGridComponent>(origin.Value.GridUid, out _))
        {
            return;
        }

        args.Cancel();

        var direction = GetBurrowDirection(args.User, origin.Value, args.ClickLocation);
        _activeBurrows.Add(new ActiveBurrow(
            args.Used,
            args.User,
            origin.Value.GridUid,
            origin.Value.GridIndices,
            direction,
            Math.Max(1, jackhammer.BurrowRange),
            _timing.CurTime,
            TimeSpan.FromSeconds(Math.Max(0.01f, jackhammer.BurrowStepDelay)),
            Math.Max(1, jackhammer.BurrowYieldMultiplier),
            jackhammer.BurrowPulsePrototype,
            jackhammer.BurrowSound));
    }

    private bool StepBurrow(ref ActiveBurrow burrow, TimeSpan curTime)
    {
        if (burrow.Remaining <= 0 ||
            TerminatingOrDeleted(burrow.Tool) ||
            !TryComp<MapGridComponent>(burrow.GridUid, out var grid) ||
            !_map.TryGetTileRef(burrow.GridUid, grid, burrow.NextTile, out var tileRef) ||
            tileRef.Tile.IsEmpty ||
            !TryGetMineableWallOnTile(tileRef, burrow.Tool, out var wall, out var gatherable))
        {
            return false;
        }

        ApplyYieldMultiplier(wall, burrow.YieldMultiplier);

        var coords = _turf.GetTileCenter(tileRef);
        Spawn(burrow.PulsePrototype, coords);

        if (burrow.Step % 2 == 0)
            _audio.PlayPvs(burrow.Sound, coords);

        EntityUid? gatherer = TerminatingOrDeleted(burrow.User) ? null : burrow.User;
        _gatherable.Gather(wall, gatherer, gatherable);

        burrow.NextTile += burrow.Direction;
        burrow.Remaining--;
        burrow.Step++;
        burrow.NextStep = curTime + burrow.StepDelay;

        return burrow.Remaining > 0;
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

    private Vector2i GetBurrowDirection(EntityUid user, TileRef origin, EntityCoordinates clickLocation)
    {
        if (_turf.TryGetTileRef(Transform(user).Coordinates, out var userTile) &&
            userTile.Value.GridUid == origin.GridUid)
        {
            var delta = origin.GridIndices - userTile.Value.GridIndices;
            if (delta != Vector2i.Zero)
                return CardinalFromDelta(delta);
        }

        var userCoords = _transform.GetMapCoordinates(user);
        var clickCoords = _transform.ToMapCoordinates(clickLocation);
        if (userCoords.MapId == clickCoords.MapId)
        {
            var delta = clickCoords.Position - userCoords.Position;
            if (delta.LengthSquared() > 0.001f)
                return CardinalFromDelta(delta);
        }

        return new Vector2i(0, 1);
    }

    private static Vector2i CardinalFromDelta(Vector2i delta)
    {
        return Math.Abs(delta.X) >= Math.Abs(delta.Y)
            ? new Vector2i(Math.Sign(delta.X), 0)
            : new Vector2i(0, Math.Sign(delta.Y));
    }

    private static Vector2i CardinalFromDelta(Vector2 delta)
    {
        return Math.Abs(delta.X) >= Math.Abs(delta.Y)
            ? new Vector2i(Math.Sign(delta.X), 0)
            : new Vector2i(0, Math.Sign(delta.Y));
    }

    private struct ActiveBurrow(
        EntityUid tool,
        EntityUid user,
        EntityUid gridUid,
        Vector2i nextTile,
        Vector2i direction,
        int remaining,
        TimeSpan nextStep,
        TimeSpan stepDelay,
        int yieldMultiplier,
        string pulsePrototype,
        SoundSpecifier sound)
    {
        public EntityUid Tool = tool;
        public EntityUid User = user;
        public EntityUid GridUid = gridUid;
        public Vector2i NextTile = nextTile;
        public Vector2i Direction = direction;
        public int Remaining = remaining;
        public TimeSpan NextStep = nextStep;
        public TimeSpan StepDelay = stepDelay;
        public int YieldMultiplier = yieldMultiplier;
        public string PulsePrototype = pulsePrototype;
        public SoundSpecifier Sound = sound;
        public int Step;
    }
}
