// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.RCD;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.RCD;

public sealed class RcdBuildSiteSystem : EntitySystem
{
    private static readonly EntProtoId SitePrototype = "RcdBuildSite";
    private static readonly TimeSpan IdleLifetime = TimeSpan.FromSeconds(180);
    private static readonly Box2 TileBox = new(-0.5f, -0.5f, 0.5f, 0.5f);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly HashSet<EntityUid> _intersecting = new();
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RcdBuildSiteComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<RcdBuildSiteComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<RcdBuildSiteComponent>();
        while (query.MoveNext(out var uid, out var site))
        {
            if (site.Active)
            {
                if (ProgressOf(site) >= 1f &&
                    _timing.CurTime - site.StartTime > TimeSpan.FromSeconds(site.FullDelay + 1f))
                {
                    site.Progress = 1f;
                    site.StartProgress = 1f;
                    site.Active = false;
                    site.User = NetEntity.Invalid;
                    site.Touched = _timing.CurTime;
                    Dirty(uid, site);
                }

                continue;
            }

            if (_timing.CurTime - site.Touched >= IdleLifetime || IsStale(site))
                QueueDel(uid);
        }
    }

    public float RemainingDelay(
        EntityUid grid,
        Vector2i tile,
        RCDPrototype proto,
        Direction direction,
        EntityUid? target,
        float fullDelay)
    {
        if (!_net.IsServer || fullDelay <= 0f)
            return fullDelay;

        if (!TrySite(grid, Key(proto, tile, direction, target), out var uid, out var site))
            return fullDelay;

        var progress = ProgressOf(site);
        if (progress <= 0f)
            return fullDelay;

        return Math.Max(fullDelay * (1f - progress), 0f);
    }

    public void Begin(
        EntityUid grid,
        MapGridComponent mapGrid,
        TileRef tile,
        Vector2i position,
        RCDPrototype proto,
        Direction direction,
        EntityUid? target,
        EntityUid user,
        EntityUid rcd,
        float fullDelay)
    {
        if (!_net.IsServer || fullDelay <= 0f)
            return;

        var key = Key(proto, position, direction, target);
        if (!TrySite(grid, key, out var uid, out var site))
        {
            var coords = _map.ToCenterCoordinates(tile, mapGrid);
            uid = Spawn(SitePrototype, coords);
            site = EnsureComp<RcdBuildSiteComponent>(uid);
            site.ProtoId = proto.ID;
            site.Tile = position;
            site.Direction = direction;
            site.UseDirection = proto.Mode != RcdMode.Deconstruct && proto.AllowMultiDirection;
            site.Target = target == null ? NetEntity.Invalid : GetNetEntity(target.Value);
            site.Grid = GetNetEntity(grid);

            site.Path = MakePath(proto, direction, target, rcd, grid, mapGrid, tile);

            EnsureComp<RcdBuildSiteIndexComponent>(grid).Sites[key] = uid;
        }

        if (!site.Active)
        {
            site.StartProgress = site.Progress;
            site.StartTime = _timing.CurTime;
            site.FullDelay = fullDelay;
            site.Active = true;
        }

        site.User = GetNetEntity(user);
        site.Downward = proto.Mode == RcdMode.Deconstruct;

        site.Touched = _timing.CurTime;
        Dirty(uid, site);
    }

    public void Pause(
        EntityUid grid,
        EntityCoordinates location,
        ProtoId<RCDPrototype> protoId,
        Direction direction,
        EntityUid? target)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp<MapGridComponent>(grid, out var mapGrid) || !_proto.TryIndex(protoId, out var proto))
            return;

        var position = _map.TileIndicesFor(grid, mapGrid, location);
        if (!TrySite(grid, Key(proto, position, direction, target), out var uid, out var site))
            return;

        var progress = ProgressOf(site);
        site.Progress = progress;
        site.StartProgress = progress;
        site.Active = false;
        site.User = NetEntity.Invalid;
        site.Touched = _timing.CurTime;
        Dirty(uid, site);
    }

    public void Clear(EntityUid grid, Vector2i position, ProtoId<RCDPrototype> protoId, Direction direction, EntityUid? target)
    {
        if (!_net.IsServer)
            return;

        if (!_proto.TryIndex(protoId, out var proto))
            return;

        if (!TrySite(grid, Key(proto, position, direction, target), out var uid, out _))
            return;

        QueueDel(uid);
    }

    public float ProgressOf(RcdBuildSiteComponent site)
    {
        if (!site.Active || site.FullDelay <= 0f)
            return Math.Clamp(site.Progress, 0f, 1f);

        var elapsed = (float) (_timing.CurTime - site.StartTime).TotalSeconds;
        return Math.Clamp(site.StartProgress + elapsed / site.FullDelay, 0f, 1f);
    }

    private void OnUnpaused(EntityUid uid, RcdBuildSiteComponent site, ref EntityUnpausedEvent args)
    {
        site.Touched += args.PausedTime;
        if (site.Active)
            site.StartTime += args.PausedTime;

        if (_net.IsServer)
            Dirty(uid, site);
    }

    private void OnShutdown(EntityUid uid, RcdBuildSiteComponent site, ComponentShutdown args)
    {
        if (!_net.IsServer || !site.Grid.IsValid())
            return;

        var grid = GetEntity(site.Grid);
        if (!TryComp<RcdBuildSiteIndexComponent>(grid, out var index))
            return;

        var key = Key(site);
        if (index.Sites.TryGetValue(key, out var existing) && existing == uid)
            index.Sites.Remove(key);
    }

    private bool IsStale(RcdBuildSiteComponent site)
    {
        var grid = GetEntity(site.Grid);
        if (!TryComp<MapGridComponent>(grid, out var mapGrid))
            return true;

        if (site.Target.IsValid() && !Exists(GetEntity(site.Target)))
            return true;

        if (!_proto.TryIndex(site.ProtoId, out var proto))
            return false;

        if (proto.Mode == RcdMode.ConstructObject && proto.Prototype != null)
            return HasIdentical(grid, site, proto);

        if (!_map.TryGetTileRef(grid, mapGrid, site.Tile, out var tile))
            return true;

        if (proto.Mode == RcdMode.ConstructTile && proto.Prototype != null)
            return _turf.GetContentTileDefinition(tile).ID == proto.Prototype;

        if (proto.Mode == RcdMode.Deconstruct && !site.Target.IsValid())
            return tile.Tile.IsEmpty;

        return false;
    }

    private bool HasIdentical(EntityUid grid, RcdBuildSiteComponent site, RCDPrototype proto)
    {
        _intersecting.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, site.Tile, _intersecting, -0.05f, LookupFlags.Uncontained);

        foreach (var ent in _intersecting)
        {
            if (MetaData(ent).EntityPrototype?.ID != proto.Prototype)
                continue;

            if (!site.UseDirection)
                return true;

            if (Transform(ent).LocalRotation.GetCardinalDir() == site.Direction)
                return true;
        }

        return false;
    }

    private Box2 MakePath(
        RCDPrototype proto,
        Direction direction,
        EntityUid? target,
        EntityUid rcd,
        EntityUid grid,
        MapGridComponent mapGrid,
        TileRef tile)
    {
        if (proto.Mode == RcdMode.Deconstruct && target != null)
            return EnsureSize(TargetBox(target.Value, grid, _map.ToCenterCoordinates(tile, mapGrid).Position));

        if (proto.CollisionBounds is { } bounds)
            return EnsureSize(RotateBox(bounds, PathAngle(proto, direction, rcd)));

        return TileBox;
    }

    private Box2 TargetBox(EntityUid target, EntityUid grid, Vector2 origin)
    {
        if (!TryComp<FixturesComponent>(target, out var fixtures))
            return TileBox;

        var localPos = Vector2.Transform(_transform.GetWorldPosition(target), _transform.GetInvWorldMatrix(grid));
        var localRot = _transform.GetWorldRotation(target) - _transform.GetWorldRotation(grid);
        var body = new Transform(localPos, localRot);
        Box2? union = null;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            var aabb = fixture.Shape.ComputeAABB(body, 0);
            union = union == null ? aabb : Union(union.Value, aabb);
        }

        if (union == null)
            return TileBox;

        var box = union.Value;
        return new Box2(box.Left - origin.X, box.Bottom - origin.Y, box.Right - origin.X, box.Top - origin.Y);
    }

    private Angle PathAngle(RCDPrototype proto, Direction direction, EntityUid rcd)
    {
        switch (proto.Rotation)
        {
            case RcdRotation.Fixed:
                return Angle.Zero;
            case RcdRotation.Camera:
                return Transform(rcd).LocalRotation;
            default:
                return direction.ToAngle();
        }
    }

    private bool TrySite(
        EntityUid grid,
        RcdBuildSiteKey key,
        out EntityUid uid,
        [NotNullWhen(true)] out RcdBuildSiteComponent? site)
    {
        site = null;
        uid = default;

        if (!TryComp<RcdBuildSiteIndexComponent>(grid, out var index))
            return false;

        if (!index.Sites.TryGetValue(key, out uid) || !TryComp(uid, out site))
        {
            index.Sites.Remove(key);
            return false;
        }

        return true;
    }

    private static RcdBuildSiteKey Key(RCDPrototype proto, Vector2i tile, Direction direction, EntityUid? target)
    {
        if (proto.Mode == RcdMode.Deconstruct)
            return new RcdBuildSiteKey(tile, proto.ID, null, target);

        var dir = proto.AllowMultiDirection ? direction : (Direction?) null;
        return new RcdBuildSiteKey(tile, proto.ID, dir, null);
    }

    private RcdBuildSiteKey Key(RcdBuildSiteComponent site)
    {
        EntityUid? target = site.Target.IsValid() ? GetEntity(site.Target) : null;
        Direction? direction = site.UseDirection ? site.Direction : null;
        return new RcdBuildSiteKey(site.Tile, site.ProtoId, direction, target);
    }

    private static Box2 RotateBox(Box2 box, Angle angle)
    {
        var a = angle.RotateVec(new Vector2(box.Left, box.Bottom));
        var b = angle.RotateVec(new Vector2(box.Right, box.Bottom));
        var c = angle.RotateVec(new Vector2(box.Left, box.Top));
        var d = angle.RotateVec(new Vector2(box.Right, box.Top));
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new Box2(min.X, min.Y, max.X, max.Y);
    }

    private static Box2 EnsureSize(Box2 box)
    {
        var width = MathF.Max(box.Width, 0.4f);
        var height = MathF.Max(box.Height, 0.4f);
        var center = box.Center;
        return new Box2(center.X - width / 2f, center.Y - height / 2f, center.X + width / 2f, center.Y + height / 2f);
    }

    private static Box2 Union(Box2 left, Box2 right)
    {
        return new Box2(
            MathF.Min(left.Left, right.Left),
            MathF.Min(left.Bottom, right.Bottom),
            MathF.Max(left.Right, right.Right),
            MathF.Max(left.Top, right.Top));
    }
}

public readonly record struct RcdBuildSiteKey(
    Vector2i Tile,
    ProtoId<RCDPrototype> ProtoId,
    Direction? Direction,
    EntityUid? Target);

[RegisterComponent]
public sealed partial class RcdBuildSiteIndexComponent : Component
{
    public Dictionary<RcdBuildSiteKey, EntityUid> Sites = new();
}
