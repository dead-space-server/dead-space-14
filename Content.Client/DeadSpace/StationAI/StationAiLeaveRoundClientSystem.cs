// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.StationAI.UI;
using Content.Shared.DeadSpace.StationAI;
using Content.Shared.Silicons.StationAi;
using Robust.Client.Player;

namespace Content.Client.DeadSpace.StationAI;

public sealed partial class StationAiLeaveRoundClientSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    private StationAiLeaveRoundConfirmationWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiHeldComponent, StationAiLeaveRoundEvent>(OnLeaveRoundAction);
    }

    private void OnLeaveRoundAction(Entity<StationAiHeldComponent> ent, ref StationAiLeaveRoundEvent args)
    {
        if (args.Handled || _player.LocalEntity != ent.Owner)
            return;

        args.Handled = true;
        OpenConfirmationWindow();
    }

    private void OpenConfirmationWindow()
    {
        _window?.Close();

        _window = new StationAiLeaveRoundConfirmationWindow();
        _window.Confirmed += OnConfirmed;
        _window.OnClose += OnWindowClosed;
        _window.OpenCentered();
    }

    private void OnConfirmed()
    {
        RaiseNetworkEvent(new StationAiLeaveRoundConfirmedEvent());
        _window?.Close();
    }

    private void OnWindowClosed()
    {
        if (_window == null)
            return;

        _window.Confirmed -= OnConfirmed;
        _window.OnClose -= OnWindowClosed;
        _window = null;
    }
}
