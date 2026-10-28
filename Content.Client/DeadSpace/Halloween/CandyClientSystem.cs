using Content.Shared.DeadSpace.Halloween;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Halloween;

/// <summary>
/// Handles client-side candy effects: effect name announcement with sound,
/// green screen tint, and muting all sounds.
/// </summary>
public sealed class CandyClientSystem : EntitySystem
{
    // DS14-start: candy effect sound, played locally with a smooth fade-out
    private const string AnnounceSound = "/Audio/_DeadSpace/Halloween/Candy.ogg";
    private const float FadeOutSeconds = 2f;
    private const float FadeOutVolumeDb = -50f;
    // DS14-end

    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private CandyAnnounceOverlay? _announceOverlay;
    private CandyScreenTintOverlay? _tintOverlay;

    private TimeSpan _tintEndTime = TimeSpan.Zero;
    private TimeSpan _muteEndTime = TimeSpan.Zero;
    // DS14: candy effect sound fade state
    private EntityUid? _announceSound;
    private TimeSpan _announceSoundStart;
    private TimeSpan _announceSoundEnd;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CandyAnnounceEvent>(OnAnnounce);
        SubscribeNetworkEvent<CandyScreenTintEvent>(OnScreenTint);
        SubscribeNetworkEvent<CandyMuteEvent>(OnMute);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _announceOverlay?.FrameUpdate(frameTime);

        if (_tintOverlay != null && _timing.CurTime >= _tintEndTime)
        {
            _overlayMan.RemoveOverlay(_tintOverlay);
            _tintOverlay = null;
        }

        // DS14-start: smooth fade-out of the candy effect sound
        if (_announceSound != null)
        {
            var length = _announceSoundEnd - _announceSoundStart;
            var fadeStart = _announceSoundEnd - TimeSpan.FromSeconds(FadeOutSeconds);
            var now = _timing.CurTime;

            if (now >= _announceSoundEnd || !_audio.IsPlaying(_announceSound))
            {
                _audio.Stop(_announceSound);
                _announceSound = null;
            }
            else if (now >= fadeStart && length > TimeSpan.FromSeconds(FadeOutSeconds))
            {
                var t = (float) ((now - fadeStart) / TimeSpan.FromSeconds(FadeOutSeconds));
                _audio.SetVolume(_announceSound, FadeOutVolumeDb * t);
            }
        }
        // DS14-end

        if (_muteEndTime != TimeSpan.Zero && _timing.CurTime >= _muteEndTime)
        {
            _cfg.SetCVar(CVars.AudioMasterVolume, 1f);
            _muteEndTime = TimeSpan.Zero;
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.SetCVar(CVars.AudioMasterVolume, 1f);
    }

    private void OnAnnounce(CandyAnnounceEvent ev)
    {
        var name = Loc.GetString($"candy-effect-{ev.Effect}");

        if (_announceOverlay == null)
        {
            _announceOverlay = new CandyAnnounceOverlay();
            _overlayMan.AddOverlay(_announceOverlay);
        }

        _announceOverlay.Show(name, 4f);
        _audio.PlayGlobal(new SoundPathSpecifier(AnnounceSound), Filter.Local(), false, AudioParams.Default.WithVolume(-4f));
    }

    // DS14-start: play the sound and smoothly fade it out at the end
    private void StartAnnounceSound()
    {
        if (_announceSound != null)
            _audio.Stop(_announceSound);

        var length = (float) _audio.GetAudioLength(AnnounceSound).TotalSeconds;
                var stream = _audio.PlayGlobal(AnnounceSound, Filter.Local(), false, AudioParams.Default.WithVolume(0f));
        if (stream == null)
            return;

        _announceSound = stream.Value.Entity;
        _announceSoundEnd = _timing.CurTime + TimeSpan.FromSeconds(length);
        _announceSoundStart = _timing.CurTime;
    }

    // DS14-end

    private void OnScreenTint(CandyScreenTintEvent ev)
    {
        if (_tintOverlay == null)
        {
            _tintOverlay = new CandyScreenTintOverlay();
            _overlayMan.AddOverlay(_tintOverlay);
        }

        _tintEndTime = _timing.CurTime + ev.Duration;
    }

    private void OnMute(CandyMuteEvent ev)
    {
        _cfg.SetCVar(CVars.AudioMasterVolume, 0f);
        _muteEndTime = _timing.CurTime + ev.Duration;
    }
}
