using Content.Shared.DeadSpace.TTS;
using Content.Shared.Corvax.TTS;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.VoiceMask;

namespace Content.Server.VoiceMask;

public partial class VoiceMaskSystem
{
    private void InitializeTTS()
    {
        SubscribeLocalEvent<VoiceMaskComponent, InventoryRelayedEvent<TransformSpeakerVoiceEvent>>(OnSpeakerVoiceTransform);
        SubscribeLocalEvent<VoiceMaskComponent, ImplantRelayEvent<TransformSpeakerVoiceEvent>>(OnSpeakerVoiceTransformImplant); // DS14
        SubscribeLocalEvent<VoiceMaskComponent, TransformSpeakerVoiceEvent>(OnSpeakerVoiceTransformInnate); // DS14
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskChangeVoiceMessage>(OnChangeVoice);
    }

    private void OnSpeakerVoiceTransform(Entity<VoiceMaskComponent> entity, ref InventoryRelayedEvent<TransformSpeakerVoiceEvent> args)
    {
        // DS14-start
        if (!entity.Comp.Active)
            return;
        // DS14-end

        args.Args.VoiceId = GetTtsVoice(entity.Comp); // DS14
    }

    // DS14-start
    private void OnSpeakerVoiceTransformImplant(Entity<VoiceMaskComponent> entity, ref ImplantRelayEvent<TransformSpeakerVoiceEvent> args)
    {
        if (!entity.Comp.Active)
            return;

        args.Event.VoiceId = GetTtsVoice(entity.Comp);
    }

    private void OnSpeakerVoiceTransformInnate(Entity<VoiceMaskComponent> entity, ref TransformSpeakerVoiceEvent args)
    {
        if (!entity.Comp.Active)
            return;

        args.VoiceId = GetTtsVoice(entity.Comp);
    }

    private string GetTtsVoice(VoiceMaskComponent component)
    {
        var provider = _cfgManager.GetCVar(TTSProviderCVars.Provider);
        var id = component.Voices.GetValueOrDefault(provider) ?? component.VoiceId;
        return TTSVoices.Resolve(_proto, provider, id)?.ID ?? id;
    }
    // DS14-end

    private void OnChangeVoice(Entity<VoiceMaskComponent> entity, ref VoiceMaskChangeVoiceMessage msg)
    {
        // DS14-start
        var provider = _cfgManager.GetCVar(TTSProviderCVars.Provider);
        var invalidVoice = msg.Voice is not { } id || !_proto.TryIndex<TTSVoicePrototype>(id, out var voice) ||
            voice.Provider != provider || !voice.RoundStart;
        // DS14-end
        if (invalidVoice) // DS14
            return;

        entity.Comp.Voices[provider] = msg.Voice; // DS14

        _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-success"), entity);

        UpdateUI(entity);
    }
}
