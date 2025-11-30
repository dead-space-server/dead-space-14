// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Shared.DeadSpace.RadioSettings;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;

namespace Content.Server.DeadSpace.RadioSettings;

/// <summary>
/// This is the component that allows the radio handheld to switch between microphone and speaker modes.
/// </summary>
public sealed class RadioSettingsSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly RadioDeviceSystem _radio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RadioSettingsComponent, RadioSettingsMessage>(OnMessage);
        SubscribeLocalEvent<RadioSettingsComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternative);
        SubscribeLocalEvent<RadioSettingsComponent, InteractUsingEvent>(OnInteract);
    }

    private void OnInteract(Entity<RadioSettingsComponent> ent, ref InteractUsingEvent args)
    {
        UpdateUi(ent.Owner);
    }

    private void OnAlternative(Entity<RadioSettingsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null)
            return;

        var user = args.User;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                ent.Comp.Speaking.IsToggle = !ent.Comp.Speaking.IsToggle;
                _radio.ToggleRadioMicrophone(ent.Owner, user, true);

                var state = Loc.GetString(ent.Comp.Speaking.IsToggle ? "radio-settings-microphone-on" : "radio-settings-microphone-off");
                var message = Loc.GetString("radio-settings-state-microphone", ("microState", state));
                _popup.PopupEntity(message, user, user);

                UpdateUi(ent.Owner);
            },
            Text = Loc.GetString("radio-settings-toggle-microphone")
        };
        args.Verbs.Add(verb);
    }

    private void OnMessage(Entity<RadioSettingsComponent> ent, ref RadioSettingsMessage args)
    {
        if (args.Mode == RadioMode.Listening)
        {
            ent.Comp.Listening = args.IsToggle;
            _radio.ToggleRadioSpeaker(ent.Owner, args.Actor, true);
        }

        else
        {
            ent.Comp.Speaking = args.IsToggle;
            _radio.ToggleRadioMicrophone(ent.Owner, args.Actor, true);
        }

        UpdateUi(ent.Owner);
    }

    public void UpdateUi(EntityUid uid, RadioSettingsComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (!_userInterfaceSystem.HasUi(uid, RadioSettingsUiKey.Key))
            return;

        var state = new RadioSettingsState(comp.Listening, comp.Speaking);
        _userInterfaceSystem.SetUiState(uid, RadioSettingsUiKey.Key, state);
    }

}
