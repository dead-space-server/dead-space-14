using System.Numerics;
using Content.Client.Audio;
using Content.Client.UserInterface.Controls;
using Content.Client.Viewport;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.GameTicking;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client.DeadSpace.Lavaland.Bosses;

public sealed class LavalandBossHudSystem : EntitySystem
{
    private const float HudTopMargin = 14f;
    private const float BossMusicMutedVolumeOffset = -32f;
    private const float BossMusicFadeInDuration = 1.75f;
    private const float BossMusicFadeOutDuration = 2.5f;

    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private LavalandBossHudControl? _hud;
    private LayoutContainer? _viewportRoot;
    private EntityUid? _musicStream;
    private ResolvedSoundSpecifier? _musicSpecifier;
    private AudioParams? _musicBaseParams;
    private int? _activeArena;
    private bool _bossMusicEnabled = true;
    private float _bossMusicVolume = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<LavalandBossHudUpdateEvent>(OnHudUpdate);
        SubscribeNetworkEvent<LavalandBossHudHideEvent>(OnHudHide);
        SubscribeNetworkEvent<LavalandBossMusicStartEvent>(OnMusicStart);
        SubscribeNetworkEvent<LavalandBossMusicStopEvent>(OnMusicStop);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        _cfg.OnValueChanged(CCCCVars.BossMusicEnabled, OnBossMusicEnabledChanged, true);
        _cfg.OnValueChanged(CCCCVars.BossMusicVolume, OnBossMusicVolumeChanged, true);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_hud?.Visible == true)
        {
            EnsureHudRoot();
            UpdateHudPlacement();
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(CCCCVars.BossMusicEnabled, OnBossMusicEnabledChanged);
        _cfg.UnsubValueChanged(CCCCVars.BossMusicVolume, OnBossMusicVolumeChanged);
        ClearHud();
        ClearMusic();
    }

    private void OnHudUpdate(LavalandBossHudUpdateEvent ev)
    {
        var hud = EnsureHud();
        _activeArena = ev.ArenaId;

        hud.Visible = true;
        hud.UpdateState(ev.BossName, ev.CurrentHealth, ev.MaxHealth, ev.Participants);
    }

    private void OnHudHide(LavalandBossHudHideEvent ev)
    {
        if (_activeArena != null && _activeArena != ev.ArenaId)
            return;

        HideHud();
    }

    private void OnMusicStart(LavalandBossMusicStartEvent ev)
    {
        // A boss fight starting during a raid takes the music over.
        if (_activeArena == LavalandBossMusicStartEvent.RaidMusicId && ev.ArenaId != LavalandBossMusicStartEvent.RaidMusicId)
            _activeArena = null;

        if (_activeArena != null && _activeArena != ev.ArenaId)
            return;

        if (_musicStream != null && _activeArena == ev.ArenaId)
            return;

        _activeArena ??= ev.ArenaId;
        _musicSpecifier = ev.Specifier;
        _musicBaseParams = (ev.AudioParams ?? AudioParams.Default).WithLoop(true);

        FadeOutCurrentMusic(false);
        TryStartMusic();
    }

    private void OnMusicStop(LavalandBossMusicStopEvent ev)
    {
        if (_activeArena != null && _activeArena != ev.ArenaId)
            return;

        FadeOutCurrentMusic(true);
        // Music without a HUD (the raid's) leaves nothing behind to block the next fight's music.
        if (_activeArena == ev.ArenaId && (_hud == null || !_hud.Visible))
            _activeArena = null;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        HideHud();
        ClearMusic();
    }

    private LavalandBossHudControl EnsureHud()
    {
        if (_hud != null)
            return _hud;

        _hud = new LavalandBossHudControl
        {
            Visible = false,
        };

        EnsureHudRoot();
        return _hud;
    }

    private void EnsureHudRoot()
    {
        if (_hud == null ||
            _ui.ActiveScreen == null)
        {
            return;
        }

        var mainViewport = _ui.ActiveScreen.GetWidget<MainViewport>();
        if (mainViewport == null)
            return;

        var viewportContainer = _ui.ActiveScreen.FindControl<LayoutContainer>("ViewportContainer");
        if (_viewportRoot?.Parent != viewportContainer)
        {
            _viewportRoot?.Orphan();
            _viewportRoot = new LayoutContainer
            {
                MouseFilter = Control.MouseFilterMode.Ignore,
            };

            viewportContainer.AddChild(_viewportRoot);
            LayoutContainer.SetAnchorPreset(_viewportRoot, LayoutContainer.LayoutPreset.Wide);
            _viewportRoot.SetPositionLast();
        }

        if (_hud.Parent == _viewportRoot)
            return;

        _hud.Orphan();
        _viewportRoot.AddChild(_hud);
        LayoutContainer.SetAnchorPreset(_hud, LayoutContainer.LayoutPreset.TopLeft);
        _hud.SetPositionLast();

        UpdateHudPlacement(mainViewport, viewportContainer);
    }

    private void HideHud()
    {
        if (_hud != null)
            _hud.Visible = false;

        _activeArena = null;
    }

    private void ClearHud()
    {
        _viewportRoot?.Orphan();
        _viewportRoot = null;
        _hud?.Orphan();
        _hud = null;
        _activeArena = null;
    }

    private void ClearMusic()
    {
        FadeOutCurrentMusic(true);
    }

    private void FadeOutCurrentMusic(bool clearMusicData)
    {
        if (_musicStream != null)
            _contentAudio.FadeOut(_musicStream, duration: BossMusicFadeOutDuration);

        _musicStream = null;

        if (!clearMusicData)
            return;

        _musicSpecifier = null;
        _musicBaseParams = null;
    }

    private void TryStartMusic()
    {
        if (!_bossMusicEnabled ||
            _musicStream != null ||
            _musicSpecifier == null)
        {
            return;
        }

        var stream = _audio.PlayGlobal(_musicSpecifier, Filter.Local(), false, CreateBossMusicParams());
        _musicStream = stream?.Entity;

        if (stream != null)
            _contentAudio.FadeIn(_musicStream, stream.Value.Component, BossMusicFadeInDuration);
    }

    private void ApplyMusicVolume()
    {
        if (_musicStream == null)
            return;

        _audio.SetVolume(_musicStream, CreateBossMusicParams().Volume);
    }

    private AudioParams CreateBossMusicParams()
    {
        var audioParams = (_musicBaseParams ?? AudioParams.Default).WithLoop(true);
        var volumeOffset = _bossMusicVolume <= 0.001f
            ? BossMusicMutedVolumeOffset
            : SharedAudioSystem.GainToVolume(_bossMusicVolume);

        return audioParams.AddVolume(volumeOffset);
    }

    private void OnBossMusicEnabledChanged(bool enabled)
    {
        _bossMusicEnabled = enabled;
        if (enabled)
            TryStartMusic();
        else
            FadeOutCurrentMusic(false);
    }

    private void OnBossMusicVolumeChanged(float volume)
    {
        _bossMusicVolume = Math.Clamp(volume, 0f, 1f);
        ApplyMusicVolume();
    }

    private void UpdateHudPlacement()
    {
        if (_ui.ActiveScreen == null)
            return;

        var mainViewport = _ui.ActiveScreen.GetWidget<MainViewport>();
        if (mainViewport == null)
            return;

        var viewportContainer = _ui.ActiveScreen.FindControl<LayoutContainer>("ViewportContainer");
        UpdateHudPlacement(mainViewport, viewportContainer);
    }

    private void UpdateHudPlacement(MainViewport mainViewport, LayoutContainer viewportContainer)
    {
        if (_hud == null ||
            _viewportRoot == null ||
            _viewportRoot.Parent != viewportContainer)
        {
            return;
        }

        var drawBox = GetViewportDrawBox(mainViewport.Viewport);
        var uiScale = Math.Max(0.001f, viewportContainer.UIScale);
        var parentTopLeft = viewportContainer.GlobalPixelPosition;

        var viewportLeft = (drawBox.Left - parentTopLeft.X) / uiScale;
        var viewportTop = (drawBox.Top - parentTopLeft.Y) / uiScale;
        var viewportWidth = drawBox.Width / uiScale;

        var hudWidth = Math.Clamp(520f, 280f, Math.Max(280f, viewportWidth - 32f));
        _hud.SetWidth = hudWidth;
        _hud.Measure(new Vector2(hudWidth, viewportContainer.Size.Y));

        var hudX = viewportLeft + MathF.Max(0f, (viewportWidth - _hud.DesiredSize.X) * 0.5f);
        var hudY = viewportTop + HudTopMargin;

        LayoutContainer.SetMarginLeft(_hud, hudX);
        LayoutContainer.SetMarginTop(_hud, hudY);
        LayoutContainer.SetMarginRight(_hud, 0);
        LayoutContainer.SetMarginBottom(_hud, 0);
    }

    private static UIBox2i GetViewportDrawBox(ScalingViewport viewport)
    {
        var viewportSize = (Vector2) viewport.ViewportSize * Math.Max(1, viewport.CurrentRenderScale);
        var controlSize = (Vector2) viewport.PixelSize;

        if (viewportSize.X <= 0f ||
            viewportSize.Y <= 0f ||
            controlSize.X <= 0f ||
            controlSize.Y <= 0f)
        {
            return UIBox2i.FromDimensions(viewport.GlobalPixelPosition, viewport.PixelSize);
        }

        Vector2 size;
        if (viewport.FixedStretchSize is { } fixedStretchSize)
        {
            size = fixedStretchSize;
        }
        else
        {
            var ratioX = controlSize.X / viewportSize.X;
            var ratioY = controlSize.Y / viewportSize.Y;
            var ratio = viewport.IgnoreDimension switch
            {
                ScalingViewportIgnoreDimension.Vertical => ratioX,
                ScalingViewportIgnoreDimension.Horizontal => ratioY,
                _ => Math.Min(ratioX, ratioY),
            };

            size = viewportSize * ratio;
        }

        var position = (controlSize - size) * 0.5f;
        return UIBox2i.FromDimensions(
            viewport.GlobalPixelPosition + (Vector2i) position,
            (Vector2i) size);
    }
}
