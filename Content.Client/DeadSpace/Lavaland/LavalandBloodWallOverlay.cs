// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.Components;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>Draws Bubblegum's walls of blood as the Circle's blink blood: drops churning over every tile.</summary>
public sealed class LavalandBloodWallSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new LavalandBloodWallOverlay(EntityManager, _timing));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<LavalandBloodWallOverlay>();
    }
}

public sealed class LavalandBloodWallOverlay : Overlay
{
    private const int Drops = 9;
    private const float FadeTime = 0.35f;

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    /// <summary>When each wall was first seen here: it thickens in from then.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _seen = new();
    private readonly List<EntityUid> _gone = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public LavalandBloodWallOverlay(IEntityManager entities, IGameTiming timing)
    {
        _entities = entities;
        _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var now = _timing.RealTime;
        var handle = args.WorldHandle;
        var query = _entities.EntityQueryEnumerator<LavalandBloodWallComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var wall, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!_seen.TryGetValue(uid, out var since))
            {
                since = now;
                _seen[uid] = since;
            }

            var age = (float) (now - since).TotalSeconds;
            var fade = Math.Clamp(Math.Min(age / FadeTime, (wall.Lifetime - age) / FadeTime), 0f, 1f);
            if (fade <= 0f)
                continue;

            var center = _transform.GetWorldPosition(xform);
            var seed = uid.Id * 0.618f;
            for (var i = 0; i < Drops; i++)
            {
                // Each drop circles slowly around its own spot in the tile: the blood churns.
                var phase = seed + i * 2.399963f;
                var spot = new Vector2(MathF.Cos(phase), MathF.Sin(phase)) * (0.12f + 0.26f * (i % 3) / 2f);
                var swirl = new Vector2(MathF.Cos(age * 2.1f + phase), MathF.Sin(age * 1.7f + phase)) * 0.06f;
                var position = center + spot + swirl;
                var size = 0.16f + 0.05f * MathF.Sin(age * 3f + phase);

                handle.DrawCircle(position, size + 0.06f, new Color(0.28f, 0.005f, 0.008f, fade * 0.72f));
                handle.DrawCircle(position, size, new Color(0.72f, 0.015f, 0.025f, fade * 0.82f));
                handle.DrawCircle(position - swirl * 1.4f, size * 0.45f, new Color(0.72f, 0.015f, 0.025f, fade * 0.82f));
            }
        }

        _gone.Clear();
        foreach (var uid in _seen.Keys)
        {
            if (!_entities.HasComponent<LavalandBloodWallComponent>(uid))
                _gone.Add(uid);
        }

        foreach (var uid in _gone)
            _seen.Remove(uid);
    }
}
