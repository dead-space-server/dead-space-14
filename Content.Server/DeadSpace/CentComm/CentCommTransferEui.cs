// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.CentComm;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.CentComm;

public sealed class CentCommTransferEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    private readonly CentCommTransferSystem _transfer;
    private string? _error;

    public CentCommTransferEui()
    {
        IoCManager.InjectDependencies(this);
        _transfer = _systems.GetEntitySystem<CentCommTransferSystem>();
    }

    public override void Opened()
    {
        _admin.OnPermsChanged += OnPermissionsChanged;
        _transfer.StateChanged += StateDirty;
        StateDirty();
    }

    public override void Closed()
    {
        _admin.OnPermsChanged -= OnPermissionsChanged;
        _transfer.StateChanged -= StateDirty;
    }

    private void OnPermissionsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admin.HasAdminFlag(Player, AdminFlags.Fun))
            Close();
    }

    public override EuiStateBase GetNewState()
    {
        var canStart = _transfer.CanStart(out var status) && _admin.HasAdminFlag(Player, AdminFlags.Fun);
        return new CentCommTransferState(_transfer.GetParallaxes(), _transfer.GetWeather(), canStart,
            canStart ? _error ?? status : status);
    }

    public override void HandleMessage(EuiMessageBase message)
    {
        base.HandleMessage(message);
        if (message is not CentCommTransferRequest request || IsShutDown)
            return;

        if (!_admin.HasAdminFlag(Player, AdminFlags.Fun))
        {
            Close();
            return;
        }

        _error = _transfer.TryStart(Player, request, out var result) ? null : result;
        StateDirty();
    }
}
