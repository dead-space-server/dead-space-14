using Content.Client.DeadSpace.UserInterface.Hologram.Sound; // DS14
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;

namespace Content.Client.Audio;

public sealed partial class AudioUIController : UIController
{
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IResourceCache _cache = default!;

    private float _interfaceGain;
    private IAudioSource? _clickSource;
    private IAudioSource? _hoverSource;

    private const float ClickGain = 0.25f;
    private const float HoverGain = 0.05f;

    // DS14-start
    private UISoundOverride? _soundOverride;
    private float CurrentClickGain => _soundOverride?.ClickGain ?? ClickGain;
    private float CurrentHoverGain => _soundOverride?.HoverGain ?? HoverGain;
    // DS14-end

    public override void Initialize()
    {
        base.Initialize();

        /*
         * This exists to load UI sounds outside of the game sim.
         */

        // No unsub coz never shuts down until program exit.
        _configManager.OnValueChanged(CCVars.UIClickSound, SetClickSound, true);
        _configManager.OnValueChanged(CCVars.UIHoverSound, SetHoverSound, true);

        _configManager.OnValueChanged(CCVars.InterfaceVolume, SetInterfaceVolume, true);
    }

    private void SetInterfaceVolume(float obj)
    {
        _interfaceGain = obj;

        if (_clickSource != null)
        {
            _clickSource.Gain = CurrentClickGain * _interfaceGain; // DS14
        }

        if (_hoverSource != null)
        {
            _hoverSource.Gain = CurrentHoverGain * _interfaceGain; // DS14
        }
    }

    private void SetClickSound(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var resource = GetSoundOrFallback(_soundOverride?.Click ?? value, CCVars.UIClickSound.DefaultValue); // DS14
            var source =
                _audioManager.CreateAudioSource(resource);

            if (source != null)
            {
                source.Gain = CurrentClickGain * _interfaceGain; // DS14
                source.Global = true;
            }

            _clickSource = source;
            UIManager.SetClickSound(source);
        }
        else
        {
            UIManager.SetClickSound(null);
        }
    }

    private void SetHoverSound(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var hoverResource = GetSoundOrFallback(_soundOverride?.Hover ?? value, CCVars.UIHoverSound.DefaultValue); // DS14
            var hoverSource =
                _audioManager.CreateAudioSource(hoverResource);

            if (hoverSource != null)
            {
                hoverSource.Gain = CurrentHoverGain * _interfaceGain; // DS14
                hoverSource.Global = true;
            }

            _hoverSource = hoverSource;
            UIManager.SetHoverSound(hoverSource);
        }
        else
        {
            UIManager.SetHoverSound(null);
        }
    }

    // DS14-start
    /// <summary>
    /// Swaps the click and hover sounds for another set with its own loudness, e.g. the "Dead Space" interface
    /// style's; null puts the server's sounds back. A sound the server turned off stays off.
    /// </summary>
    public void SetSoundOverride(UISoundOverride? soundOverride)
    {
        if (_soundOverride == soundOverride)
            return;

        _soundOverride = soundOverride;
        SetClickSound(_configManager.GetCVar(CCVars.UIClickSound));
        SetHoverSound(_configManager.GetCVar(CCVars.UIHoverSound));
    }
    // DS14-end

    private AudioResource GetSoundOrFallback(string path, string fallback)
    {
        if (!_cache.TryGetResource(path, out AudioResource? resource))
            return _cache.GetResource<AudioResource>(fallback);

        return resource;
    }
}
