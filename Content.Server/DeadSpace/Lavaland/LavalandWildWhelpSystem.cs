// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Wild ash whelps (<see cref="LavalandWildWhelpConfig"/>): now and then, and from hidden nests, unkillable whelps drop
/// from the sky next to a player, spit a line of fire and fly away.
/// </summary>
public sealed class LavalandWildWhelpSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly TimeSpan NestCheckInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, TimeSpan> _nextRandomVisit = new();
    private readonly Dictionary<EntityUid, TimeSpan> _playerCooldown = new();
    private TimeSpan _nextNestCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandWildWhelpTestComponent, MapInitEvent>(OnTestMapInit);
    }

    private void OnTestMapInit(Entity<LavalandWildWhelpTestComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        var config = TryComp<LavalandMapComponent>(xform.MapUid, out var lavaland) && TryGetConfig(lavaland, out var mapConfig)
            ? mapConfig
            : new LavalandWildWhelpConfig();

        if (FindNearestPlayer(xform, ent.Comp.Range) is { } player)
            Visit(player, config, _timing.CurTime);

        QueueDel(ent);
    }

    private EntityUid? FindNearestPlayer(TransformComponent origin, float range)
    {
        var position = _transform.GetWorldPosition(origin);
        EntityUid? best = null;
        var bestDistance = range;
        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var player, out _, out var mobState, out var xform))
        {
            if (xform.MapID != origin.MapID || HasComp<GhostComponent>(player) || !_mobState.IsAlive(player, mobState))
                continue;

            var distance = (_transform.GetWorldPosition(xform) - position).Length();
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = player;
        }

        return best;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateWhelps(frameTime, now);
        UpdateRandomVisits(now);

        if (now >= _nextNestCheck)
        {
            _nextNestCheck = now + NestCheckInterval;
            UpdateNests(now);
        }
    }

    #region Spawning

    private void UpdateRandomVisits(TimeSpan now)
    {
        var maps = EntityQueryEnumerator<LavalandMapComponent>();
        while (maps.MoveNext(out var map, out var lavaland))
        {
            if (!TryGetConfig(lavaland, out var config))
                continue;

            if (!_nextRandomVisit.TryGetValue(map, out var next))
            {
                _nextRandomVisit[map] = now + config.RandomInterval;
                continue;
            }

            if (now < next)
                continue;

            _nextRandomVisit[map] = now + config.RandomInterval;
            if (!_random.Prob(config.RandomChance))
                continue;

            var candidates = new List<EntityUid>();
            var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
            while (players.MoveNext(out var player, out _, out var mobState, out var xform))
            {
                if (xform.MapUid == map && IsEligible(player, mobState, xform, now))
                    candidates.Add(player);
            }

            if (candidates.Count > 0)
                Visit(_random.Pick(candidates), config, now);
        }
    }

    private void UpdateNests(TimeSpan now)
    {
        var nests = EntityQueryEnumerator<LavalandWildWhelpNestComponent, TransformComponent>();
        while (nests.MoveNext(out _, out var nest, out var nestXform))
        {
            if (now < nest.NextTrigger ||
                !TryComp<LavalandMapComponent>(nestXform.MapUid, out var lavaland) ||
                !TryGetConfig(lavaland, out var config))
            {
                continue;
            }

            var nestPos = _transform.GetWorldPosition(nestXform);
            var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
            while (players.MoveNext(out var player, out _, out var mobState, out var xform))
            {
                if (xform.MapID != nestXform.MapID ||
                    (_transform.GetWorldPosition(xform) - nestPos).Length() > nest.Radius ||
                    !IsEligible(player, mobState, xform, now))
                {
                    continue;
                }

                nest.NextTrigger = now + nest.Cooldown;
                Visit(player, config, now);
                break;
            }
        }
    }

    private bool TryGetConfig(LavalandMapComponent lavaland, out LavalandWildWhelpConfig config)
    {
        config = default!;
        if (lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            !planet.WildWhelps.Enabled)
        {
            return false;
        }

        config = planet.WildWhelps;
        return true;
    }

    /// <summary>Alive, out in the open on the surface (not at the outpost, in an arena or with the Celestial), not visited lately.</summary>
    private bool IsEligible(EntityUid player, MobStateComponent mobState, TransformComponent xform, TimeSpan now)
    {
        if (HasComp<GhostComponent>(player) || !_mobState.IsAlive(player, mobState) ||
            _playerCooldown.TryGetValue(player, out var until) && now < until)
        {
            return false;
        }

        if (xform.GridUid is { } grid &&
            (HasComp<LavalandOutpostComponent>(grid) || HasComp<LavalandBossArenaComponent>(grid)))
        {
            return false;
        }

        // Not by the outpost either: from just outside they flew in and set people alight at the base itself.
        return !NearOutpost(_transform.GetMapCoordinates(xform), OutpostPlayerMargin);
    }

    /// <summary>Players this close to the outpost (tiles from its edge) get no visits.</summary>
    private const float OutpostPlayerMargin = 15f;

    /// <summary>Whelps land and spit fire no closer than this to the outpost (tiles from its edge).</summary>
    private const float OutpostFireMargin = 3f;

    private bool NearOutpost(Robust.Shared.Map.MapCoordinates coords, float margin)
    {
        var outposts = EntityQueryEnumerator<LavalandOutpostComponent, MapGridComponent, TransformComponent>();
        while (outposts.MoveNext(out _, out _, out var grid, out var xform))
        {
            if (xform.MapID != coords.MapId)
                continue;

            var box = _transform.GetWorldMatrix(xform).TransformBox(grid.LocalAABB).Enlarged(margin);
            if (box.Contains(coords.Position))
                return true;
        }

        return false;
    }

    private void Visit(EntityUid player, LavalandWildWhelpConfig config, TimeSpan now)
    {
        _playerCooldown[player] = now + config.PlayerCooldown;

        var xform = Transform(player);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var count = _random.Next(Math.Max(1, config.MinWhelps), Math.Max(config.MinWhelps, config.MaxWhelps) + 1);
        var playerPos = _transform.GetWorldPosition(xform);
        for (var i = 0; i < count; i++)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var offset = _random.NextAngle().ToVec() *
                             _random.NextFloat(config.SpawnDistanceMin, MathF.Max(config.SpawnDistanceMin, config.SpawnDistanceMax));
                var coords = _transform.ToCoordinates(gridUid, new Robust.Shared.Map.MapCoordinates(playerPos + offset, xform.MapID));
                var tile = _map.LocalToTile(gridUid, grid, coords);
                if (!IsFreeTile(gridUid, grid, tile) ||
                    NearOutpost(new Robust.Shared.Map.MapCoordinates(playerPos + offset, xform.MapID), OutpostFireMargin))
                {
                    continue;
                }

                SpawnWhelp(config.Prototype, player, gridUid, grid, tile, now);
                break;
            }
        }
    }

    private bool IsFreeTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        return _map.TryGetTileRef(gridUid, grid, tile, out var tileRef) &&
               !tileRef.Tile.IsEmpty &&
               !_turf.IsSpace(tileRef) &&
               !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable);
    }

    private void SpawnWhelp(EntProtoId prototype, EntityUid target, EntityUid gridUid, MapGridComponent grid, Vector2i tile, TimeSpan now)
    {
        var coords = _map.GridTileToLocal(gridUid, grid, tile);
        var whelp = Spawn(prototype, coords);
        var comp = EnsureComp<LavalandWildWhelpComponent>(whelp);
        comp.Target = target;
        comp.Stage = LavalandWildWhelpStage.Dropping;
        comp.StageEnd = now + TimeSpan.FromSeconds(comp.DropTime);

        var drop = EnsureComp<LavalandSkyDropComponent>(whelp);
        drop.Start = now;
        drop.Duration = comp.DropTime;
        drop.Height = comp.DropHeight;
        drop.Rising = false;
        Dirty(whelp, drop);

        Spawn(comp.LandingMarkerPrototype, coords);
    }

    #endregion

    #region Whelp

    private void UpdateWhelps(float frameTime, TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandWildWhelpComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var whelp, out var xform))
        {
            if (whelp.Stage == LavalandWildWhelpStage.Fleeing)
                Flee(uid, whelp, xform, frameTime);

            if (now < whelp.StageEnd)
                continue;

            switch (whelp.Stage)
            {
                case LavalandWildWhelpStage.Dropping:
                    Land(uid, whelp, xform, now);
                    break;
                case LavalandWildWhelpStage.Aiming:
                    SpitFire(uid, whelp, xform, now);
                    break;
                case LavalandWildWhelpStage.Fleeing:
                    StartRising(uid, whelp, now);
                    break;
                case LavalandWildWhelpStage.Rising:
                    QueueDel(uid);
                    break;
            }
        }
    }

    private void Land(EntityUid uid, LavalandWildWhelpComponent whelp, TransformComponent xform, TimeSpan now)
    {
        _audio.PlayPvs(whelp.LandSound, uid);
        whelp.Stage = LavalandWildWhelpStage.Aiming;
        whelp.StageEnd = now + whelp.AimTime;

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        var targetTile = whelp.Target is { } target && !TerminatingOrDeleted(target)
            ? _map.LocalToTile(gridUid, grid, Transform(target).Coordinates)
            : origin + new Vector2i(1, 0);

        var direction = new Vector2i(Math.Sign(targetTile.X - origin.X), Math.Sign(targetTile.Y - origin.Y));
        if (direction == Vector2i.Zero)
            direction = new Vector2i(0, -1);
        whelp.Direction = direction;
        _transform.SetLocalRotation(uid, new Vector2(direction.X, direction.Y).ToWorldAngle());

        var perpendicular = new Vector2i(-direction.Y, direction.X);
        for (var step = 1; step <= whelp.FireRange; step++)
        {
            var forward = origin + direction * step;
            foreach (var tile in new[] { forward, forward + perpendicular, forward - perpendicular })
            {
                if (whelp.Tiles.Contains(tile) || !IsFreeTile(gridUid, grid, tile))
                    continue;

                whelp.Tiles.Add(tile);
                whelp.Telegraphs.Add(Spawn(whelp.TelegraphPrototype, _map.GridTileToLocal(gridUid, grid, tile)));
            }
        }
    }

    private void SpitFire(EntityUid uid, LavalandWildWhelpComponent whelp, TransformComponent xform, TimeSpan now)
    {
        foreach (var telegraph in whelp.Telegraphs)
        {
            if (!TerminatingOrDeleted(telegraph))
                QueueDel(telegraph);
        }
        whelp.Telegraphs.Clear();

        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            foreach (var tile in whelp.Tiles)
            {
                // The target ran to the outpost meanwhile: no fire at the base.
                var at = _map.GridTileToLocal(gridUid, grid, tile);
                if (NearOutpost(_transform.ToMapCoordinates(at), OutpostFireMargin))
                    continue;

                Spawn(whelp.FirePrototype, at);
            }
        }

        whelp.Tiles.Clear();
        _audio.PlayPvs(whelp.FireSound, uid, AudioParams.Default.WithVolume(1f));
        whelp.Stage = LavalandWildWhelpStage.Fleeing;
        whelp.StageEnd = now + whelp.FleeTime;
    }

    /// <summary>Runs straight away from where it spat.</summary>
    private void Flee(EntityUid uid, LavalandWildWhelpComponent whelp, TransformComponent xform, float frameTime)
    {
        var away = new Vector2(-whelp.Direction.X, -whelp.Direction.Y);
        if (away == Vector2.Zero)
            return;

        away = Vector2.Normalize(away);
        _transform.SetWorldPosition(uid, _transform.GetWorldPosition(xform) + away * whelp.FleeSpeed * frameTime);
        _transform.SetLocalRotation(uid, away.ToWorldAngle());
    }

    private void StartRising(EntityUid uid, LavalandWildWhelpComponent whelp, TimeSpan now)
    {
        whelp.Stage = LavalandWildWhelpStage.Rising;
        whelp.StageEnd = now + TimeSpan.FromSeconds(whelp.RiseTime);

        // Straight up on the screen: the sprite offset follows the entity rotation.
        _transform.SetLocalRotation(uid, Angle.Zero);
        var drop = EnsureComp<LavalandSkyDropComponent>(uid);
        drop.Start = now;
        drop.Duration = whelp.RiseTime;
        drop.Height = whelp.DropHeight;
        drop.Rising = true;
        Dirty(uid, drop);
    }

    #endregion
}
