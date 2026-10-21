using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Parallax;
using Content.Server.Tiles;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Destructible;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandNecropolisTendrilSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly LavalandFaunaRaidSystem _raid = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly List<(Vector2i Index, Tile Tile)> _reservedTiles = new();
    private readonly List<Vector2i> _spawnTiles = new();
    private readonly HashSet<Vector2i> _collapseTiles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandNecropolisTendrilComponent, MapInitEvent>(OnTendrilMapInit);
        SubscribeLocalEvent<LavalandNecropolisTendrilComponent, BeforeDamageChangedEvent>(OnTendrilBeforeDamage);
        SubscribeLocalEvent<LavalandNecropolisTendrilComponent, DestructionEventArgs>(OnTendrilDestroyed);
        SubscribeLocalEvent<LavalandTendrilCollapseComponent, MapInitEvent>(OnCollapseMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var tendrilQuery = EntityQueryEnumerator<LavalandNecropolisTendrilComponent, TransformComponent>();
        while (tendrilQuery.MoveNext(out var uid, out var tendril, out var xform))
        {
            if (tendril.Destroyed ||
                tendril.NextSpawnTime > now)
            {
                continue;
            }

            tendril.NextSpawnTime = now + ClampInterval(tendril.SpawnInterval);
            SpawnMob((uid, tendril, xform));
        }

        var collapseQuery = EntityQueryEnumerator<LavalandTendrilCollapseComponent, TransformComponent>();
        while (collapseQuery.MoveNext(out var uid, out var collapse, out var xform))
        {
            if (!collapse.TendrilCollapseInitialized ||
                collapse.CollapseTime > now)
            {
                continue;
            }

            Collapse((uid, collapse, xform));
        }
    }

    /// <summary>
    /// Its own fauna (and any other) don't break tendrils, smashing their way to the outpost - with their claws,
    /// their shots or what they throw. A raid's tendril takes no damage from nobody in particular either.
    /// </summary>
    private void OnTendrilBeforeDamage(Entity<LavalandNecropolisTendrilComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin is not { } origin)
        {
            if (HasComp<LavalandRaidTendrilComponent>(ent))
                args.Cancelled = true;
            return;
        }

        // A shot or a thrown thing counts as whoever sent it.
        if (TryComp<Content.Shared.Projectiles.ProjectileComponent>(origin, out var projectile) && projectile.Shooter is { } shooter)
            origin = shooter;
        else if (TryComp<Content.Shared.Throwing.ThrownItemComponent>(origin, out var thrown) && thrown.Thrower is { } thrower)
            origin = thrower;

        if (HasComp<LavalandFaunaComponent>(origin))
            args.Cancelled = true;
    }

    private void OnTendrilMapInit(Entity<LavalandNecropolisTendrilComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpawnTime = _timing.CurTime + ClampInterval(ent.Comp.SpawnInterval);
        ClearTerrainAround(ent.Owner, ent.Comp.ClearRadius);
    }

    private void OnTendrilDestroyed(Entity<LavalandNecropolisTendrilComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.Destroyed)
            return;

        ent.Comp.Destroyed = true;
        var coords = Transform(ent.Owner).Coordinates;

        Spawn(ent.Comp.ChestPrototype.Id, coords);
        Spawn(ent.Comp.CollapsePrototype.Id, coords);
    }

    private void OnCollapseMapInit(Entity<LavalandTendrilCollapseComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.TendrilCollapseInitialized = true;
        ent.Comp.CollapseTime = _timing.CurTime + ent.Comp.Delay;

        _popup.PopupEntity("The ground begins to collapse around the tendril!", ent.Owner, PopupType.LargeCaution);
    }

    private void SpawnMob(Entity<LavalandNecropolisTendrilComponent, TransformComponent> ent)
    {
        PruneMobs(ent.Comp1);

        if (ent.Comp1.SpawnPrototypes.Count == 0 ||
            ent.Comp1.ActiveMobs.Count >= ent.Comp1.MaxActiveMobs)
        {
            return;
        }

        if (!TryGetMobSpawnCoordinates(ent.Comp2, ent.Comp1.SpawnRadius, out var coordinates))
            return;

        var prototype = _random.Pick(ent.Comp1.SpawnPrototypes);
        var spawned = Spawn(prototype.Id, coordinates);
        ent.Comp1.ActiveMobs.Add(spawned);

        // A raid tendril sends its fauna at the outpost.
        if (HasComp<LavalandRaidTendrilComponent>(ent))
            _raid.MakeRaider(spawned);
    }

    private bool TryGetMobSpawnCoordinates(
        TransformComponent xform,
        float spawnRadius,
        out EntityCoordinates coordinates)
    {
        coordinates = default;

        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var centerTile))
        {
            return false;
        }

        var center = centerTile.GridIndices;
        var radius = Math.Max(1, (int) MathF.Ceiling(spawnRadius));
        var radiusSquared = radius * radius;

        _spawnTiles.Clear();
        for (var x = center.X - radius; x <= center.X + radius; x++)
        {
            for (var y = center.Y - radius; y <= center.Y + radius; y++)
            {
                var indices = new Vector2i(x, y);
                if (indices == center ||
                    (indices - center).LengthSquared > radiusSquared ||
                    !CanSpawnMobAt(gridUid, grid, indices))
                {
                    continue;
                }

                _spawnTiles.Add(indices);
            }
        }

        if (_spawnTiles.Count == 0)
            return false;

        var picked = _random.Pick(_spawnTiles);
        if (!_map.TryGetTileRef(gridUid, grid, picked, out var tile))
            return false;

        coordinates = _map.ToCenterCoordinates(tile, grid);
        return true;
    }

    private void PruneMobs(LavalandNecropolisTendrilComponent tendril)
    {
        tendril.ActiveMobs.RemoveWhere(uid =>
            !Exists(uid) ||
            TerminatingOrDeleted(uid) ||
            TryComp<MobStateComponent>(uid, out var mobState) && _mobState.IsDead(uid, mobState));
    }

    private void Collapse(Entity<LavalandTendrilCollapseComponent, TransformComponent> ent)
    {
        if (!_turf.TryGetTileRef(ent.Comp2.Coordinates, out var centerTile) ||
            !TryComp<MapGridComponent>(centerTile.Value.GridUid, out var grid))
        {
            QueueDel(ent.Owner);
            return;
        }

        _collapseTiles.Clear();
        var radius = Math.Max(0, ent.Comp1.Radius);
        var center = centerTile.Value.GridIndices;
        var radiusSquared = radius * radius;

        for (var x = center.X - radius; x <= center.X + radius; x++)
        {
            for (var y = center.Y - radius; y <= center.Y + radius; y++)
            {
                var indices = new Vector2i(x, y);
                if (ent.Comp1.SkipCenter && indices == center ||
                    (indices - center).LengthSquared > radiusSquared ||
                    !CanCreateChasm(centerTile.Value.GridUid, grid, indices))
                {
                    continue;
                }

                _collapseTiles.Add(indices);
            }
        }

        foreach (var indices in _collapseTiles)
        {
            if (_map.TryGetTileRef(centerTile.Value.GridUid, grid, indices, out var tile))
                Spawn(ent.Comp1.ChasmPrototype.Id, _map.ToCenterCoordinates(tile, grid));
        }

        _audio.PlayPvs(ent.Comp1.CollapseSound, ent.Owner);
        QueueDel(ent.Owner);
    }

    private bool CanCreateChasm(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (!_map.TryGetTileRef(gridUid, grid, indices, out var tile) ||
            tile.Tile.IsEmpty ||
            _turf.IsSpace(tile))
        {
            return false;
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (uid == null ||
                TerminatingOrDeleted(uid.Value))
            {
                continue;
            }

            if (HasComp<ChasmComponent>(uid.Value) ||
                HasComp<LavalandOutpostComponent>(uid.Value) ||
                HasComp<LavalandShelterComponent>(uid.Value) ||
                HasComp<LavalandNecropolisTendrilComponent>(uid.Value))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanSpawnMobAt(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (!_map.TryGetTileRef(gridUid, grid, indices, out var tile) ||
            tile.Tile.IsEmpty ||
            _turf.IsSpace(tile) ||
            _turf.IsTileBlocked(tile, CollisionGroup.MobMask))
        {
            return false;
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (uid == null ||
                TerminatingOrDeleted(uid.Value))
            {
                continue;
            }

            if (HasComp<ChasmComponent>(uid.Value) ||
                HasComp<TileEntityEffectComponent>(uid.Value) ||
                HasComp<LavalandOutpostComponent>(uid.Value) ||
                HasComp<LavalandShelterComponent>(uid.Value) ||
                HasComp<LavalandNecropolisTendrilComponent>(uid.Value) ||
                HasComp<MobStateComponent>(uid.Value))
            {
                return false;
            }
        }

        return true;
    }

    private void ClearTerrainAround(EntityUid uid, int radius)
    {
        radius = Math.Max(0, radius);
        var xform = Transform(uid);

        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !TryComp<BiomeComponent>(gridUid, out var biome))
        {
            return;
        }

        var center = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        var min = new Vector2(center.X - radius, center.Y - radius);
        var max = new Vector2(center.X + radius + 1, center.Y + radius + 1);

        _reservedTiles.Clear();
        _biome.ReserveTiles(gridUid, new Box2(min, max), _reservedTiles, biome, grid);
        _reservedTiles.Clear();
    }

    private static TimeSpan ClampInterval(TimeSpan interval)
    {
        return interval <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1)
            : interval;
    }
}
