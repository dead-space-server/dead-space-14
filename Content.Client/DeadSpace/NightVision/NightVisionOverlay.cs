using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Client.DeadSpace.Components.NightVision;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.NightVision;

public sealed partial class NightVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _greyscaleShader;
    private readonly ShaderInstance _desaturateShader;
    private readonly ShaderInstance _circleMaskShader;
    private NightVisionComponent? _nightVisionComponent;

    private static readonly ProtoId<ShaderPrototype> GreyscaleFullscreenId = "GreyscaleFullscreen";
    private static readonly ProtoId<ShaderPrototype> DesaturateFullscreenId = "DesaturateFullscreen";
    private static readonly ProtoId<ShaderPrototype> ColorCircleMaskId = "PNVMask";

    private float _transitionProgress = 0f;

    private bool _readyForPlayback = true;
    public NightVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _greyscaleShader = _prototypeManager.Index(GreyscaleFullscreenId).InstanceUnique();
        _desaturateShader = _prototypeManager.Index(DesaturateFullscreenId).InstanceUnique();
        _circleMaskShader = _prototypeManager.Index(ColorCircleMaskId).InstanceUnique();
    }

    public float GetTransitionProgress() => _transitionProgress;

    public void Reset()
    {
        _transitionProgress = 0f;
        _readyForPlayback = true;
        _nightVisionComponent = null;
    }

    public void SetTransitionProgress(float value) => _transitionProgress = value;

    public void SetSoundBeenPlayed(bool state) => _readyForPlayback = state;

    public bool SoundBeenPlayed() => _readyForPlayback;
    public bool IsRunning()
    {
        return _nightVisionComponent != null
            && _nightVisionComponent.IsNightVision
            && _transitionProgress >= 1f;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(
                _playerManager.LocalSession?.AttachedEntity,
                out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        var playerEntity = _playerManager.LocalSession?.AttachedEntity;
        if (playerEntity == null)
            return false;

        if (!_entityManager.TryGetComponent<NightVisionComponent>(playerEntity, out var nvComp))
            return false;

        _nightVisionComponent = nvComp;

        return _nightVisionComponent.IsNightVision;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || _nightVisionComponent == null)
            return;

        var playerEntity = _playerManager.LocalSession?.AttachedEntity;
        if (playerEntity == null)
            return;

        var delta = (float) _timing.FrameTime.TotalSeconds;

        if (_nightVisionComponent.Animation)
        {
            if (_nightVisionComponent.IsNightVision)
                _transitionProgress = MathF.Min(1f, _transitionProgress + _nightVisionComponent.TransitionSpeed * delta);
            else
                _transitionProgress = MathF.Max(0f, _transitionProgress - _nightVisionComponent.TransitionSpeed * delta);
        }

        if (!_nightVisionComponent.IsNightVision)
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;

        if (_entityManager.TryGetComponent<EyeComponent>(playerEntity, out var eye))
            _circleMaskShader.SetParameter("Zoom", eye.Zoom.X / 14);

        if (IsRunning())
        {
            if (_nightVisionComponent.Desaturation.HasValue)
            {
                _desaturateShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
                _desaturateShader.SetParameter("Desaturation", _nightVisionComponent.Desaturation.Value);
                worldHandle.UseShader(_desaturateShader);
            }
            else
            {
                _greyscaleShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
                worldHandle.UseShader(_greyscaleShader);
            }
            worldHandle.DrawRect(viewport, Color.White);
        }

        _circleMaskShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _circleMaskShader.SetParameter("Color", IsRunning()
            ? _nightVisionComponent.Color
            : Color.Transparent);
        _circleMaskShader.SetParameter("TransitionProgress", 1f - _transitionProgress);

        worldHandle.UseShader(_circleMaskShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}