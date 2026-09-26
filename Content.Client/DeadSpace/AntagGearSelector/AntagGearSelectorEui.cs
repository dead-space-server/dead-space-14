// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.AntagGearSelector;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.AntagGearSelector;

[UsedImplicitly]
public sealed class AntagGearSelectorEui : BaseEui
{
    private readonly AntagGearSelectorWindow _window = new();

    public AntagGearSelectorEui()
    {
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnConfirmed += (gear, perk) => SendMessage(new AntagGearSelectorSelectedMessage(gear, perk));
    }

    public override void Opened() => _window.OpenCentered();
    public override void Closed() => _window.Close();

    public override void HandleState(EuiStateBase state)
    {
        if (state is AntagGearSelectorEuiState selector)
            _window.UpdateState(selector);
    }
}
