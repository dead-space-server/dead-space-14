// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Atmos.Piping;
using Content.Shared.IdentityManagement;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Atmos.UI;

[UsedImplicitly]
public sealed class GasElectrolyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GasElectrolyzerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GasElectrolyzerWindow>();
        _window.Title = Identity.Name(Owner, EntMan);
        _window.ToggleEnabled += OnToggle;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not GasElectrolyzerBoundUserInterfaceState cast)
            return;

        _window.UpdateState(cast);
    }

    private void OnToggle()
    {
        SendMessage(new GasElectrolyzerToggleMessage());
    }
}
