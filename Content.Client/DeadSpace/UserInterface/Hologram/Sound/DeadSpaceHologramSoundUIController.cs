// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Audio;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Strip;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Sound;

/// <summary>
/// Sounds of the "Dead Space" interface style: soft hologram blips for button clicks and hovers, and a short chirp when
/// a window is projected in or goes out. Follows the interface volume; <see cref="CCCCVars.HologramSounds"/> turns it
/// off and leaves the stock interface sounds.
/// </summary>
public sealed partial class DeadSpaceHologramSoundUIController : UIController
{
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string SoundDirectory = "/Audio/_DeadSpace/Interface/Hologram/";
    private const string OpenSound = SoundDirectory + "open.ogg";
    private const string CloseSound = SoundDirectory + "close.ogg";

    // The files are full scale; these bring them level with the stock click and hover sounds.
    private const float OpenGain = 0.03f;
    private const float CloseGain = 0.03f;

    private static readonly UISoundOverride ButtonSounds = new(
        SoundDirectory + "click.ogg",
        0.05f,
        SoundDirectory + "hover.ogg",
        0.012f);

    /// <summary>Windows opened or closed together (a menu swapping windows, a screen change) chirp once.</summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(80);

    private IAudioSource? _openSource;
    private IAudioSource? _closeSource;
    private TimeSpan _lastOpen;
    private TimeSpan _lastClose;
    private float _interfaceVolume;
    private bool _enabled;
    private bool _overriding;

    private bool Active => _enabled && DeadSpaceStylePalette.Hologram;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCCCVars.HologramSounds, value => _enabled = value, true);
        _cfg.OnValueChanged(CCVars.InterfaceVolume, value => _interfaceVolume = value, true);

        UIManager.WindowRoot.OnChildAdded += OnWindowAdded;
        UIManager.WindowRoot.OnChildRemoved += OnWindowRemoved;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Follows the palette rather than the style cvar, so it doesn't depend on which cvar callback runs first.
        var active = Active;
        if (active == _overriding)
            return;

        _overriding = active;
        UIManager.GetUIController<AudioUIController>().SetSoundOverride(active ? ButtonSounds : null);
    }

    private void OnWindowAdded(Control control)
    {
        if (control is BaseWindow window && !IsInstant(window))
            Play(ref _openSource, OpenSound, OpenGain, ref _lastOpen);
    }

    private void OnWindowRemoved(Control control)
    {
        if (control is BaseWindow window && !IsInstant(window))
            Play(ref _closeSource, CloseSound, CloseGain, ref _lastClose);
    }

    /// <summary>
    /// Bags, boxes and someone else's inventory are opened and closed all the time mid-action: they stay silent, as
    /// they stay instant in <see cref="DeadSpaceHologramUIController"/>.
    /// </summary>
    private static bool IsInstant(BaseWindow window)
    {
        return window is StorageWindow or StrippingMenu;
    }

    private void Play(ref IAudioSource? source, string path, float gain, ref TimeSpan last)
    {
        if (!Active || _interfaceVolume <= 0f || !_cfg.GetCVar(CVars.InterfaceAudio))
            return;

        var now = _timing.RealTime;
        if (now - last < Cooldown)
            return;

        last = now;
        source ??= CreateSource(path);
        if (source == null)
            return;

        source.Gain = gain * _interfaceVolume;
        source.Restart();
    }

    private IAudioSource? CreateSource(string path)
    {
        if (!_cache.TryGetResource(path, out AudioResource? resource))
        {
            Log.Error($"Missing hologram interface sound {path}");
            return null;
        }

        var source = _audio.CreateAudioSource(resource);
        if (source != null)
            source.Global = true;

        return source;
    }
}
