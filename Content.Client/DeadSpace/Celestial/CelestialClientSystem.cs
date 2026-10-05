using Content.Shared.DeadSpace.Celestial;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Client.DeadSpace.Celestial;

/// <summary>
/// Клиентская сторона Селестиала: субтитры через оверлей и анимация роста сфер.
/// </summary>
public sealed class CelestialClientSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IClyde _clyde = default!;

    private EntityUid? _introMusic;
    private EntityUid? _deathMusic;
    private CelestialCutsceneOverlay? _cutsceneOverlay;
    private CelestialDeathOverlay? _deathOverlay;

    private CelestialSubtitleOverlay? _subtitleOverlay;
    private CelestialBeamOverlay? _beamOverlay;

    public override void Initialize()
    {
        base.Initialize();

        _subtitleOverlay = new CelestialSubtitleOverlay(_entityManager, _resourceCache);
        _overlay.AddOverlay(_subtitleOverlay);
        _beamOverlay = new CelestialBeamOverlay(_entityManager);
        _overlay.AddOverlay(_beamOverlay);
        _cutsceneOverlay = new CelestialCutsceneOverlay(_resourceCache);
        _overlay.AddOverlay(_cutsceneOverlay);
        _deathOverlay = new CelestialDeathOverlay(_resourceCache, _clyde);
        _overlay.AddOverlay(_deathOverlay);

        SubscribeNetworkEvent<CelestialSpeakEvent>(OnSpeak);
        SubscribeNetworkEvent<CelestialBeamVisualEvent>(OnBeam);
        SubscribeNetworkEvent<CelestialCutterEvent>(OnCutter);
        SubscribeNetworkEvent<CelestialCutsceneStartEvent>(OnCutsceneStart);
        SubscribeNetworkEvent<CelestialCutsceneEndEvent>(OnCutsceneEnd);
        SubscribeNetworkEvent<CelestialSpiritStageEvent>(OnSpiritStage);
        SubscribeNetworkEvent<CelestialDeathEvent>(OnDeathStart);
        SubscribeNetworkEvent<CelestialDeathSpeakEvent>(OnDeathSpeak);
        SubscribeNetworkEvent<CelestialDeathEndEvent>(OnDeathEnd);
    }

    public override void Shutdown()
    {
        if (_subtitleOverlay != null)
            _overlay.RemoveOverlay(_subtitleOverlay);
        _subtitleOverlay = null;
        if (_beamOverlay != null)
            _overlay.RemoveOverlay(_beamOverlay);
        _beamOverlay = null;
        if (_cutsceneOverlay != null)
            _overlay.RemoveOverlay(_cutsceneOverlay);
        _cutsceneOverlay = null;
        if (_deathOverlay != null)
            _overlay.RemoveOverlay(_deathOverlay);
        if (_deathMusic != null)
            _audio.Stop(_deathMusic.Value);
        _deathOverlay = null;
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _subtitleOverlay?.FrameUpdate(frameTime);
        _beamOverlay?.FrameUpdate(frameTime);
        _cutsceneOverlay?.FrameUpdate(frameTime);
        _deathOverlay?.FrameUpdate(frameTime);
    }

    private void OnSpeak(CelestialSpeakEvent ev, EntitySessionEventArgs args)
    {
        _subtitleOverlay?.Show(ev.Text, ev.Duration);
    }

    private void OnBeam(CelestialBeamVisualEvent ev)
    {
        _beamOverlay?.Add(ev.Start, ev.End, new MapId(ev.MapId), ev.PinkTime, ev.DarkTime, ev.WidthScale);
    }

    private void OnCutter(CelestialCutterEvent ev)
    {
        _beamOverlay?.AddCutter(ev.Center, new MapId(ev.MapId), ev.BaseAngle, ev.Length, ev.RotateTime, ev.FireTime);
    }

    private void OnCutsceneStart(CelestialCutsceneStartEvent ev)
    {
        _cutsceneOverlay?.Show();

        // музыка катсцены
        var intro = _audio.PlayGlobal(
            "/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/music/Celestial_Intro.ogg",
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(-2f));
        _introMusic = intro?.Entity;

    }

    private void OnCutsceneEnd(CelestialCutsceneEndEvent ev)
    {
        // плавное затухание оверлея
        _cutsceneOverlay?.BeginFadeOut();

        // музыка глушится только после затухания
        var music = _introMusic;
        _introMusic = null;
        Timer.Spawn(TimeSpan.FromSeconds(1.3f), () =>
        {
            if (music != null)
                _audio.Stop(music.Value);
        });
    }

    private void OnSpiritStage(CelestialSpiritStageEvent ev)
    {
        _cutsceneOverlay?.SetStage(ev.Stage);
    }

    private void OnDeathStart(CelestialDeathEvent ev)
    {
        _cutsceneOverlay?.BeginFadeOut();
        _deathOverlay?.Show();

        // музыка концовки
        var deathMusic = _audio.PlayGlobal(
            "/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/music/Celestial_Cutscene_Music.ogg",
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(-2f));
        _deathMusic = deathMusic?.Entity;

    }

    private void OnDeathSpeak(CelestialDeathSpeakEvent ev)
    {
        // тот же рендер, что в фразочках и стартовой катсцене, но в палитре смерти
        _subtitleOverlay?.ShowDeath(ev.Text, ev.Duration);

        // 1 фраза - 1 звук
        _audio.PlayGlobal(
            "/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/sounds/Celestial_Talk_5.ogg",
            Filter.Local(),
            false,
            AudioParams.Default.WithVolume(-4f));
    }

    private void OnDeathEnd(CelestialDeathEndEvent ev)
    {
        // плавное затухание белого экрана
        _deathOverlay?.BeginFadeOut();

        // музыка НЕ глушится - доигрывает до конца сама
    }
}
