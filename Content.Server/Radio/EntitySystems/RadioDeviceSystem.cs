using Content.Shared.Radio.EntitySystems;
// DS14-start
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Radio.Components;
using Content.Shared.Speech.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
// DS14-end

namespace Content.Server.Radio.EntitySystems;

/// <inheritdoc/>
public sealed partial class RadioDeviceSystem : SharedRadioDeviceSystem
{
    // DS14-start
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioMicrophoneComponent, GetVerbsEvent<Verb>>(OnGetVerb);
    }

    //Не совсем правильная реализация, но быстро и не нужно много переделывать.
    private void OnGetVerb(EntityUid uid, RadioMicrophoneComponent component, GetVerbsEvent<Verb> args)
    {
        if (HasComp<ActorComponent>(uid) ||
                !args.CanAccess ||
                !args.CanInteract ||
                !args.CanComplexInteract)
            return;

        if (!component.ToggleOnInteract)
            return;

        if (!component.Enabled)
            return;

        if (component.PowerRequired && !_power.IsPowered(uid))
            return;

        var on = HasComp<ActiveListenerComponent>(uid);
        args.Verbs.Add(new Verb
        {
            Priority = 1,
            Disabled = on,
            Category = VerbCategory.Microphone,
            Text = Loc.GetString("handheld-radio-component-mic-on-set"),
            Impact = LogImpact.Low,
            DoContactInteraction = false,
            CloseMenu = true,
            Act = () =>
            {
                var state = Loc.GetString(!on ? "handheld-radio-component-mic-on-state" : "handheld-radio-component-mic-off-state");
                var message = Loc.GetString("handheld-radio-component-mic-change", ("microphonState", state));
                _popup.PopupEntity(message, args.User, args.User);
                EnsureComp<ActiveListenerComponent>(uid).Range = component.ListenRange;
            },
        });

        args.Verbs.Add(new Verb
        {
            Priority = 0,
            Disabled = !on,
            Category = VerbCategory.Microphone,
            Text = Loc.GetString("handheld-radio-component-mic-off-set"),
            Impact = LogImpact.Low,
            DoContactInteraction = false,
            CloseMenu = true,
            Act = () =>
            {
                var state = Loc.GetString(!on ? "handheld-radio-component-mic-on-state" : "handheld-radio-component-mic-off-state");
                var message = Loc.GetString("handheld-radio-component-mic-change", ("microphonState", state));
                _popup.PopupEntity(message, args.User, args.User);
                RemCompDeferred<ActiveListenerComponent>(uid);
            }
        });
    }
    // DS14-end
}
