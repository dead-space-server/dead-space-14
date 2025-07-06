// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking.Rules;
using Robust.Shared.Player;
using Content.Server.DeadSpace.AutoBan.Components;
using Robust.Server.Player;
using Robust.Server.Console;

namespace Content.Server.DeadSpace.AutoBan;

public sealed class AutoBanP8RuleSystem : GameRuleSystem<AutoBanP8RuleComponent>
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerConsoleHost _host = default!;

    public override void Initialize()
    {
        base.Initialize();

    }

    protected override void Started(EntityUid uid, AutoBanP8RuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
    }

    /// <summary>
    ///     Метод для регистрации нарушения игроком.
    /// </summary>
    public void RegisterViolation(EntityUid uid, string playerUsername, AutoBanP8RuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.RuleViolations.ContainsKey(playerUsername))
        {
            component.RuleViolations[playerUsername]++;
        }
        else
        {
            component.RuleViolations[playerUsername] = 1;
        }
    }

    /// <summary>
    ///     Проверка, сколько раз игрок нарушил правило.
    /// </summary>
    public int GetViolationCount(EntityUid uid, string playerUsername, AutoBanP8RuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0;

        return component.RuleViolations.TryGetValue(playerUsername, out var count) ? count : 0;
    }

    /// <summary>
    ///     Нужно ли банить игрока?
    /// </summary>
    public bool NeedToBan(EntityUid uid, string playerUsername, AutoBanP8RuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return GetViolationCount(uid, playerUsername, component) > component.MaxWarnings;
    }

    /// <summary>
    ///     Банит игрока, если он превысил лимит нарушений.
    /// </summary>
    public void Punish(EntityUid uid, ICommonSession playerSession, AutoBanP8RuleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_playerManager.TryGetPlayerData(playerSession.UserId, out var data))
            return;

        var playerName = data.UserName;

        var cmd = $"ban \"{playerName}\" \"{component.Reason}\" {component.Minutes}";
        _host.ExecuteCommand(null, cmd);
    }

}
