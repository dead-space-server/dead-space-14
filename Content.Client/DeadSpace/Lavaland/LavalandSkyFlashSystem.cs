// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland;

public sealed class LavalandSkyFlashSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private LavalandSkyFlashOverlay? _flash;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LavalandSkyFlashEvent>(OnFlash);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_flash != null)
            _overlay.RemoveOverlay(_flash);
        _flash = null;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _flash?.FrameUpdate(frameTime);
    }

    private void OnFlash(LavalandSkyFlashEvent ev)
    {
        if (_flash == null)
        {
            _flash = new LavalandSkyFlashOverlay();
            _overlay.AddOverlay(_flash);
        }

        _flash.Show(ev.Duration, ev.Strength);
    }
}

/// <summary>The whole screen white for a moment, fading out.</summary>
public sealed class LavalandSkyFlashOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private float _duration;
    private float _elapsed;
    private float _strength;

    public LavalandSkyFlashOverlay()
    {
        ZIndex = 190;
    }

    public void Show(float duration, float strength)
    {
        _duration = MathF.Max(0.05f, duration);
        _strength = Math.Clamp(strength, 0f, 1f);
        _elapsed = 0f;
    }

    public void FrameUpdate(float frameTime)
    {
        if (_elapsed < _duration)
            _elapsed += frameTime;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _elapsed < _duration;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // Bright at once, then fading out.
        var alpha = _strength * (1f - _elapsed / _duration);
        args.ScreenHandle.DrawRect(args.ViewportBounds, Color.White.WithAlpha(alpha));
    }
}
