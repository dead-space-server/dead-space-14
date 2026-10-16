// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Physics.Components;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Server.NPC.Systems;
using Content.Shared.NPC;
using Content.Server.Shuttles.Components;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Tiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Periodic fauna raids on the salvage outpost, independent of any machine. Settings live in the lavaland
/// planet prototype (<see cref="LavalandFaunaRaidConfig"/>).
/// </summary>
public sealed class LavalandFaunaRaidSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly NPCUseActionOnTargetSystem _useAction = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;

    private const string RaidCompound = "LavalandRaidHostileCompound";

    /// <summary>Raid fauna find the players of the outpost from this far.</summary>
    private const float RaidVision = 60f;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandFaunaRaidComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<LavalandFaunaRaidComponent> ent, ref ComponentShutdown args)
    {
        EndRaid(ent, TerminatingOrDeleted(ent) ? MapId.Nullspace : Transform(ent).MapID);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateRaiders();

        var now = _timing.CurTime;
        var maps = EntityQueryEnumerator<LavalandMapComponent, TransformComponent>();
        while (maps.MoveNext(out var mapUid, out var lavaland, out var xform))
        {
            if (lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) || !planet.FaunaRaid.Enabled)
                continue;

            var config = planet.FaunaRaid;
            if (!TryComp<LavalandFaunaRaidComponent>(mapUid, out var raid))
            {
                raid = AddComp<LavalandFaunaRaidComponent>(mapUid);
                raid.Chance = config.BaseChance;
                raid.NextCheck = now + config.CheckInterval;
            }

            UpdateRaid((mapUid, raid), config, xform.MapID, now);
        }
    }

    private void UpdateRaid(Entity<LavalandFaunaRaidComponent> ent, LavalandFaunaRaidConfig config, MapId mapId, TimeSpan now)
    {
        var raid = ent.Comp;
        switch (raid.Stage)
        {
            case LavalandFaunaRaidStage.Idle:
                if (now < raid.NextCheck)
                    return;

                raid.NextCheck = now + config.CheckInterval;
                if (!TryFindOutpost(ent, out _))
                    return;

                if (!_random.Prob(Math.Clamp(raid.Chance, 0f, 1f)))
                {
                    // Every miss makes the next raid more likely.
                    var min = config.ChanceIncreaseMin;
                    var max = Math.Max(min, config.ChanceIncreaseMax);
                    raid.Chance = Math.Min(1f, raid.Chance + _random.NextFloat(min, max));
                    return;
                }

                StartWarning(ent, config, mapId, now);
                break;

            case LavalandFaunaRaidStage.Warning when now >= raid.StageEnd:
                raid.Stage = LavalandFaunaRaidStage.Active;
                raid.StageEnd = now + config.Duration;
                raid.NextTimerUpdate = TimeSpan.Zero;
                raid.DefenseCheckAt = now + config.DefenseGrace;
                SpawnTendrils(ent, config);
                var filter = RaidFilter(mapId);
                _chat.DispatchFilteredAnnouncement(filter, Loc.GetString("lavaland-raid-active"),
                    playSound: false,
                    colorOverride: Color.CornflowerBlue);
                if (config.Music.Count > 0)
                    raid.MusicStream = _audio.PlayGlobal(_random.Pick(config.Music), filter, false)?.Entity;
                break;

            case LavalandFaunaRaidStage.Active when now >= raid.StageEnd:
                EndRaid(ent, mapId);
                return;

            case LavalandFaunaRaidStage.Active when raid.DefenseCheckAt is { } checkAt && now >= checkAt:
                raid.DefenseCheckAt = null;
                if (TryFindOutpost(ent, out var outpost) && !AnyDefender(outpost))
                    RuinOutpost(outpost, config, mapId);
                break;
        }

        if (raid.Stage == LavalandFaunaRaidStage.Idle || now < raid.NextTimerUpdate)
            return;

        raid.NextTimerUpdate = now + TimeSpan.FromSeconds(1);
        var seconds = Math.Max(0, (int) Math.Ceiling((raid.StageEnd - now).TotalSeconds));
        RaiseNetworkEvent(new LavalandFaunaRaidTimerEvent(seconds, raid.Stage == LavalandFaunaRaidStage.Warning),
            RaidFilter(mapId));
    }

    private void StartWarning(Entity<LavalandFaunaRaidComponent> ent, LavalandFaunaRaidConfig config, MapId mapId, TimeSpan now)
    {
        var raid = ent.Comp;
        raid.Chance = config.BaseChance;
        raid.Stage = LavalandFaunaRaidStage.Warning;
        raid.StageEnd = now + config.WarningDuration;
        raid.NextTimerUpdate = TimeSpan.Zero;
        _chat.DispatchFilteredAnnouncement(RaidFilter(mapId),
            Loc.GetString("lavaland-raid-warning"),
            playSound: true,
            announcementSound: config.WarningSound,
            colorOverride: Color.Red);
    }

    /// <summary>
    /// Starts a raid on the lavaland map right now (guaranteed raids of the bluespace well).
    /// Does nothing if a raid is already coming or going on. The regular schedule is not touched.
    /// </summary>
    /// <summary>A raid on the outpost of this lavaland is announced or going on.</summary>
    public bool IsRaidRunning(EntityUid mapUid)
    {
        return TryComp<LavalandFaunaRaidComponent>(mapUid, out var raid) && raid.Stage != LavalandFaunaRaidStage.Idle;
    }

    public void ForceRaid(EntityUid mapUid)
    {
        if (!TryComp<LavalandMapComponent>(mapUid, out var lavaland) ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            !planet.FaunaRaid.Enabled || !TryFindOutpost(mapUid, out _))
        {
            return;
        }

        var raid = EnsureComp<LavalandFaunaRaidComponent>(mapUid);
        if (raid.Stage != LavalandFaunaRaidStage.Idle)
            return;

        var now = _timing.CurTime;
        if (raid.NextCheck == TimeSpan.Zero)
            raid.NextCheck = now + planet.FaunaRaid.CheckInterval;
        StartWarning((mapUid, raid), planet.FaunaRaid, Transform(mapUid).MapID, now);
    }

    private void EndRaid(Entity<LavalandFaunaRaidComponent> ent, MapId mapId)
    {
        foreach (var spawned in ent.Comp.Entities)
        {
            if (Exists(spawned))
                QueueDel(spawned);
        }

        ent.Comp.Entities.Clear();
        ent.Comp.MusicStream = _audio.Stop(ent.Comp.MusicStream);
        var wasRunning = ent.Comp.Stage != LavalandFaunaRaidStage.Idle;
        ent.Comp.Stage = LavalandFaunaRaidStage.Idle;
        if (wasRunning && mapId != MapId.Nullspace)
            RaiseNetworkEvent(new LavalandFaunaRaidTimerHideEvent(), RaidFilter(mapId));
    }

    private static readonly TimeSpan RaiderThinkInterval = TimeSpan.FromSeconds(0.3);

    /// <summary>A player this close and in plain sight is left to the fauna's own AI.</summary>
    private const float RaiderSightRange = 9f;

    /// <summary>A player this close (and nothing between) gets the blow instead of the wall.</summary>
    private const float RaiderBiteRange = 1.6f;

    /// <summary>Shooting and lashing reach.</summary>
    private const float RaiderShootRange = 9f;

    /// <summary>The tentacle goliath stops coming closer at this distance.</summary>
    private const float RaiderTentacleHold = 6f;

    /// <summary>Shooters stop coming closer at this distance.</summary>
    private const float RaiderShooterHold = 5f;

    private readonly HashSet<MapId> _tentaclerMaps = new();

    private readonly HashSet<EntityUid> _obstacles = new();

    /// <summary>
    /// Raid fauna that see nobody walk straight at the nearest player and beat down the walls, doors, windows and
    /// machines in the way (their blows carry structural damage). As soon as a player is in sight, their AI takes over.
    /// </summary>
    private void UpdateRaiders()
    {
        var now = _timing.CurTime;

        // One tentacle goliath per raid: whoever has the job keeps it while alive.
        _tentaclerMaps.Clear();
        var tentaclers = EntityQueryEnumerator<LavalandRaiderComponent, TransformComponent>();
        while (tentaclers.MoveNext(out var uid, out var raider, out var xform))
        {
            if (raider.Tentacler && !_mobState.IsDead(uid))
                _tentaclerMaps.Add(xform.MapID);
        }

        var query = EntityQueryEnumerator<LavalandRaiderComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var raider, out var mover, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (!raider.Tentacler && HasComp<NPCUseActionOnTargetComponent>(uid) && _tentaclerMaps.Add(xform.MapID))
                raider.Tentacler = true;

            if (now >= raider.NextThink)
            {
                raider.NextThink = now + RaiderThinkInterval;
                ThinkRaider((uid, raider), xform);
            }

            if (!raider.Assaulting || raider.Target is not { } target || TerminatingOrDeleted(target))
                continue;

            var offset = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
            if (offset.LengthSquared() < 0.01f)
                continue;

            // What their AI did and does not now: watchers shoot, legions spit their skulls, the tentacle goliath
            // lashes out. In sight and in range: they do it, and keep their distance instead of coming close.
            var distance = offset.Length();
            if (distance <= RaiderShootRange && UseRangedAttack((uid, raider), xform, target, distance))
                continue;

            // Close enough and nothing between: the blow goes to him.
            if (offset.Length() <= RaiderBiteRange && _interaction.InRangeUnobstructed((uid, xform), target, RaiderBiteRange))
            {
                if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) && weapon.NextAttack <= now)
                {
                    // A blow needs combat mode on; with its AI off nothing else turns it on.
                    _combatMode.SetInCombatMode(uid, true);
                    _melee.AttemptLightAttack(uid, weaponUid, weapon, target);
                }
                continue;
            }

            // Straight at him, the way NPC steering moves its mobs; whatever stands in the way is smashed.
            mover.CurTickSprintMovement = Vector2.Normalize(offset);
            mover.LastInputTick = _timing.CurTick;
            mover.LastInputSubTick = ushort.MaxValue;
            SmashAhead(uid, xform, offset);
        }
    }

    private void ThinkRaider(Entity<LavalandRaiderComponent> ent, TransformComponent xform)
    {
        var position = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDistance = RaidVision * RaidVision;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player || HasComp<GhostComponent>(player) ||
                HasComp<AshWalkerComponent>(player) || !HasComp<MobStateComponent>(player) || !_mobState.IsAlive(player))
            {
                continue;
            }

            var playerXform = Transform(player);
            if (playerXform.MapID != xform.MapID)
                continue;

            var distance = Vector2.DistanceSquared(position, _transform.GetWorldPosition(playerXform));
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = player;
        }

        // Nobody about: the fauna's own AI wanders. Somebody there - seen through a window or not - it goes straight
        // for him by itself: the AI's pathfinding does not find a way from the land onto the outpost grid, and the
        // mob would only stand and stare at him through the wall.
        if (best is not { } target)
        {
            SetAssault(ent, false, null);
            return;
        }

        SetAssault(ent, true, target);
    }

    private void SetAssault(Entity<LavalandRaiderComponent> ent, bool assault, EntityUid? target)
    {
        ent.Comp.Target = target;
        if (ent.Comp.Assaulting == assault)
            return;

        ent.Comp.Assaulting = assault;
        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent.Owner, htn), !assault, assault ? 0f : 0.3f);

        // It moves by itself now: it must not be asleep (asleep its movement is not processed at all).
        if (assault && !HasComp<ActiveNPCComponent>(ent))
            _npc.WakeNPC(ent);
    }

    /// <summary>Beats down whatever solid and breakable stands right ahead: walls, doors, windows, machines.</summary>
    private void SmashAhead(EntityUid uid, TransformComponent xform, Vector2 towards)
    {
        if (towards.LengthSquared() < 0.01f || !_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) ||
            weapon.NextAttack > _timing.CurTime)
        {
            return;
        }

        // A little ahead and a little farther: big mobs like the goliath stop with the wall more than a tile away.
        var position = _transform.GetWorldPosition(xform);
        var direction = Vector2.Normalize(towards);
        foreach (var reach in SmashReaches)
        {
            _obstacles.Clear();
            _lookup.GetEntitiesInRange(xform.MapID, position + direction * reach, 0.6f, _obstacles,
                LookupFlags.Static | LookupFlags.Dynamic);
            foreach (var obstacle in _obstacles)
            {
                // Walls, basalt and rock, doors, windows, machines: anything solid, anchored and breakable.
                if (obstacle == uid || HasComp<MobStateComponent>(obstacle) || !HasComp<DamageableComponent>(obstacle) ||
                    !TryComp<PhysicsComponent>(obstacle, out var body) || !body.Hard || !body.CanCollide ||
                    !Transform(obstacle).Anchored)
                {
                    continue;
                }

                _combatMode.SetInCombatMode(uid, true);
                _melee.AttemptLightAttack(uid, weaponUid, weapon, obstacle);
                return;
            }
        }
    }

    private static readonly float[] SmashReaches = { 0.9f, 1.4f };

    /// <summary>
    /// The ranged attacks of the raid fauna, done here while their AI is off. True if it holds its ground this tick
    /// (it attacked from where it stands); false - it keeps running at him.
    /// </summary>
    private bool UseRangedAttack(Entity<LavalandRaiderComponent> ent, TransformComponent xform, EntityUid target, float distance)
    {
        var uid = ent.Owner;
        var tentacles = ent.Comp.Tentacler && TryComp<NPCUseActionOnTargetComponent>(uid, out _);
        var gun = TryComp<GunComponent>(uid, out var gunComp);
        if (!tentacles && !gun)
            return false;

        if (!_interaction.InRangeUnobstructed((uid, xform), target, RaiderShootRange))
            return false;

        _combatMode.SetInCombatMode(uid, true);
        if (tentacles)
        {
            _useAction.TryUseTentacleAttack(uid, target);
            // Lashing from afar: it does not walk into his reach.
            return distance <= RaiderTentacleHold;
        }

        _gun.AttemptShoot(uid, (uid, gunComp!), Transform(target).Coordinates, target);
        // A watcher keeps away; a legion still comes on to bite, its skulls flying ahead of it.
        return distance <= RaiderShooterHold && !HasComp<LavalandLegionComponent>(uid);
    }

    /// <summary>Everyone on the map but the ash walkers: the raid is the outpost's trouble.</summary>
    private Filter RaidFilter(MapId mapId)
    {
        return Filter.BroadcastMap(mapId).RemoveWhere(session =>
            session.AttachedEntity is { } mob && HasComp<AshWalkerComponent>(mob));
    }

    /// <summary>A living player standing on the outpost grid.</summary>
    private bool AnyDefender(EntityUid outpost)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob || HasComp<GhostComponent>(mob) ||
                !HasComp<MobStateComponent>(mob) || !_mobState.IsAlive(mob))
            {
                continue;
            }

            if (Transform(mob).GridUid == outpost)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Nobody came: the fauna wreck the outpost. A part of its walls, doors, windows and machines is torn down,
    /// and blasts go off over it.
    /// </summary>
    private void RuinOutpost(EntityUid outpost, LavalandFaunaRaidConfig config, MapId mapId)
    {
        if (!TryComp<MapGridComponent>(outpost, out var grid))
            return;

        var structures = new List<EntityUid>();
        var tiles = new List<Vector2i>();
        var enumerator = _map.GetAllTilesEnumerator(outpost, grid);
        while (enumerator.MoveNext(out var tile))
        {
            tiles.Add(tile.Value.GridIndices);
            var anchored = _map.GetAnchoredEntitiesEnumerator(outpost, grid, tile.Value.GridIndices);
            while (anchored.MoveNext(out var uid))
            {
                if (uid is { } structure && HasComp<DamageableComponent>(structure) && !HasComp<MobStateComponent>(structure))
                    structures.Add(structure);
            }
        }

        _random.Shuffle(structures);
        var count = (int) MathF.Round(structures.Count * Math.Clamp(config.RuinFraction, 0f, 1f));
        for (var i = 0; i < count; i++)
        {
            QueueDel(structures[i]);
        }

        for (var i = 0; i < config.RuinBlasts && tiles.Count > 0; i++)
        {
            var tile = _random.Pick(tiles);
            _explosion.QueueExplosion(_transform.ToMapCoordinates(_map.GridTileToLocal(outpost, grid, tile)),
                "Default", 20f, 4f, 6f, null, tileBreakScale: 0f, maxTileBreak: 0, canCreateVacuum: false);
        }

        _chat.DispatchFilteredAnnouncement(RaidFilter(mapId), Loc.GetString("lavaland-raid-ruined"),
            playSound: false,
            colorOverride: Color.Red);
    }

    private void SpawnTendrils(Entity<LavalandFaunaRaidComponent> ent, LavalandFaunaRaidConfig config)
    {
        if (!TryFindOutpost(ent, out var outpost) || !TryComp<MapGridComponent>(outpost, out var outpostGrid) ||
            !TryComp<MapGridComponent>(ent, out var terrain))
        {
            return;
        }

        // Around the outpost's edges, not in its walls: the box of the whole outpost grid.
        var bounds = _transform.GetWorldMatrix(outpost).TransformBox(outpostGrid.LocalAABB);
        var docks = new List<Vector2>();
        var dockQuery = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (dockQuery.MoveNext(out _, out _, out var dockXform))
        {
            if (dockXform.GridUid == outpost)
                docks.Add(_transform.GetWorldPosition(dockXform));
        }

        var min = Math.Max(1f, config.MinDistance);
        var max = Math.Max(min, config.MaxDistance);
        var placed = new List<Vector2>();
        var now = _timing.CurTime;
        foreach (var prototype in config.Tendrils)
        {
            if (!TryPickTendrilSpot(ent, terrain, outpost, bounds, docks, placed, min, max, out var spot))
            {
                Log.Warning($"Lavaland raid: no free spot for the tendril {prototype} around the outpost.");
                continue;
            }

            placed.Add(spot);
            var tendril = Spawn(prototype, new EntityCoordinates(ent, spot));
            EnsureComp<TimedDespawnComponent>(tendril).Lifetime = (float) config.Duration.TotalSeconds;
            EnsureComp<LavalandRaidTendrilComponent>(tendril).Map = ent.Owner;
            // The raid does not wait: the first fauna come out right away.
            if (TryComp<LavalandNecropolisTendrilComponent>(tendril, out var tendrilComp))
                tendrilComp.NextSpawnTime = now + TimeSpan.FromSeconds(1);
            ent.Comp.Entities.Add(tendril);
        }
    }

    /// <summary>
    /// A spot on the open land around the outpost, as close to its walls as it can be: rings around the outpost are
    /// tried from the innermost out, the first free spot wins. Off every grid (the outpost, docked shuttles), away from
    /// its docking ports where shuttles land and from the landing beacon, not on lava or a chasm, not on another tendril.
    /// </summary>
    private bool TryPickTendrilSpot(
        Entity<LavalandFaunaRaidComponent> map,
        MapGridComponent terrain,
        EntityUid outpost,
        Box2 bounds,
        List<Vector2> docks,
        List<Vector2> placed,
        float min,
        float max,
        out Vector2 spot)
    {
        spot = default;
        var mapId = Transform(map).MapID;
        var beacon = TryComp<LavalandMapComponent>(map, out var lavaland) && lavaland.Planet.Id != null &&
                     _prototype.TryIndex(lavaland.Planet, out var planet) && planet.FtlEnabled
            ? planet.FtlBeaconOffset
            : (Vector2?) null;

        for (var ring = min; ring <= max; ring += 1f)
        {
            if (TryPickOnRing(map, terrain, bounds.Enlarged(ring), mapId, beacon, docks, placed, out spot))
                return true;
        }

        return false;
    }

    /// <summary>A free spot on the edge of this box around the outpost, a random one of them.</summary>
    private bool TryPickOnRing(
        Entity<LavalandFaunaRaidComponent> map,
        MapGridComponent terrain,
        Box2 ring,
        MapId mapId,
        Vector2? beacon,
        List<Vector2> docks,
        List<Vector2> placed,
        out Vector2 spot)
    {
        spot = default;
        var width = ring.Width;
        var height = ring.Height;
        var perimeter = 2f * (width + height);
        for (var attempt = 0; attempt < 48; attempt++)
        {
            // A random point on the box's edge.
            var along = _random.NextFloat(0f, perimeter);
            Vector2 candidate;
            if (along < width)
                candidate = new Vector2(ring.Left + along, ring.Bottom);
            else if ((along -= width) < height)
                candidate = new Vector2(ring.Right, ring.Bottom + along);
            else if ((along -= height) < width)
                candidate = new Vector2(ring.Right - along, ring.Top);
            else
                candidate = new Vector2(ring.Left, ring.Top - (along - width));

            if (docks.Any(dock => Vector2.DistanceSquared(dock, candidate) < 9f * 9f) ||
                beacon is { } beaconSpot && Vector2.DistanceSquared(beaconSpot, candidate) < 14f * 14f ||
                placed.Any(other => Vector2.DistanceSquared(other, candidate) < 5f * 5f))
            {
                continue;
            }

            // No grid right around it but the land itself: not in the outpost's walls, not on a shuttle.
            var clear = true;
            foreach (var grid in _mapManager.FindGridsIntersecting(mapId, Box2.CenteredAround(candidate, new Vector2(2.5f))))
            {
                if (grid.Owner != map.Owner)
                {
                    clear = false;
                    break;
                }
            }

            if (!clear)
                continue;

            var tile = _map.TileIndicesFor(map.Owner, terrain, new EntityCoordinates(map.Owner, candidate));
            if (!_map.TryGetTileRef(map.Owner, terrain, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var anchored = _map.GetAnchoredEntitiesEnumerator(map.Owner, terrain, tile);
            var hazard = false;
            while (anchored.MoveNext(out var uid))
            {
                if (HasComp<ChasmComponent>(uid) || HasComp<LavaComponent>(uid))
                {
                    hazard = true;
                    break;
                }
            }

            if (hazard)
                continue;

            spot = _map.GridTileToLocal(map.Owner, terrain, tile).Position;
            return true;
        }

        return false;
    }

    /// <summary>
    /// A raid tendril's fauna: they see the players of the outpost from afar, go for them even behind walls and
    /// break through doors, windows and machines on the way.
    /// </summary>
    public void MakeRaider(EntityUid mob)
    {
        if (!TryComp<HTNComponent>(mob, out var htn))
            return;

        EnsureComp<LavalandRaiderComponent>(mob);
        htn.RootTask = new HTNCompoundTask { Task = RaidCompound };
        // Look for a new target all the time, not only once the current wander is done.
        htn.ConstantlyReplan = true;
        htn.Blackboard.SetValue(NPCBlackboard.NavSmash, true);
        htn.Blackboard.SetValue(NPCBlackboard.NavInteract, true);
        htn.Blackboard.SetValue("VisionRadius", RaidVision);
        htn.Blackboard.SetValue("AggroVisionRadius", RaidVision);
        _htn.Replan(htn);
    }

    private bool TryFindOutpost(EntityUid mapUid, out EntityUid outpost)
    {
        var query = EntityQueryEnumerator<LavalandOutpostComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (marker.Map != mapUid)
                continue;
            outpost = uid;
            return true;
        }

        outpost = default;
        return false;
    }
}
