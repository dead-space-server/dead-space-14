using System.Linq;
using Content.Client.Corvax.TTS;
using Content.Shared.Corvax.TTS;
using Content.Shared.DeadSpace.TTS;
using Content.Shared.Preferences;
using Robust.Shared.Timing;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<TTSVoicePrototype> _voiceList = new();

    private void InitializeVoice()
    {
        _cfgManager.OnValueChanged(TTSProviderCVars.Provider, OnTtsProviderChanged, true); // DS14

        VoiceButton.OnItemSelected += args =>
        {
            VoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTTS();
    }

    // DS14-start
    private void OnTtsProviderChanged(string provider)
    {
        _voiceList = _prototypeManager.EnumeratePrototypes<TTSVoicePrototype>()
            .Where(voice => voice.RoundStart && voice.Provider == provider)
            .OrderBy(voice => Loc.GetString(voice.Name))
            .ToList();
        UpdateTTSVoicesControls();
    }
    // DS14-end

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null)
            return;

        VoiceButton.Clear();

        var firstVoiceChoiceId = -1; // DS14
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            VoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == -1) // DS14
                firstVoiceChoiceId = i;

            // Не спонсоры могут прослушивать голоса в лобби
            // if (voice.SponsorOnly)
            // {
            //     if (!IoCManager.Resolve<SponsorsManager>().TryGetInfo(out var sponsor))
            //     {
            //         VoiceButton.SetItemDisabled(VoiceButton.GetIdx(i), true);
            //     }
            //     else if (!sponsor.AllowedMarkings.Contains(voice.ID))
            //     {
            //         VoiceButton.SetItemDisabled(VoiceButton.GetIdx(i), true);
            //     }
            // }
        }

        // DS14-start
        var selectedVoice = Profile.GetVoice(_cfgManager.GetCVar(TTSProviderCVars.Provider), _prototypeManager);
        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == selectedVoice);
        VoiceButton.Disabled = _readOnly || firstVoiceChoiceId == -1;
        VoicePlayButton.Disabled = firstVoiceChoiceId == -1;
        // DS14-end
        if (!VoiceButton.TrySelectId(voiceChoiceId) &&
            VoiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetVoice(_voiceList[firstVoiceChoiceId].ID);
        }

        // DS14-TTS-search-start
        VoiceButton.ResetSearch();
        // DS14-TTS-search-end
    }

    private void PlayPreviewTTS()
    {
        if (Profile is null)
            return;

        _entManager.System<TTSSystem>().RequestPreviewTTS(Profile.GetVoice(_cfgManager.GetCVar(TTSProviderCVars.Provider), _prototypeManager)); // DS14
    }
}
