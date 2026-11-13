// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Administration.UI.GameRules;
using Content.Shared.DeadSpace.Administration.GameRules;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.DeadSpace.Administration.GameRules;

public sealed partial class GameRulesClientSystem : EntitySystem
{
    [Dependency] private IClientConsoleHost _consoleHost = default!;
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
        RaiseNetworkEvent(new RequestGameRulesListMessage());
    }

    public void AddRule(string ruleId)
    {
        RaiseNetworkEvent(new AddGameRuleRequestMessage(ruleId));
    }
}
