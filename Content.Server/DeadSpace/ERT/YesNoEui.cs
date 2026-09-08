// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.ERT.Components;
using Content.Server.EUI;
using Content.Server.Popups;
using Content.Shared.DeadSpace.UI;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.ERT;

public sealed class YesNoEui : BaseEui
{
    private readonly Entity<ResponseErtImplantComponent> _implant;
    private readonly EntityUid _target;
    private readonly ResponseErtImplantSystem _system;

    public YesNoEui(Entity<ResponseErtImplantComponent> implant, EntityUid target, ResponseErtImplantSystem system)
    {
        _implant = implant;
        _target = target;
        _system = system;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override void Closed()
    {
        _system.ConfirmationClosed(_implant, this);
    }

    public override EuiStateBase GetNewState()
    {
        return new YesNoEuiState(Loc.GetString(_implant.Comp.ConfirmationTitle), Loc.GetString(_implant.Comp.ConfirmationPrompt));
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is not YesNoChoiceMessage choice)
        {
            base.HandleMessage(msg);
            return;
        }

        Close();
        if (choice.Button != YesNoUiButton.Yes)
            return;

        _system.TryCallHelp(_implant, _target, Player, out var reason);
        var entMan = IoCManager.Resolve<IEntityManager>();
        if (Player.AttachedEntity is { } player)
            entMan.System<PopupSystem>().PopupEntity(reason, player, player);
    }
}
