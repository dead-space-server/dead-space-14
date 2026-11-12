using Content.Shared.Atmos.Components;
using Content.Shared.Audio;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Numerics;
using Content.Shared.Damage.Components;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

// shuttle impact damage ported from Goobstation (AGPLv3) with agreement of all coders involved
public sealed partial class ShuttleSystem
{
    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery = default!;
    [Dependency] private EntityQuery<MovedByPressureComponent> _movedByPressureQuery = default!;

    private bool _enabled;
    private float _minimumImpactInertia;
    private float _minimumImpactVelocity;
    private float _tileBreakEnergyMultiplier;
    private float _damageMultiplier;
    private float _structuralDamage;
    private float _sparkEnergy;
    private float _impactRadius;
    private float _impactSlowdown;
    private float _minThrowVelocity;
    private float _massBias;
    private float _inertiaScaling;
    // this doesn't update if plating mass is changed but edgecase
    private float _platingMass;

    private const float _sparkChance = 0.2f;
    // shuttle mass to consider the neutral point for inertia scaling: 100 tiles at standard weight
    private const float _baseShuttleMass = 100f * TileDensityMultiplier;
    // exists primarily for optimisation so not a cvar
    private const float _minImpulseVelocity = 0.07f;
    // high-speed collisions tend to be a series of increasingly smaller collisions so don't spam admin logs
    private readonly TimeSpan _adminLogSpacing = TimeSpan.FromSeconds(3);

    private readonly SoundCollectionSpecifier _shuttleImpactSound = new("ShuttleImpactSound");
    private readonly ProtoId<ContentTileDefinition> _platingId = "Plating";
    private readonly EntProtoId _sparkEffect = "EffectSparks";

<<<<<<< ours
    private EntityQuery<DamageableComponent> _dmgQuery;
    private EntityQuery<ProjectileComponent> _projQuery;
    private EntityQuery<MovedByPressureComponent> _movedByPressureQuery;

    private readonly HashSet<EntityUid> _countedEnts = new();
    private readonly HashSet<EntityUid> _intersecting = new();
    private readonly List<Entity<PhysicsComponent>> _gridThrowEntities = new();
    private readonly HashSet<EntityUid> _gridThrowProcessed = new();
    private readonly List<ImpactTileData> _impactTiles = new();
    private readonly List<(Vector2i, Tile)> _brokenImpactTiles = new();
    private readonly List<Vector2i> _sparkImpactTiles = new();
    private readonly HashSet<Entity<TransformComponent>> _entitiesOnImpactTile = new();
||||||| base
    private EntityQuery<DamageableComponent> _dmgQuery;
    private EntityQuery<ProjectileComponent> _projQuery;

    private HashSet<EntityUid> _countedEnts = new();
    private HashSet<EntityUid> _intersecting = new();
=======
    private HashSet<EntityUid> _countedEnts = new();
    private HashSet<EntityUid> _intersecting = new();
>>>>>>> theirs
    // for _adminLogSpacing
    private Dictionary<EntityUid, TimeSpan> _impactedAt = new();

    private void InitializeImpact()
    {
        SubscribeLocalEvent<ShuttleComponent, StartCollideEvent>(OnShuttleCollide);

<<<<<<< ours
        _dmgQuery = GetEntityQuery<DamageableComponent>();
        _projQuery = GetEntityQuery<ProjectileComponent>();
        _movedByPressureQuery = GetEntityQuery<MovedByPressureComponent>();

||||||| base
        _dmgQuery = GetEntityQuery<DamageableComponent>();
        _projQuery = GetEntityQuery<ProjectileComponent>();

=======
>>>>>>> theirs
        Subs.CVar(_cfg, CCVars.ImpactEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CCVars.MinimumImpactInertia, value => _minimumImpactInertia = value, true);
        Subs.CVar(_cfg, CCVars.MinimumImpactVelocity, value => _minimumImpactVelocity = value, true);
        Subs.CVar(_cfg, CCVars.TileBreakEnergyMultiplier, value => _tileBreakEnergyMultiplier = value, true);
        Subs.CVar(_cfg, CCVars.ImpactDamageMultiplier, value => _damageMultiplier = value, true);
        Subs.CVar(_cfg, CCVars.ImpactStructuralDamage, value => _structuralDamage = value, true);
        Subs.CVar(_cfg, CCVars.SparkEnergy, value => _sparkEnergy = value, true);
        Subs.CVar(_cfg, CCVars.ImpactRadius, value => _impactRadius = value, true);
        Subs.CVar(_cfg, CCVars.ImpactSlowdown, value => _impactSlowdown = value, true);
        Subs.CVar(_cfg, CCVars.ImpactMinThrowVelocity, value => _minThrowVelocity = value, true);
        Subs.CVar(_cfg, CCVars.ImpactMassBias, value => _massBias = value, true);
        Subs.CVar(_cfg, CCVars.ImpactInertiaScaling, value => _inertiaScaling = value, true);

        _platingMass = ProtoMan.Index(_platingId).Mass;
    }

