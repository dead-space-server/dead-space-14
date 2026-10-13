using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Maths;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using System.Numerics;

namespace Content.Client.DeadSpace.Celestial;

/// <summary>
/// Катсцена смерти Селестиала: полностью белый экран и живой глаз в центре.
/// Фразы рисуются штатным субтитровым оверлеем в режиме смерти (чёрный текст,
/// чисто белые прямоугольники, зона над глазом).
/// </summary>
public sealed class CelestialDeathOverlay : Overlay
{
    private const string RsiPath = "/Textures/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/eye.rsi";

    private readonly RSI _eyeRsi;
    private readonly IClyde _windows;

    private bool _active;
    private bool _fadingOut;
    private float _fadeAlpha;
    private float _elapsed;

    private const float FadeInTime = 1f;
    private const float FadeOutTime = 1.3f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CelestialDeathOverlay(IResourceCache resourceCache, IClyde windows)
    {
        _eyeRsi = resourceCache.GetResource<RSIResource>(RsiPath).RSI;
        _windows = windows;
        ZIndex = 5; // ниже субтитровых фраз (ZIndex 10)
    }

    public void Show()
    {
        _active = true;
        _fadingOut = false;
        _fadeAlpha = 0f; // плавное проявление
        _elapsed = 0f;
    }

    public void BeginFadeOut()
    {
        _fadingOut = true;
    }

    public void Hide()
    {
        _active = false;
    }

    public void FrameUpdate(float frameTime)
    {
        if (!_active)
            return;

        _elapsed += frameTime;

        if (_fadingOut)
        {
            _fadeAlpha -= frameTime / FadeOutTime;
            if (_fadeAlpha <= 0f)
            {
                _fadeAlpha = 0f;
                _active = false;
            }
        }
        else
        {
            _fadeAlpha = MathF.Min(1f, _fadeAlpha + frameTime / FadeInTime);
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_active)
            return;

        var handle = args.ScreenHandle;

        // плавность
        var fade = _fadeAlpha * _fadeAlpha * (3f - 2f * _fadeAlpha);

        // белый фон на ВСЁ окно, включая зоны вне вьюпорта
        var windowSize = _windows.MainWindow?.Size ?? (Vector2i) args.ViewportBounds.Size;
        var width = windowSize.X;
        var height = windowSize.Y;
        var center = new Vector2(width / 2f, height / 2f);

        handle.DrawRect(new UIBox2(0f, 0f, width, height), new Color(1f, 1f, 1f, fade));

        // глаз: дрейфует, меняет положение и вращается - как буква, но сильнее
        if (_eyeRsi.TryGetState("icon", out var eyeState))
        {
            var eyeSize = new Vector2(280f, 280f);
            var driftX = 55f * MathF.Sin(_elapsed * 1.1f);
            var driftY = 35f * MathF.Sin(_elapsed * 0.8f + 2f);
            var jump = (_elapsed % 1f) < 0.12f ? 25f : 0f; // редкий резкий скачок
            var rotation = 25f * MathF.Sin(_elapsed * 1.4f + 1f);
            var eyeCenter = center + new Vector2(driftX - jump, driftY);

            handle.SetTransform(eyeCenter, Angle.FromDegrees(rotation), new Vector2(fade, fade));
            handle.DrawTextureRect(eyeState.Frame0, new UIBox2(-eyeSize / 2f, eyeSize / 2f));
            handle.SetTransform(Vector2.Zero, Angle.Zero, Vector2.One);
        }
    }
}
