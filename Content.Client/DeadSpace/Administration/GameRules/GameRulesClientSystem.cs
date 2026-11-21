// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Administration.UI.GameRules;
using Content.Shared.DeadSpace.Administration.GameRules;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Shared.Console;
using Content.Shared.Administration;
using Content.Client.Administration.Managers;

namespace Content.Client.DeadSpace.Administration.GameRules;

public sealed partial class GameRulesClientSystem : EntitySystem
{
    [Dependency] private IClientConsoleHost _consoleHost = default!;
    [Dependency] private IClientAdminManager _adminManager = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    public event Action<GameRulesListResponseMessage>? RulesUpdated;

    private static bool _commandRegistered;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GameRulesListResponseMessage>(OnRulesResponse);

        if (!_commandRegistered)
        {
            _consoleHost.RegisterCommand(
                "gamerulesui",
                "Открыть окно управления геймрулами",
                "gamerulesui",
                ExecuteOpenGameRulesWindow,
                false);
            _commandRegistered = true;
        }
    }

    private void ExecuteOpenGameRulesWindow(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_adminManager.HasFlag(AdminFlags.Admin))
            return;

        var window = new GameRulesWindow();
        _uiManager.WindowRoot.AddChild(window);
        window.OpenCentered();
    }

    private void OnRulesResponse(GameRulesListResponseMessage msg)
    {
        RulesUpdated?.Invoke(msg);
    }

    public void RequestRules()
    {
        if (!_adminManager.HasFlag(AdminFlags.Admin))
            return;

        RaiseNetworkEvent(new RequestGameRulesListMessage());
    }

    public void AddRule(string ruleId)
    {
        RaiseNetworkEvent(new AddGameRuleRequestMessage(ruleId));
    }
}
