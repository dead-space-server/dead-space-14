using Content.Shared.DeadSpace.Radio.Systems;
using Content.Shared.DeadSpace.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Radio.Components;

namespace Content.Server.DeadSpace.Radio.Systems;

public sealed partial class RadioToggleActionSystem : SharedRadioToggleActionSystem
{
    [Dependency] private SharedRadioDeviceSystem _radioDevice = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioToggleActionComponent, RadioToggleEvent>(OnRadioToggle);
    }

    private void OnRadioToggle(Entity<RadioToggleActionComponent> ent, ref RadioToggleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RadioMicrophoneComponent>(ent.Owner, out var microphone))
            return;

        if (!TryComp<RadioSpeakerComponent>(ent.Owner, out var speaker))
            return;

        args.Handled = true;
        ent.Comp.Enabled = !(microphone.Enabled && speaker.Enabled);

        _radioDevice.SetMicrophoneEnabled(ent, args.Performer, ent.Comp.Enabled, quiet: true);
        _radioDevice.SetSpeakerEnabled(ent, args.Performer, ent.Comp.Enabled);
        Dirty(ent);
    }
}