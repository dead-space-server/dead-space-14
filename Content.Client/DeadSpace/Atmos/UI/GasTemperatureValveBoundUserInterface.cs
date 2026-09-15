// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Atmos.Piping;
using Content.Shared.IdentityManagement;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Atmos.UI;

[UsedImplicitly]
public sealed class GasTemperatureValveBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GasTemperatureValveWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GasTemperatureValveWindow>();
        _window.ThresholdChanged += OnThresholdChanged;
        _window.ModeToggled += OnModeToggled;
        Update();
    }

    public override void Update()
    {
        if (_window == null)
            return;

        _window.Title = Identity.Name(Owner, EntMan);

        if (!EntMan.TryGetComponent(Owner, out GasTemperatureValveComponent? valve))
            return;

        _window.SetState(valve.Threshold, valve.PassWhenBelow, GasTemperatureValveComponent.MaxThreshold);
    }

    private void OnThresholdChanged(float value)
    {
        SendPredictedMessage(new GasTemperatureValveChangeThresholdMessage(value));
    }

    private void OnModeToggled(bool passWhenBelow)
    {
        SendPredictedMessage(new GasTemperatureValveToggleModeMessage(passWhenBelow));
    }
}
