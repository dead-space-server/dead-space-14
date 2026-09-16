// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared.DeadSpace.AntagGearSelector;
using Content.Shared.Eui;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.AntagGearSelector;

public sealed class AntagGearSelectorEui(
    AntagGearSelectorSystem system,
    ICommonSession session,
    EntityUid target,
    EntityUid rule,
    AntagGearSelectorEuiState initialState) : BaseEui
{
    public override EuiStateBase GetNewState() => initialState;

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (IsShutDown || msg is not AntagGearSelectorSelectedMessage selected)
            return;

        if (system.TryApplySelection(session, target, rule, selected.GearIndex, selected.PerkIndex))
            Close();
    }
}
