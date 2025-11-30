// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.RadioSettings;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.RadioSettings;

[UsedImplicitly]

public sealed class RadioSettingsBoundUserInterface : BoundUserInterface
{
    private RadioSettingsWindow? _window;

    public RadioSettingsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<RadioSettingsWindow>();
        _window.OnRadioSettingsButton += (mode, isToggle) => SendMessage(new RadioSettingsMessage(mode, new RadioSettingsButton(isToggle)));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RadioSettingsState cast)
            return;

        _window?.UpdateState(cast);
    }
}
