using Robust.Client.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using System.Numerics;


namespace Content.Client.Sectants;

public sealed class SectantFlashOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly Color _color;
    private readonly float _duration;
    private float _timeLeft;

    public SectantFlashOverlay(Color color, float duration)
    {
        _color = color;
        _duration = duration;
        _timeLeft = duration;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _timeLeft -= args.DeltaSeconds;
        if (_timeLeft <= 0)
            Dispose();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var t = Math.Clamp(_timeLeft / _duration, 0f, 1f);

        // Пульсирующая альфа: сначала резко, потом плавно
        var alpha = t * t * 0.6f;

        var handle = args.ScreenHandle;
        handle.SetTransform(Matrix3x2.Identity);

        // Вспышка на весь экран
        handle.DrawRect(args.Viewport, _color.WithAlpha(alpha));

        // Винетка по краям — усиливаем насыщенность
        var vignette = _color.WithAlpha(t * 0.85f);
        var border = 64f; // толщина винетки в пикселях
        var vp = args.Viewport;
        handle.DrawRect(new UIBox2(vp.Left, vp.Top, vp.Right, vp.Top + border), vignette);
        handle.DrawRect(new UIBox2(vp.Left, vp.Bottom - border, vp.Right, vp.Bottom), vignette);
        handle.DrawRect(new UIBox2(vp.Left, vp.Top, vp.Left + border, vp.Bottom), vignette);
        handle.DrawRect(new UIBox2(vp.Right - border, vp.Top, vp.Right, vp.Bottom), vignette);
    }
}
