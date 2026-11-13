// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.ViewVariables;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.AdminCommandLogger;

public sealed partial class AdminCommandLoggerSystem : EntitySystem
{
    private const int MaxLogValueLength = 256;

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IConsoleHost _consoleHost = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IResourceManager _resource = default!;
    [Dependency] private ToolshedManager _toolshed = default!;

    private readonly AdminCommandPermissions _engineCommandPermissions = new();

    public override void Initialize()
    {
        LoadEngineCommandPermissions();

        _consoleHost.AnyCommandExecuted += OnCommandExecuted;
        SubscribeLocalEvent<ViewVariablesModifyRemoteEvent>(OnViewVariablesModify);
    }

    public override void Shutdown()
    {
        _consoleHost.AnyCommandExecuted -= OnCommandExecuted;
    }

    private void LoadEngineCommandPermissions()
    {
        if (!_resource.TryContentFileRead(new ResPath("/engineCommandPerms.yml"), out var stream))
            return;

        using (stream)
        {
            _engineCommandPermissions.LoadPermissionsFromStream(stream);
        }
    }

    private void OnCommandExecuted(IConsoleShell shell, string name, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
            return;

        var adminOnly = false;

        if (_toolshed.DefaultEnvironment.TryGetCommand(name, out var tsCmd))
        {
            var sub = tsCmd.HasSubCommands && args.Length > 0 ? args[0] : null;
            var commandSpec = new CommandSpec(tsCmd, sub);

            adminOnly = _adminManager.TryGetCommandFlags(commandSpec, out var flags) && flags != null;
        }
        else if (_consoleHost.AvailableCommands.TryGetValue(name, out var consoleCommand))
        {
            adminOnly = Attribute.IsDefined(consoleCommand.GetType(), typeof(AdminCommandAttribute)) ||
                        _engineCommandPermissions.AdminCommands.ContainsKey(name);
        }

        if (!adminOnly)
            return;

        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.High,
            $"Administrator {player:player} executed command <{name}> with args: [{string.Join(", ", args)}]");
    }

    private void OnViewVariablesModify(ViewVariablesModifyRemoteEvent ev)
    {
        var target = FormatTarget(ev.Target, ev.TargetType);
        var property = ev.PropertyPath ?? "<unknown>";
        var oldValue = ev.HasOldValue ? FormatValue(ev.OldValue) : "<unknown>";
        var newValue = FormatValue(ev.NewValue);
        var reinterpret = ev.ReinterpretedValue ? " (reinterpreted)" : string.Empty;

        if (_player.TryGetSessionById(ev.PlayerUser, out var player))
        {
            _adminLog.Add(
                LogType.AdminCommands,
                LogImpact.High,
                $"Administrator {player:player} modified VV {target} property <{property}> from [{oldValue}] to [{newValue}]{reinterpret}");
            return;
        }

        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.High,
            $"Administrator {ev.PlayerUser} modified VV {target} property <{property}> from [{oldValue}] to [{newValue}]{reinterpret}");
    }

    private string FormatTarget(object target, Type targetType)
    {
        return target switch
        {
            EntityUid uid => $"{ToPrettyString(uid)} ({targetType.FullName})",
            Component component => $"{targetType.Name} on {ToPrettyString(component.Owner)}",
            _ => $"{FormatValue(target)} ({targetType.FullName})"
        };
    }

    private static string FormatValue(object? value)
    {
        string? text;

        try
        {
            text = value == null ? "null" : PrettyPrint.PrintUserFacing(value);
        }
        catch
        {
            text = value?.ToString() ?? "null";
        }

        text ??= "null";
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= MaxLogValueLength ? text : text[..MaxLogValueLength] + "...";
    }
}
