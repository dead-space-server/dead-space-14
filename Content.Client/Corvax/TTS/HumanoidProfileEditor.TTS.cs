using System.Linq;
using Content.Client.Corvax.TTS;
using Content.Shared.Corvax.TTS;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<TTSVoicePrototype> _voiceList = new();

    private void InitializeVoice()
    {
        if (VoiceButton == null)
            return;

        _voiceList = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(x => x.RoundStart)
            .OrderBy(x => Loc.GetString(x.Name))
            .ToList();

        VoiceButton.OnItemSelected += args =>
        {
            VoiceButton.SelectId(args.Id);
            var voiceId = VoiceButton.GetItemMetadata(args.Id) as string;
            if (voiceId != null)
                SetVoice(voiceId);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTTS();

        UpdateTTSVoicesControls();
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile == null || VoiceButton == null)
            return;

        VoiceButton.Clear();

        var firstVoiceChoiceId = -1;
        var selectedVoice = Profile.Voice;

        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];

            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            VoiceButton.AddItem(name, i);
            VoiceButton.SetItemMetadata(i, voice.ID);

            if (firstVoiceChoiceId == -1)
                firstVoiceChoiceId = i;

            // Sponsor check (commented out as in original)
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

        var voiceFound = false;
        for (var i = 0; i < VoiceButton.ItemCount; i++)
        {
            var voiceId = VoiceButton.GetItemMetadata(i) as string;
            if (voiceId == selectedVoice)
            {
                VoiceButton.SelectId(i);
                voiceFound = true;
                break;
            }
        }

        if (!voiceFound && firstVoiceChoiceId != -1)
        {
            VoiceButton.SelectId(firstVoiceChoiceId);
            var firstVoiceId = VoiceButton.GetItemMetadata(firstVoiceChoiceId) as string;
            if (firstVoiceId != null && string.IsNullOrEmpty(selectedVoice))
                SetVoice(firstVoiceId);
        }

        VoiceButton.ResetSearch();
    }

    private void PlayPreviewTTS()
    {
        if (Profile is null)
            return;

        _entManager.System<TTSSystem>().RequestPreviewTTS(Profile.Voice);
    }

    private void SetVoice(string newVoice)
    {
        Profile = Profile?.WithVoice(newVoice);
        IsDirty = true;
    }
}