    /// <summary>
    /// Handles collision between two shuttles, applying impact damage and effects.
    /// </summary>
    private void OnShuttleCollide(EntityUid uid, ShuttleComponent component, ref StartCollideEvent args)
    {
        // DS14-Start: prevent station anchors and active shuttle pairs from processing the same impact twice.
        if (!component.Enabled || args.OurEntity != uid)
            return;

        if (TryComp<ShuttleComponent>(args.OtherEntity, out var otherShuttle) &&
            otherShuttle.Enabled &&
            uid.Id > args.OtherEntity.Id)
        {
            return;
        }
        // DS14-End

        if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid)
            || TerminatingOrDeleted(args.OtherEntity) || EntityManager.IsQueuedForDeletion(args.OtherEntity)
        )
            return;

        if (!_gridQuery.TryComp(args.OurEntity, out var ourGrid) ||
            !_gridQuery.TryComp(args.OtherEntity, out var otherGrid)
        )
            return;

        if (IsDockImpactSuppressed(args.OurEntity, args.OtherEntity))
            return;

        var ourBody = args.OurBody;
        var otherBody = args.OtherBody;

        // TODO: Would also be nice to have a continuous sound for scraping.
        var ourXform = Transform(args.OurEntity);
        var otherXform = Transform(args.OtherEntity);
        var worldPoints = args.WorldPoints;
        var worldNormal = args.WorldNormal;

        for (var i = 0; i < worldPoints.Length; i++)
        {
            var worldPoint = worldPoints[i];

            var ourPoint = _transform.ToCoordinates((args.OurEntity, ourXform), new MapCoordinates(worldPoint, ourXform.MapID));
            var otherPoint = _transform.ToCoordinates((args.OtherEntity, otherXform), new MapCoordinates(worldPoint, otherXform.MapID));

            var ourVelocity = _physics.GetLinearVelocity(args.OurEntity, ourPoint.Position, ourBody, ourXform);
            var otherVelocity = _physics.GetLinearVelocity(args.OtherEntity, otherPoint.Position, otherBody, otherXform);
            var topDiff = (ourVelocity - otherVelocity);
            var jungleDiff = topDiff.Length();
            if (jungleDiff <= 0f)
                continue;

            // Get the velocity in relation to the contact normal
            // If this still causes issues see https://box2d.org/posts/2020/06/ghost-collisions/
            // This should only be a potential problem on chunk seams.
            var dotProduct = MathF.Abs(Vector2.Dot(topDiff.Normalized(), worldNormal.Normalized()));
            jungleDiff *= dotProduct;

            // this is cursed but makes it so that collisions of small grid with large grid count the inertia as being approximately the small grid's
            var effectiveInertiaMult = (ourBody.FixturesMass * otherBody.FixturesMass) / (ourBody.FixturesMass + otherBody.FixturesMass);
            var effectiveInertia = jungleDiff * effectiveInertiaMult;

            // TODO: squish damage so that a tiny splinter grid can't stop 2 big grids by being in the way
            if (jungleDiff < _minimumImpactVelocity && effectiveInertia < _minimumImpactInertia
                || ourXform.MapUid == null
                || float.IsNaN(jungleDiff))
            {
                continue;
            }

            // Play impact sound
            var coordinates = new EntityCoordinates(ourXform.MapUid.Value, worldPoint);

            var volume = MathF.Min(10f, MathF.Pow(jungleDiff, 0.5f) - 5f);
            var audioParams = AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).AddVolume(volume);
            _audio.PlayPvs(_shuttleImpactSound, coordinates, audioParams);

            // if we're not enabled, stop after playing sound
            if (!_enabled)
                continue;

            // Convert the collision point directly to tile indices
            var ourTile = new Vector2i((int)Math.Floor(ourPoint.X / ourGrid.TileSize), (int)Math.Floor(ourPoint.Y / ourGrid.TileSize));
            var otherTile = new Vector2i((int)Math.Floor(otherPoint.X / otherGrid.TileSize), (int)Math.Floor(otherPoint.Y / otherGrid.TileSize));

            var ourMass = GetRegionMass(args.OurEntity, ourGrid, ourTile, _impactRadius, out var ourTiles);
            var otherMass = GetRegionMass(args.OtherEntity, otherGrid, otherTile, _impactRadius, out var otherTiles);

            // just in case
            if (ourTiles == 0 || otherTiles == 0)
                continue;

            var shouldLog = CheckShouldLog(args.OurEntity) && CheckShouldLog(args.OtherEntity);
            if (shouldLog)
                Log.Info($"Shuttle impact of {ToPrettyString(args.OurEntity)} with {ToPrettyString(args.OtherEntity)}; our mass: {ourMass}, other: {otherMass}, velocity {jungleDiff}, impact point {worldPoint}");

            // E = MV^2/2
            var energyMult = MathF.Pow(jungleDiff, 2) / 2;
            // mass-based damage reduction to grid with more mass so that plastitanium block rammer doesn't die to lattice
            var ourMassDR = MathF.Max(otherMass / ourMass, 1f);
            var otherMassDR = MathF.Max(ourMass / otherMass, 1f);
            // multiplier to make large grids not just bonk against each other
            var inertiaMult = MathF.Pow(effectiveInertiaMult / _baseShuttleMass, _inertiaScaling);
            var toUsEnergy = otherMass * energyMult * inertiaMult * ourMassDR;
            var toOtherEnergy = ourMass * energyMult * inertiaMult * otherMassDR;

            var impact = LogImpact.High;
            // if impact isn't tiny, log it as extreme
            if (toUsEnergy + toOtherEnergy > 2f * _tileBreakEnergyMultiplier * _platingMass)
                impact = LogImpact.Extreme;
            // TODO: would be nice for it to also log who is piloting the grid(s)
            if (shouldLog)
                _logger.Add(LogType.ShuttleImpact, impact, $"Shuttle impact of {ToPrettyString(args.OurEntity)} with {ToPrettyString(args.OtherEntity)} at {worldPoint}");

            _impactedAt[args.OurEntity] = _gameTiming.CurTime;
            _impactedAt[args.OtherEntity] = _gameTiming.CurTime;

            // uses local region mass for slowdown calculation so lattice doesn't have same slowdown as wall block
            var totalInertia = ourVelocity * ourMass + otherVelocity * otherMass;
            var inelasticVel = totalInertia / (ourMass + otherMass);

            DoGridImpact((args.OurEntity, ourGrid, ourXform, ourBody), args.OurFixture, inelasticVel, ourVelocity, ourTile, ourTiles, toUsEnergy);
            DoGridImpact((args.OtherEntity, otherGrid, otherXform, otherBody), args.OtherFixture, inelasticVel, otherVelocity, otherTile, otherTiles, toOtherEnergy);
        }
    }

    private void DoGridImpact(Entity<MapGridComponent, TransformComponent, PhysicsComponent> ent,
                              Fixture fix,
                              Vector2 inelasticVelocity,
                              Vector2 velocity,
                              Vector2i tile,
                              int tiles,
                              float energy)
    {
        // for readability to not have .Comp1 .Comp2 for everything
        var (_, grid, _, body) = ent;

        // radius in which to actually do things so we don't hurt person 4 tiles away on slow bump
        var radius = Math.Min(_impactRadius, MathF.Sqrt(energy / _tileBreakEnergyMultiplier / _platingMass));

        // slow us down since destroying impacting grid tiles prevents the collision
        // without this impacts which destroy tiles just make grids slice straight through each other
        var postImpactVelocity = Vector2.Lerp(velocity, inelasticVelocity, MathF.Min(1f, _impactSlowdown * tiles * fix.Density / body.FixturesMass));
        var deltaV = -velocity + postImpactVelocity;
        var deltaVLength = deltaV.Length();

        if (body.BodyType != BodyType.Static)
            _physics.ApplyLinearImpulse(ent, deltaV * body.FixturesMass, body: body);

        // process tile and entity damage
        // DS14-Start: static station grids should take local impact damage without throwing their whole contents.
        var canThrowEntities = body.BodyType != BodyType.Static;
        var impactDirection = deltaVLength > 0f ? deltaV / deltaVLength : Vector2.Zero;
        ProcessImpactZone(ent, grid, tile, energy, impactDirection, radius, canThrowEntities);

        // throw every entity on grid if the impulse is not negligible
        if (canThrowEntities && deltaVLength > _minImpulseVelocity)
            ThrowEntitiesOnGrid(ent, -deltaV);
        // DS14-End
    }

    /// <summary>
    /// Knocks and throws all unbuckled entities on the specified grid.
    /// </summary>
    private void ThrowEntitiesOnGrid(EntityUid gridUid, Vector2 direction)
    {
        var knockdownTime = TimeSpan.FromSeconds(5);

        var minsq = _minThrowVelocity * _minThrowVelocity;

        // iterate all dynamic entities on the grid
        if (!TryComp<BroadphaseComponent>(gridUid, out var lookup) || !_gridQuery.TryComp(gridUid, out var gridComp))
            return;

        var gridBox = gridComp.LocalAABB;
        _gridThrowEntities.Clear();
        _gridThrowProcessed.Clear();
        var state = (_gridThrowEntities, _gridThrowProcessed, _physicsQuery);
        lookup.DynamicTree.QueryAabb(ref state, GridQueryCallback, gridBox, true);
        lookup.SundriesTree.QueryAabb(ref state, GridQueryCallback, gridBox, true);

        foreach (var ent in _gridThrowEntities)
        {
            // don't throw if buckled
            if (_buckle.IsBuckled(ent, _buckleQuery.CompOrNull(ent)))
                continue;

            // don't throw them if they have magboots
            if (_movedByPressureQuery.TryComp(ent, out var moved) && !moved.Enabled)
                continue;

            if (direction.LengthSquared() > minsq)
            {
                _stuns.TryCrawling(ent.Owner, knockdownTime);
                _throwing.TryThrow(ent, direction, ent.Comp, Transform(ent), direction.Length(), playSound: false);
            }
            else
            {
                _physics.ApplyLinearImpulse(ent, direction * ent.Comp.Mass, body: ent.Comp);
            }
        }
    }

    private static bool GridQueryCallback(
        ref (List<Entity<PhysicsComponent>> List, HashSet<EntityUid> Processed, EntityQuery<PhysicsComponent> PhysicsQuery) state,
        in EntityUid uid)
    {
        if (state.Processed.Add(uid) && state.PhysicsQuery.TryComp(uid, out var body))
            state.List.Add((uid, body));

        return true;
    }

    private static bool GridQueryCallback(
        ref (List<Entity<PhysicsComponent>> List, HashSet<EntityUid> Processed, EntityQuery<PhysicsComponent> PhysicsQuery) state,
        in FixtureProxy proxy)
    {
        var owner = proxy.Entity;
        return GridQueryCallback(ref state, in owner);
    }

    /// <summary>
    /// Structure to hold impact tile processing data for batch processing
    /// </summary>
    private record struct ImpactTileData(Vector2i Tile, float Energy, float DistanceFactor);

    /// <summary>
    /// Gets the total mass of all entities and tiles (using ContentTileDefinition.Mass) belonging to this grid in a circle
    /// </summary>
    private float GetRegionMass(EntityUid uid, MapGridComponent grid, Vector2i centerTile, float radius, out int tileCount)
    {
        tileCount = 0;
        var mass = 0f;
        _countedEnts.Clear();

        foreach (var tileRef in _mapSystem.GetLocalTilesIntersecting(uid, grid, new Circle(centerTile, radius)))
        {
            var def = _turf.GetContentTileDefinition(tileRef);
            mass += def.Mass;
            tileCount++;

            _intersecting.Clear();
            _lookup.GetLocalEntitiesIntersecting(uid, tileRef.GridIndices, _intersecting, gridComp: grid);
            foreach (var localUid in _intersecting)
            {
                if (!_countedEnts.Add(localUid))
                    continue;

                if (_physicsQuery.TryComp(localUid, out var physics))
                    mass += physics.FixturesMass;
            }
        }
        return mass;
    }

    /// <summary>
    /// Processes a zone of tiles around the impact point
    /// </summary>
    private void ProcessImpactZone(EntityUid uid, MapGridComponent grid, Vector2i centerTile, float energy, Vector2 dir, float radius, bool throwEntities)
    {
        _impactTiles.Clear();
        _brokenImpactTiles.Clear();
        _sparkImpactTiles.Clear();

        // Pre-calculate all tiles that need processing
        foreach (var tileRef in _mapSystem.GetLocalTilesIntersecting(uid, grid, new Circle(centerTile, radius)))
        {
            var distance = centerTile - tileRef.GridIndices;
            // Calculate distance-based energy falloff
            float distanceFactor = 1.0f - distance.Length / (radius + 1);
            float tileEnergy = energy * distanceFactor;

            _impactTiles.Add(new ImpactTileData(tileRef.GridIndices, tileEnergy, distanceFactor));
        }

        ProcessTileBatch(uid, grid, _impactTiles, dir, 0, _impactTiles.Count, _brokenImpactTiles, _sparkImpactTiles, throwEntities);

        // Only proceed with visual effects if the entity still exists
        if (Exists(uid))
        {
            ProcessBrokenTilesAndSparks(uid, grid, _brokenImpactTiles, _sparkImpactTiles);
        }
    }

    /// <summary>
    /// Process a batch of tiles from the impact zone
    /// </summary>
    private void ProcessTileBatch(
        EntityUid uid,
        MapGridComponent grid,
        List<ImpactTileData> tilesToProcess,
        Vector2 throwDirection,
        int startIndex,
        int endIndex,
        List<(Vector2i, Tile)> brokenTiles,
        List<Vector2i> sparkTiles,
        bool throwEntities)
    {
        // here so we don't have to `new` it every iteration
        var damageSpec = new DamageSpecifier()
        {
            DamageDict = { ["Blunt"] = 0, ["Structural"] = 0 }
        };

        var tileCenter = new Vector2(grid.TileSize / 2f, grid.TileSize / 2f);

        for (var i = startIndex; i < endIndex; i++)
        {
            var tileData = tilesToProcess[i];

            bool canBreakTile = true;

            // Process entities on this tile
            _entitiesOnImpactTile.Clear();
            _lookup.GetLocalEntitiesIntersecting(uid, tileData.Tile, _entitiesOnImpactTile, gridComp: grid);

            // this loop is a hotspot so tell if you know how to optimise it
            foreach (var localEnt in _entitiesOnImpactTile)
            {
                // the query can ocassionally return entities barely touching this tile so check for that
                var toCenter = tileData.Tile + tileCenter - localEnt.Comp.Coordinates.Position;
                if (MathF.Abs(toCenter.X) > 0.5f || MathF.Abs(toCenter.Y) > 0.5f)
                    continue;

                if (_damageableQuery.TryComp(localEnt, out var damageable))
                {
                    // Apply damage scaled by distance but capped to prevent gibbing
                    var scaledDamage = tileData.Energy * _damageMultiplier;
                    damageSpec.DamageDict["Blunt"] = scaledDamage;
                    damageSpec.DamageDict["Structural"] = scaledDamage * _structuralDamage;

                    _damageSys.ChangeDamage((localEnt, damageable), damageSpec);
                }
                // might've been destroyed
                if (TerminatingOrDeleted(localEnt) || EntityManager.IsQueuedForDeletion(localEnt))
                    continue;

                if (!_physicsQuery.TryComp(localEnt, out var physics))
                    continue;

                // no breaking tiles under walls that haven't been destroyed
                if ((physics.BodyType & BodyType.Static) != 0
                    && (physics.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
                {
                    canBreakTile = false;
                }
                else if (throwEntities)
                {
                    var direction = throwDirection * tileData.DistanceFactor;
                    _throwing.TryThrow(localEnt, direction, physics, localEnt.Comp, direction.Length(), playSound: false);
                }
            }

            // Mark tiles for spark effects
            if (tileData.Energy > _sparkEnergy && tileData.DistanceFactor > 0.7f && _random.Prob(_sparkChance))
                sparkTiles.Add(tileData.Tile);

            if (!canBreakTile)
                continue;

            // Mark tiles for breaking/effects
            var def = _turf.GetContentTileDefinition(_mapSystem.GetTileRef(uid, grid, tileData.Tile));
            if (tileData.Energy > def.Mass * _tileBreakEnergyMultiplier)
                brokenTiles.Add((tileData.Tile, Tile.Empty));

        }
    }

    /// <summary>
    /// Process visual effects and tile breaking after entity processing
    /// </summary>
    private void ProcessBrokenTilesAndSparks(
        EntityUid uid,
        MapGridComponent grid,
        List<(Vector2i, Tile)> brokenTiles,
        List<Vector2i> sparkTiles)
    {
        // Break tiles
        _mapSystem.SetTiles(uid, grid, brokenTiles);

        if (TerminatingOrDeleted(uid))
            return;

        // Spawn spark effects
        foreach (var tile in sparkTiles)
        {
            var coords = _mapSystem.GridTileToLocal(uid, grid, tile);
            Spawn(_sparkEffect, coords);
        }
    }

    /// <summary>
    /// Check whether this impact should be logged to admins.
    /// Used to prevent spamming logs.
    /// </summary>
    private bool CheckShouldLog(EntityUid uid)
    {
        return !(_impactedAt.ContainsKey(uid) && _gameTiming.CurTime < _impactedAt[uid] + _adminLogSpacing);
    }
}
