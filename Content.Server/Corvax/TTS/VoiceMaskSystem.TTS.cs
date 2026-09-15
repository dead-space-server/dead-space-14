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

        args.Args.VoiceId = entity.Comp.VoiceId;
    }

    // DS14-start
    private void OnSpeakerVoiceTransformImplant(Entity<VoiceMaskComponent> entity, ref ImplantRelayEvent<TransformSpeakerVoiceEvent> args)
    {
        if (!entity.Comp.Active)
            return;

        args.Event.VoiceId = entity.Comp.VoiceId;
    }

    private void OnSpeakerVoiceTransformInnate(Entity<VoiceMaskComponent> entity, ref TransformSpeakerVoiceEvent args)
    {
        if (!entity.Comp.Active)
            return;

        args.VoiceId = entity.Comp.VoiceId;
    }
    // DS14-end

    private void OnChangeVoice(Entity<VoiceMaskComponent> entity, ref VoiceMaskChangeVoiceMessage msg)
    {
        if (msg.Voice is { } id && !_proto.HasIndex<TTSVoicePrototype>(id))
            return;

        entity.Comp.VoiceId = msg.Voice;

        _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-success"), entity);

        UpdateUI(entity);
    }
}
