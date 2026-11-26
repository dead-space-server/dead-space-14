// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.TheCircle.Cardinal;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// Feeds the cardinal's screams to <c>cardinal_scream.swsl</c>: a lens punch, then ripples pouring out to the edge of its
/// reach for a second or so, with streaks of sound flying off, fading at the end.
/// </summary>
public sealed class CardinalScreamWaveOverlay : Overlay
{
    private const int MaxWaves = 4;

    /// <summary>How long a scream fills the air, and how quickly it reaches the edge of its reach.</summary>
    private const float Life = 1.3f;
    private const float Grow = 0.22f;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "CardinalScream";

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;

    private readonly Vector2[] _centers = new Vector2[MaxWaves];
    private readonly float[] _radii = new float[MaxWaves];
    private readonly float[] _times = new float[MaxWaves];
    private readonly float[] _strengths = new float[MaxWaves];
    private readonly Color[] _colors = new Color[MaxWaves];
    private int _count;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public CardinalScreamWaveOverlay(IEntityManager entities, IGameTiming timing, SharedTransformSystem transform,
        IPrototypeManager prototypes)
    {
        _entities = entities;
        _timing = timing;
        _transform = transform;
        _shader = prototypes.Index(ShaderId).InstanceUnique();
        ZIndex = 5;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _count = 0;
        if (args.Viewport.Eye == null)
            return false;

        var now = _timing.CurTime;
        var query = _entities.EntityQueryEnumerator<CardinalScreamWaveComponent, TransformComponent>();
        while (query.MoveNext(out _, out var wave, out var xform) && _count < MaxWaves)
        {
            if (xform.MapID != args.MapId || wave.InitTime == TimeSpan.Zero || wave.Duration <= 0f)
                continue;

            var elapsed = (float) (now - wave.InitTime).TotalSeconds;
            if (elapsed < 0f || elapsed >= Life)
                continue;

            var world = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(wave.Radius + 1f).Contains(world))
                continue;

            var local = args.Viewport.WorldToLocal(world);
            local.Y = args.Viewport.Size.Y - local.Y;

            // Bursts in at once, holds, then dies away over the last part.
            var rise = Math.Clamp(elapsed / 0.05f, 0f, 1f);
            var fade = 1f - Math.Clamp((elapsed - Life * 0.55f) / (Life * 0.45f), 0f, 1f);
            _centers[_count] = local;
            _radii[_count] = 0.5f + (wave.Radius - 0.5f) * MathF.Min(1f, elapsed / Grow);
            _times[_count] = elapsed;
            _strengths[_count] = rise * fade * fade;
            _colors[_count] = wave.Color;
            _count++;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("renderScale", args.Viewport.RenderScale * args.Viewport.Eye.Scale);
        _shader.SetParameter("count", _count);
        _shader.SetParameter("centers", _centers);
        _shader.SetParameter("radii", _radii);
        _shader.SetParameter("times", _times);
        _shader.SetParameter("strengths", _strengths);
        _shader.SetParameter("colors", _colors);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
