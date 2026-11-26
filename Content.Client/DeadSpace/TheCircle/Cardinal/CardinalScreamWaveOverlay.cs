// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.TheCircle.Cardinal;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// A scream as rings of sound: a bright flash at the cardinal, then three pulses rolling out to the edge of its reach,
/// thinning and fading as they go.
/// </summary>
public sealed class CardinalScreamWaveOverlay : Overlay
{
    private const int Segments = 72;
    private const int Pulses = 3;
    private const float PulseLag = 0.16f;

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly Vector2[] _strip = new Vector2[(Segments + 1) * 2];

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CardinalScreamWaveOverlay(IEntityManager entities, IGameTiming timing, SharedTransformSystem transform)
    {
        _entities = entities;
        _timing = timing;
        _transform = transform;
        ZIndex = 5;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = _timing.CurTime;
        var query = _entities.EntityQueryEnumerator<CardinalScreamWaveComponent, TransformComponent>();
        while (query.MoveNext(out _, out var wave, out var xform))
        {
            if (xform.MapID != args.MapId || wave.InitTime == TimeSpan.Zero || wave.Duration <= 0f)
                continue;

            var t = (float) (now - wave.InitTime).TotalSeconds / wave.Duration;
            if (t < 0f || t > 1f + PulseLag * (Pulses - 1))
                continue;

            var center = _transform.GetWorldPosition(xform);
            if (!args.WorldBounds.Enlarged(wave.Radius).Contains(center))
                continue;

            // The flash at the mouth of the scream.
            if (t < 0.3f)
            {
                var flash = 1f - t / 0.3f;
                handle.DrawCircle(center, 0.6f + t * 2f, wave.Color.WithAlpha(0.3f * flash));
            }

            for (var i = 0; i < Pulses; i++)
            {
                var p = t - i * PulseLag;
                if (p <= 0f || p >= 1f)
                    continue;

                // Fast out of the mouth, slowing towards the edge.
                var eased = 1f - (1f - p) * (1f - p);
                var radius = 0.4f + (wave.Radius - 0.4f) * eased;
                var width = (0.45f - 0.3f * p) * (i == 0 ? 1f : 0.7f);
                var alpha = (1f - p) * (i == 0 ? 0.75f : 0.45f);
                Ring(handle, center, radius, width, wave.Color.WithAlpha(alpha));
                // A soft halo just inside the ring.
                Ring(handle, center, MathF.Max(0f, radius - width * 1.6f), width * 1.2f, wave.Color.WithAlpha(alpha * 0.3f));
            }
        }
    }

    private void Ring(DrawingHandleWorld handle, Vector2 center, float radius, float width, Color color)
    {
        var inner = MathF.Max(0f, radius - width / 2f);
        var outer = radius + width / 2f;
        for (var i = 0; i <= Segments; i++)
        {
            var angle = MathF.Tau * i / Segments;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _strip[i * 2] = center + dir * inner;
            _strip[i * 2 + 1] = center + dir * outer;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _strip, color);
    }
}
