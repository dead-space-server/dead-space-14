using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Client.DeadSpace.Celestial;

/// <summary>
/// Оверлей стартовой катсцены: полупрозрачный чёрный экран,
/// в центре — анимированный спрайт Селестиала (кокон → глаза → разрыв).
/// </summary>
public sealed class CelestialCutsceneOverlay : Overlay
{
    private const string RsiPath = "/Textures/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/celestial.rsi";
    private const float FrameTime = 0.1f;
    private const int FrameCount = 30;
    private const float SpriteSize = 950f; // размер спрайта на экране, px

    private static readonly Color Backdrop = new(0f, 0f, 0f, 0.93f);
    private static readonly Color Pink = new(0xFF / 255f, 0x52 / 255f, 0x98 / 255f);

    private readonly RSI _rsi;

    private bool _active;
    private bool _fadingOut;
    private float _fadeAlpha;
    private int _stage;
    private float _elapsed;

    private const float FadeInTime = 1f;
    private const float FadeOutTime = 1.3f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CelestialCutsceneOverlay(IResourceCache resourceCache)
    {
        _rsi = resourceCache.GetResource<RSIResource>(RsiPath).RSI;
    }

    public void Show()
    {
        _active = true;
        _fadingOut = false;
        _fadeAlpha = 0f; // плавное проявление
        _stage = 0;
        _elapsed = 0f;
    }

    public void BeginFadeOut()
    {
        _fadingOut = true;
    }

    public void SetStage(int stage)
    {
        _stage = MathHelper.Clamp(stage, 0, 2);
        _elapsed = 0f;
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
        var bounds = args.ViewportBounds;

        // плавность: прозрачность фона и всех элементов умножается на фейд
        var fade = _fadeAlpha * _fadeAlpha * (3f - 2f * _fadeAlpha); // smoothstep

        // почти чёрный фон на весь экран (5-10% прозрачности)
        handle.DrawRect(
            new UIBox2(bounds.Left, bounds.Top, bounds.Left + bounds.Width, bounds.Top + bounds.Height),
            new Color(0f, 0f, 0f, 0.93f * fade));

        // большая чёрная сфера с тонкой розовой обводкой: дышит и деформируется
        var center = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f - 40f);
        var baseR = MathF.Min(bounds.Width, bounds.Height) * 0.42f;

        // дыхание: медленный пульс радиуса
        var breathe = 1f + 0.05f * MathF.Sin(_elapsed * 1.6f);
        // деформация: сплющивание по медленно вращающейся оси + резкий "глоток" раз в ~4 сек
        var gulpPhase = Math.Clamp(1f - MathF.Abs((_elapsed % 4f) - 0.35f) / 0.35f, 0f, 1f);
        var squash = 0.09f * MathF.Sin(_elapsed * 2.3f) + 0.13f * gulpPhase;
        var axis = _elapsed * 0.5f;

        handle.SetTransform(center, Angle.FromDegrees(axis), new Vector2(breathe * (1f + squash), breathe * (1f - squash)));
        handle.DrawCircle(Vector2.Zero, baseR, Pink.WithAlpha(0.9f * fade));
        handle.DrawCircle(Vector2.Zero, baseR - 3f, new Color(0f, 0f, 0f, 1f * fade));
        handle.SetTransform(Vector2.Zero, Angle.Zero, Vector2.One);

        // спрайт Селестиала по центру
        var stateName = _stage switch
        {
            1 => "CelestialEyesOpen",
            2 => "cocoon_breach",
            _ => "cocoon_idle",
        };

        if (_rsi.TryGetState(stateName, out var state))
        {
            var frameIndex = (int) (_elapsed / FrameTime) % FrameCount;
            var texture = state.GetFrame(Robust.Shared.Graphics.RSI.RsiDirection.South, frameIndex);

            var size = new Vector2(SpriteSize, SpriteSize);
            var pos = center - size / 2f;

            handle.DrawTextureRect(texture, new UIBox2(pos, pos + size), new Color(1f, 1f, 1f, fade));
        }
    }
}
