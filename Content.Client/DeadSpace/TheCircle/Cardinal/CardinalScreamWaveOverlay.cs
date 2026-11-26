// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.TheCircle.Cardinal;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// Feeds the cardinal's screams to <c>cardinal_scream.swsl</c>: each one a wave of sound rolling out to the edge of its
/// reach, quick out of the mouth and slowing as it goes, fading on the way.
/// </summary>
public sealed class CardinalScreamWaveOverlay : Overlay
{
    private const int MaxWaves = 4;
    private static readonly ProtoId<ShaderPrototype> ShaderId = "CardinalScream";

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;

    private readonly Vector2[] _centers = new Vector2[MaxWaves];
    private readonly float[] _radii = new float[MaxWaves];
    private readonly float[] _widths = new float[MaxWaves];
    private readonly float[] _strengths = new float[MaxWaves];
    private readonly float[] _times = new float[MaxWaves];
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

            var t = (float) (now - wave.InitTime).TotalSeconds / wave.Duration;
            if (t is < 0f or >= 1f)
                continue;

            var world = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(wave.Radius + 1f).Contains(world))
                continue;

            var local = args.Viewport.WorldToLocal(world);
            local.Y = args.Viewport.Size.Y - local.Y;

            var eased = 1f - (1f - t) * (1f - t);
            var rise = Math.Clamp(t / 0.08f, 0f, 1f);
            _centers[_count] = local;
            _radii[_count] = 0.3f + (wave.Radius - 0.3f) * eased;
            _widths[_count] = 0.28f + 0.3f * t;
            _strengths[_count] = rise * MathF.Pow(1f - t, 1.3f);
            _times[_count] = t * wave.Duration;
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
        _shader.SetParameter("widths", _widths);
        _shader.SetParameter("strengths", _strengths);
        _shader.SetParameter("times", _times);
        _shader.SetParameter("colors", _colors);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
