// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>Records puffs behind <see cref="LavalandProjectileTrailComponent"/> entities and draws them fading out.</summary>
public sealed class LavalandProjectileTrailSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<TrailPuff> _puffs = new();
    private readonly Dictionary<EntityUid, float> _sinceLast = new();
    private LavalandProjectileTrailOverlay _overlay = default!;
    private float _time;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LavalandProjectileTrailOverlay(this);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _time += frameTime;

        var query = EntityQueryEnumerator<LavalandProjectileTrailComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trail, out var xform))
        {
            var since = _sinceLast.GetValueOrDefault(uid) + frameTime;
            if (since < trail.Interval)
            {
                _sinceLast[uid] = since;
                continue;
            }

            _sinceLast[uid] = 0f;
            var coords = _transform.GetMapCoordinates(uid, xform);
            if (coords.MapId != MapId.Nullspace)
                _puffs.Add(new TrailPuff(coords, _time, trail.Lifetime, trail.Size, trail.Color));
        }

        _puffs.RemoveAll(p => _time - p.Born > p.Lifetime);
        if (_sinceLast.Count > 256)
            _sinceLast.Clear();
    }

    internal IEnumerable<(Vector2 Position, float Radius, Color Color)> Visible(MapId map)
    {
        foreach (var puff in _puffs)
        {
            if (puff.Coords.MapId != map)
                continue;
            var t = Math.Clamp((_time - puff.Born) / puff.Lifetime, 0f, 1f);
            yield return (puff.Coords.Position, puff.Size * (1f - t * 0.85f), puff.Color.WithAlpha(puff.Color.A * (1f - t)));
        }
    }

    private readonly record struct TrailPuff(MapCoordinates Coords, float Born, float Lifetime, float Size, Color Color);
}

public sealed class LavalandProjectileTrailOverlay(LavalandProjectileTrailSystem system) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var (position, radius, color) in system.Visible(args.MapId))
        {
            args.WorldHandle.DrawCircle(position, radius, color);
        }
    }
}
