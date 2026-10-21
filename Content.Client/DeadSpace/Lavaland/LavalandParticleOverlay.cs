// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Draws <see cref="LavalandParticleSystem"/> particles over the lit world, so the glowing ones shine in the dark.
/// Round particles are low-poly circles built into one vertex list and drawn in a single call:
/// a halo and a core per particle cost no more than one batch for the whole screen.
/// </summary>
public sealed class LavalandParticleOverlay : Overlay
{
    /// <summary>Sides of a particle circle: round enough at this size, far cheaper than a real circle.</summary>
    private const int Segments = 8;

    private static readonly Vector2[] Unit = BuildUnit();

    private readonly LavalandParticleSystem _system;
    private DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[1024];
    private int _count;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public LavalandParticleOverlay(LavalandParticleSystem system)
    {
        _system = system;
        ZIndex = 5;
    }

    private static Vector2[] BuildUnit()
    {
        var unit = new Vector2[Segments];
        for (var i = 0; i < Segments; i++)
        {
            var angle = MathF.Tau * i / Segments;
            unit[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        return unit;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.Particles.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(1f);
        _count = 0;

        foreach (var p in _system.Particles)
        {
            if (p.Map != args.MapId || !bounds.Contains(p.Position))
                continue;

            var alpha = LavalandParticleSystem.Alpha(p);
            if (alpha <= 0.01f)
                continue;

            switch (p.Kind)
            {
                case LavalandParticleKind.Petal:
                {
                    var box = Box2.CenteredAround(p.Position, new Vector2(p.Size * 1.6f, p.Size));
                    handle.DrawRect(new Box2Rotated(box, new Angle(p.Age * p.Spin), p.Position), p.Color.WithAlpha(alpha));
                    break;
                }

                case LavalandParticleKind.Ash:
                {
                    var box = Box2.CenteredAround(p.Position, new Vector2(p.Size));
                    handle.DrawRect(new Box2Rotated(box, new Angle(p.Age * p.Spin * 0.3f), p.Position), p.Color.WithAlpha(alpha));
                    break;
                }

                case LavalandParticleKind.Cloud:
                    AddCircle(p.Position, p.Size, p.Color.WithAlpha(alpha));
                    break;

                default:
                    // A soft halo under a bright core.
                    AddCircle(p.Position, p.Size * 2.6f, p.Color.WithAlpha(alpha * 0.16f));
                    AddCircle(p.Position, p.Size, p.Color.WithAlpha(alpha));
                    break;
            }
        }

        if (_count > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices.AsSpan(0, _count));
    }

    private void AddCircle(Vector2 center, float radius, Color color)
    {
        if (_count + Segments * 3 > _vertices.Length)
            Array.Resize(ref _vertices, _vertices.Length * 2);

        // Vertex colours are in linear space.
        var linear = Color.FromSrgb(color);
        var previous = center + Unit[Segments - 1] * radius;
        for (var i = 0; i < Segments; i++)
        {
            var next = center + Unit[i] * radius;
            _vertices[_count++] = new DrawVertexUV2DColor(center, linear);
            _vertices[_count++] = new DrawVertexUV2DColor(previous, linear);
            _vertices[_count++] = new DrawVertexUV2DColor(next, linear);
            previous = next;
        }
    }
}
