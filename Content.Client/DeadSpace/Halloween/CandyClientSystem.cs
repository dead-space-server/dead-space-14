using Content.Shared.DeadSpace.Halloween;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Shared;
using Robust.Shared.Audio;
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
    private const string AnnounceSound = "/Audio/_DeadSpace/Halloween/Candy.ogg";

    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private CandyAnnounceOverlay? _announceOverlay;
    private CandyScreenTintOverlay? _tintOverlay;

    private TimeSpan _tintEndTime = TimeSpan.Zero;
    private TimeSpan _muteEndTime = TimeSpan.Zero;

    private float? _savedMasterVolume;

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

        if (_muteEndTime != TimeSpan.Zero && _timing.CurTime >= _muteEndTime)
            RestoreMasterVolume();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        RestoreMasterVolume();
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
        _savedMasterVolume ??= _cfg.GetCVar(CVars.AudioMasterVolume);

        _cfg.SetCVar(CVars.AudioMasterVolume, 0f);
        _muteEndTime = _timing.CurTime + ev.Duration;
    }

    private void RestoreMasterVolume()
    {
        if (_savedMasterVolume is { } saved)
        {
            _cfg.SetCVar(CVars.AudioMasterVolume, saved);
            _savedMasterVolume = null;
        }

        _muteEndTime = TimeSpan.Zero;
    }
}