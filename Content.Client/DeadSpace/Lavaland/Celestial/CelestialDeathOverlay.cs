using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Maths;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using System.Numerics;

namespace Content.Client.DeadSpace.Lavaland.Celestial;

/// <summary>
/// Катсцена смерти Селестиала: полностью белый экран и живой глаз в центре.
/// Фразы рисуются штатным субтитровым оверлеем в режиме смерти (чёрный текст,
/// чисто белые прямоугольники, зона над глазом).
/// </summary>
public sealed class CelestialDeathOverlay : Overlay
{
    private const string RsiPath = "/Textures/_DeadSpace/Lavaland/Celestial/eye.rsi";

    private static readonly Color Pink = new(0xFF / 255f, 0x52 / 255f, 0x98 / 255f);

    private readonly RSI _eyeRsi;
    private readonly IClyde _windows;

    private bool _active;
    private bool _fadingOut;
    private float _fadeAlpha;
    private float _elapsed;

    // фазы концовки: глаз возвращается в центр, затем инверсия 10 секунд
    private bool _inverted;
    private float _invertElapsed;
    private Vector2 _eyeOffsetAtInvert;
    private const float InvertTime = 10f;
    private const float EyeReturnTime = 1.5f;

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

    public void BeginInvert()
    {
        if (_inverted)
            return;

        // запоминаем текущее смещение глаза, чтобы плавно вернуть в центр
        var driftX = 55f * MathF.Sin(_elapsed * 1.1f);
        var driftY = 35f * MathF.Sin(_elapsed * 0.8f + 2f);
        var jump = (_elapsed % 1f) < 0.12f ? 25f : 0f;
        _eyeOffsetAtInvert = new Vector2(driftX - jump, driftY);

        _inverted = true;
        _invertElapsed = 0f;
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

        if (_inverted)
            _invertElapsed += frameTime;

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

        var windowSize = _windows.MainWindow?.Size ?? (Vector2i) args.ViewportBounds.Size;
        var width = windowSize.X;
        var height = windowSize.Y;
        var center = new Vector2(width / 2f, height / 2f);

        // инверсия: ПЛАВНЫЙ кроссфейд белый->чёрный за первые 3 секунды
        var invertMix = 0f;
        if (_inverted)
        {
            var t = Math.Clamp(_invertElapsed / 3f, 0f, 1f);
            invertMix = t * t * (3f - 2f * t);
        }
        var bgColor = new Color(
            MathHelper.Lerp(1f, 0f, invertMix),
            MathHelper.Lerp(1f, 0f, invertMix),
            MathHelper.Lerp(1f, 0.02f, invertMix));

        handle.DrawRect(new UIBox2(0f, 0f, width, height), new Color(bgColor.R, bgColor.G, bgColor.B, fade));

        // глаз: дрейфует и вращается; в инверсии - возвращается в центр и выворачивается
        if (_eyeRsi.TryGetState("icon", out var eyeState))
        {
            var eyeSize = new Vector2(280f, 280f);
            float driftX, driftY, rotation;

            if (_inverted)
            {
                // плавный возврат в исходную позицию
                var t = Math.Clamp(_invertElapsed / EyeReturnTime, 0f, 1f);
                var ease = t * t * (3f - 2f * t);
                var liveX = 55f * MathF.Sin(_elapsed * 1.1f);
                var liveY = 35f * MathF.Sin(_elapsed * 0.8f + 2f);
                driftX = MathHelper.Lerp(_eyeOffsetAtInvert.X, 0f, ease);
                driftY = MathHelper.Lerp(_eyeOffsetAtInvert.Y, 0f, ease);
                driftX += liveX * (1f - ease);
                driftY += liveY * (1f - ease);
                rotation = 25f * MathF.Sin(_elapsed * 1.4f + 1f) * (1f - ease);
            }
            else
            {
                driftX = 55f * MathF.Sin(_elapsed * 1.1f);
                driftY = 35f * MathF.Sin(_elapsed * 0.8f + 2f);
                rotation = 25f * MathF.Sin(_elapsed * 1.4f + 1f);
            }

            var jump = !_inverted && (_elapsed % 1f) < 0.12f ? 25f : 0f;
            var eyeCenter = center + new Vector2(driftX - jump, driftY);

            // на чёрном фоне глаз слегка подсвечивается белым, чтобы читался
            var tint = _inverted ? new Color(1f, 1f, 1f, fade) : new Color(1f, 1f, 1f, fade);
            handle.SetTransform(eyeCenter, Angle.FromDegrees(rotation), new Vector2(fade, fade));
            handle.DrawTextureRect(eyeState.Frame0, new UIBox2(-eyeSize / 2f, eyeSize / 2f), tint);
            handle.SetTransform(Vector2.Zero, Angle.Zero, Vector2.One);
        }
    }
}
