// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Sandevistan;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Sandevistan;

public sealed partial class SandevistanOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "SandevistanOverlay";
    private const float FadeDuration = 2.5f;
    private const float SoftcapRampLeadTime = 2f;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;
    private float _intensity;
    private float _motionScale = 1f;

    public SandevistanOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 8;
        _config.OnValueChanged(CCVars.ReducedMotion, OnReducedMotionChanged, invokeImmediately: true);
    }

    public void Reset()
    {
        _intensity = 0f;
    }

    private void OnReducedMotionChanged(bool reducedMotion)
    {
        _motionScale = reducedMotion ? 0f : 1f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (player != null &&
            !_entityManager.HasComponent<ActiveSandevistanComponent>(player.Value) &&
            _entityManager.TryGetComponent<SandevistanVisualFadeoutComponent>(player.Value, out var fadeout) &&
            !fadeout.AllowRampIn)
        {
            return;
        }

        _intensity = Math.Min(1f, _intensity + args.DeltaSeconds / FadeDuration);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (player == null)
            return false;

        if (_entityManager.TryGetComponent<ActiveSandevistanComponent>(player.Value, out var active))
            return GetVisualIntensity(active) > 0f;

        return _entityManager.TryGetComponent<SandevistanVisualFadeoutComponent>(player.Value, out var fadeout) &&
            GetVisualIntensity(fadeout) > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var player = _playerManager.LocalEntity;
        if (player == null)
        {
            return;
        }

        var intensity = 0f;
        var softcapIntensity = 0f;
        if (_entityManager.TryGetComponent<ActiveSandevistanComponent>(player.Value, out var active))
        {
            intensity = GetVisualIntensity(active);
            softcapIntensity = GetSoftcapIntensity(active);
        }
        else if (_entityManager.TryGetComponent<SandevistanVisualFadeoutComponent>(player.Value, out var fadeout))
        {
            intensity = GetVisualIntensity(fadeout);
            softcapIntensity = GetSoftcapIntensity(fadeout);
        }
        else
        {
            return;
        }

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("Intensity", intensity);
        _shader.SetParameter("SoftcapIntensity", softcapIntensity);
        _shader.SetParameter("MotionScale", _motionScale);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private float GetVisualIntensity(ActiveSandevistanComponent active)
    {
        var remaining = Math.Max(0f, (float) (active.EndTime - _timing.CurTime).TotalSeconds);
        var fadeOut = SmoothStep(Math.Clamp(remaining / FadeDuration, 0f, 1f));

        return Math.Min(_intensity, fadeOut);
    }

    private float GetVisualIntensity(SandevistanVisualFadeoutComponent fadeout)
    {
        var remaining = Math.Max(0f, (float) (fadeout.EndTime - _timing.CurTime).TotalSeconds);
        var fadeOut = SmoothStep(Math.Clamp(remaining / Math.Max(fadeout.Duration, 0.1f), 0f, 1f));

        return Math.Min(_intensity, Math.Clamp(fadeout.StartIntensity, 0f, 1f) * fadeOut);
    }

    private float GetSoftcapIntensity(ActiveSandevistanComponent active)
    {
        var rampStart = active.SoftcapTime - TimeSpan.FromSeconds(SoftcapRampLeadTime);
        if (_timing.CurTime < rampStart)
            return 0f;

        var elapsed = Math.Max(0f, (float) (_timing.CurTime - rampStart).TotalSeconds);
        var progress = SmoothStep(Math.Clamp(elapsed / SoftcapRampLeadTime, 0f, 1f));

        return progress * GetVisualIntensity(active);
    }

    private float GetSoftcapIntensity(SandevistanVisualFadeoutComponent fadeout)
    {
        return Math.Clamp(fadeout.SoftcapProgress, 0f, 1f) * GetVisualIntensity(fadeout);
    }

    private static float SmoothStep(float progress)
    {
        return progress * progress * (3f - 2f * progress);
    }
}
