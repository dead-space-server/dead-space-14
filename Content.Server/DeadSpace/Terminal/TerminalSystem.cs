using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Administration;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Mind;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.DeadSpace.Terminal;
using Robust.Server.GameObjects;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Toolshed.Commands.GameTiming;
using Content.Server.DeadSpace.Terminal;

namespace Content.Server.Terminal;

public sealed class TerminalSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;

    private readonly Dictionary<EntityUid, List<EntityUid>> _agentList = new();

    private sealed class PendingTransfer
    {
        public EntityUid Sender;
        public EntityUid Receiver;
        public float RemainingSeconds;
        public Action Deliver = default!;
    }

    private sealed class RunInstruction
    {
        public string Kind;
        public string Argument;
        public string Variable;
        public int Line;
        public List<RunInstruction> Body = new();
        public List<RunInstruction> ElseBody = new();
        public RunInstruction(string kind, string arg, string var, int line)
        {
            Kind = kind;
            Argument = arg;
            Variable = var;
            Line = line;
        }
    }

    private const int MaxRunNesting = 8;
    private const int MaxRunIterations = 100;
    private const int MaxRunSteps = 1000;
    private const int MaxRunOutputLength = 16_384;
    private static readonly Regex RunVariableRegex = new(@"%([A-Za-z_][A-Za-z0-9_]*)%", RegexOptions.Compiled);
    private static readonly Regex RunVariableNameRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex RunConditionRegex = new(@"^(.+?)\s*(==|!=)\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex RunForRegex = new(@"^for\s+([A-Za-z_][A-Za-z0-9_]*)\s+in\s*\((.*)\)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly List<PendingTransfer> _transfers = new();
    private readonly Dictionary<EntityUid, Dictionary<string, string>> _files = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TerminalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerminalComponent, TerminalCommandMessage>(OnCommand);
        SubscribeLocalEvent<TerminalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerminalComponent, TerminalSaveFileMessage>(OnSaveFile);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<AgentRequestActionEvent>(OnAgentRequest);
        SubscribeLocalEvent<AgentReceiveActionEvent>(OnAgentReceive);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _transfers.Count - 1; i >= 0; i--)
        {
            var transfer = _transfers[i];
            transfer.RemainingSeconds -= frameTime;

            if (transfer.RemainingSeconds > 0)
                continue;

            _transfers.RemoveAt(i);
            transfer.Deliver();
        }
    }

    private readonly HashSet<string> _ips = new();
    private readonly Dictionary<EntityUid, HashSet<string>> _directories = new();
    private const float LanSpeedBPS = 100_000f;
    private const float BaseLatencySeconds = 0.05f;
    private const float SignalSpeedTilesPerSecond = 20f;

    private void OnShutdown(EntityUid uid, TerminalComponent comp, ComponentShutdown args)
    {
        _ips.Remove(comp.IpAdress);
        _directories.Remove(uid);
        _files.Remove(uid);
        _agentList.Remove(uid);
    }

    private bool IsTraitor(MindComponent mind)
    {
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (HasComp<TraitorRoleComponent>(role))
                return true;
        }
        return false;
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (args.Mind.OwnedEntity is not { } agent || !IsTraitor(args.Mind))
            return;

        var comp = EnsureComp<TaipanAgentCargoComponent>(agent);
        comp.Buffer ??= _container.EnsureContainer<Container>(agent, TaipanAgentCargoComponent.BufferContainerId);
        _action.AddAction(agent, ref comp.RequestActionEntity, comp.RequestActionPrototype);
    }

    private void OnAgentRequest(AgentRequestActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.Performer, out var actor) || !TryComp<TaipanAgentCargoComponent>(args.Performer, out var agentComp) || agentComp.HasShipment || !string.IsNullOrWhiteSpace(agentComp.Request))
            return;
        var agent = args.Performer;
        args.Handled = true;

        _quickDialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("agent-request-title"), Loc.GetString("agent-request-prompt"), request =>
        {
            if (Deleted(agent) || !TryComp<TaipanAgentCargoComponent>(agent, out var agentComp) || agentComp.HasShipment || !string.IsNullOrWhiteSpace(agentComp.Request))
                return;

            request = request.Trim();
            if (request.Length > 0)
                agentComp.Request = request;
        });
    }

    private void OnAgentReceive(AgentReceiveActionEvent args)
    {
        if (args.Handled || !TryComp<TaipanAgentCargoComponent>(args.Performer, out var agentComp) || !agentComp.HasShipment || agentComp.Buffer == null)
            return;
        args.Handled = true;
        foreach (var item in agentComp.Buffer.ContainedEntities.ToArray())
        {
            if (!_container.Remove(item, agentComp.Buffer))
                continue;
            _hands.PickupOrDrop(args.Performer, item, checkActionBlocker: false, animate: false, dropNear: true);
        }
        agentComp.HasShipment = false;
        agentComp.Request = null;
        _action.RemoveAction(args.Performer, agentComp.ReceiveActionEntity);
        agentComp.ReceiveActionEntity = null;
    }

    private List<EntityUid> GetTraitors()
    {
        var agents = new List<EntityUid>();
        var query = EntityQueryEnumerator<TaipanAgentCargoComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (_mobState.IsAlive(uid, mobState))
                agents.Add(uid);
        }

        return agents.OrderBy(agent => Name(agent), StringComparer.OrdinalIgnoreCase).ThenBy(agent => agent.ToString(), StringComparer.Ordinal).ToList();
    }

    private float GetDistance(EntityUid sender, EntityUid receiver)
    {
        var senderCoords = _transform.GetMapCoordinates(sender);
        var receiverCoords = _transform.GetMapCoordinates(receiver);
        if (senderCoords.MapId != receiverCoords.MapId)
            return float.PositiveInfinity;
        return (senderCoords.Position - receiverCoords.Position).Length();
    }

    private bool TryFindByIp(string address, out EntityUid terminal)
    {
        var query = EntityQueryEnumerator<TerminalComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IpAdress == address)
            {
                terminal = uid;
                return true;
            }
        }

        terminal = default!;
        return false;
    }

    private float GetTransferDelay(EntityUid sender, EntityUid receiver, int sizeBytes)
    {
        var distance = GetDistance(sender, receiver);
        if (float.IsPositiveInfinity(distance))
            return float.PositiveInfinity;
        return BaseLatencySeconds + distance / SignalSpeedTilesPerSecond + sizeBytes / LanSpeedBPS;
    }

    private void SendWithDelay(EntityUid sender, EntityUid receiver, int sizeBytes, Action deliver)
    {
        var delay = GetTransferDelay(sender, receiver, sizeBytes);

        if (float.IsPositiveInfinity(delay))
            return;
        _transfers.Add(new PendingTransfer
        {
            Sender = sender,
            Receiver = receiver,
            RemainingSeconds = delay,
            Deliver = deliver
        });
    }

    private string GetIp(TerminalComponent comp)
    {
        return comp.IpAdress;
    }
    private void GenerateIp(TerminalComponent comp)
    {
        do
        {
            comp.IpFirst = Random.Shared.Next(0, 256);
            comp.IpSecond = Random.Shared.Next(0, 256);
            comp.IpThird = Random.Shared.Next(0, 256);
        }
        while (!_ips.Add(GetIp(comp)));
    }
    private void GenerateDirs(EntityUid uid, TerminalComponent comp)
    {
        comp.CurrentDir = "/";
        _directories[uid] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/",
            "/home",
            "/tmp",
            "/etc",
            "/bin"
        };

        _files[uid] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void OnMapInit(EntityUid uid, TerminalComponent comp, MapInitEvent args)
    {
        GenerateIp(comp);
        GenerateDirs(uid, comp);
        comp.UserIndex = Random.Shared.Next(1000, 10000);
        Dirty(uid, comp);
    }

    private static bool TryTokenizeCommand(string input, out List<string> parts)
    {
        parts = new List<string>();
        var token = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;

        foreach (var character in input)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }
            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (hasToken)
                {
                    parts.Add(token.ToString());
                    token.Clear();
                    hasToken = false;
                }
                continue;
            }
            token.Append(character);
            hasToken = true;
        }

        if (inQuotes)
            return false;
        if (hasToken)
            parts.Add(token.ToString());
        return true;
    }

    private static string ExpandRunVariables(string text, Dictionary<string, string> vars)
    {
        return RunVariableRegex.Replace(text, match => vars.TryGetValue(match.Groups[1].Value, out var value) ? value : string.Empty);
    }

    private static string UnquoteRunValue(string value)
    {
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }

    private static List<string> SplitRunValues(string text)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;

        foreach (var character in text)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                value.Append(character);
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(UnquoteRunValue(value.ToString().Trim()));
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }
        if (value.Length > 0)
            values.Add(UnquoteRunValue(value.ToString().Trim()));
        return values;
    }

    private static void RestoreRunVariable(Dictionary<string, string> vars, string name, bool hadPreviousValue, string? previousValue)
    {
        if (hadPreviousValue)
            vars[name] = previousValue!;
        else
            vars.Remove(name);
    }

    private string ExecuteRunCommand(EntityUid uid, TerminalComponent comp, RunInstruction instruction, Dictionary<string, string> variables)
    {
        var line = ExpandRunVariables(instruction.Argument, variables);

        if (line.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            var assignment = line[4..].Trim();
            var equals = assignment.IndexOf('=');

            if (equals <= 0)
            {
                return $"{Loc.GetString("terminal-run-syntax", ("line", instruction.Line), ("reason", "Expected set NAME=value"))}\n";
            }
            var name = assignment[..equals].Trim();
            if (!RunVariableNameRegex.IsMatch(name))
            {
                return $"{Loc.GetString("terminal-run-syntax", ("line", instruction.Line), ("reason", "Invalid variable name"))}\n";
            }

            variables[name] = UnquoteRunValue(assignment[(equals + 1)..].Trim());
            return string.Empty;
        }
        if (line.Equals("rem", StringComparison.OrdinalIgnoreCase) || line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("::", StringComparison.Ordinal))
            return string.Empty;

        if (!TryTokenizeCommand(line, out var parts))
        {
            return $"{Loc.GetString("terminal-run-syntax", ("line", instruction.Line), ("reason", "Unclosed quotation mark"))}\n";
        }
        if (parts.Count == 0)
            return string.Empty;
        var command = parts[0];
        var args = parts.Skip(1).ToArray();
        if (command.Equals("echo", StringComparison.OrdinalIgnoreCase))
            return $"{string.Join(' ', args)}\n";
        if (command.Equals("ping", StringComparison.OrdinalIgnoreCase) || command.Equals("nano", StringComparison.OrdinalIgnoreCase) || command.Equals("agents", StringComparison.OrdinalIgnoreCase) || command.Equals("orders", StringComparison.OrdinalIgnoreCase) || command.Equals("send", StringComparison.OrdinalIgnoreCase) || command.Equals("run", StringComparison.OrdinalIgnoreCase) || command.Equals("clear", StringComparison.OrdinalIgnoreCase))
            return $"{Loc.GetString("terminal-run-command-unsupported", ("command", command))}\n";
        var result = command.ToLowerInvariant() switch
        {
            "help" when args.Length == 0 => "help\nls\nclear\nwhoami\nhostname\nifconfig\npwd\nmkdir [dir]\ntouch [file]\ncat [file]\n",
            "whoami" when args.Length == 0 => "User\n",
            "ifconfig" when args.Length == 0 => $"{GetIp(comp)}\n",
            "hostname" when args.Length == 0 => $"TEMPUser{comp.UserIndex}\n",
            "pwd" when args.Length == 0 => $"{comp.CurrentDir}\n",
            "ls" when args.Length == 0 => HandleLS(uid, comp),
            "cd" => HandleCD(uid, comp, args),
            "mkdir" => HandleMKDir(uid, comp, args),
            "touch" => HandleTouch(uid, comp, args),
            "cat" => HandleCat(uid, comp, args),
            _ => "command not found\n"
        };
        return result.Length > 0 && !result.EndsWith('\n') ? $"{result}\n" : result;
    }

    private bool ExecuteRunBlock(EntityUid uid, TerminalComponent comp, List<RunInstruction> instructions, Dictionary<string, string> vars, StringBuilder output, ref int steps)
    {
        foreach (var instruction in instructions)
        {
            if (++steps > MaxRunSteps)
            {
                output.Append(Loc.GetString("terminal-run-limit")).Append('\n');
                return false;
            }
            if (instruction.Kind == "if")
            {
                var condition = ExpandRunVariables(instruction.Argument, vars);
                var match = RunConditionRegex.Match(condition);

                if (!match.Success)
                {
                    output.Append(Loc.GetString("terminal-run-syntax", ("line", instruction.Line), ("reason", "Expected a comparison using == or !="))).Append('\n');
                    continue;
                }
                var left = UnquoteRunValue(match.Groups[1].Value.Trim());
                var right = UnquoteRunValue(match.Groups[3].Value.Trim());
                var equal = left.Equals(right, StringComparison.OrdinalIgnoreCase);
                var conditionIsTrue = match.Groups[2].Value == "==" ? equal : !equal;
                var branch = conditionIsTrue ? instruction.Body : instruction.ElseBody;

                if (!ExecuteRunBlock(uid, comp, branch, vars, output, ref steps))
                    return false;
                continue;
            }
            if (instruction.Kind == "for")
            {
                var values = SplitRunValues(ExpandRunVariables(instruction.Argument, vars));

                if (values.Count > MaxRunIterations)
                {
                    output.Append(Loc.GetString("terminal-run-limit")).Append('\n');
                    return false;
                }
                var hadPreviousValue = vars.TryGetValue(instruction.Variable, out var previousValue);

                foreach (var value in values)
                {
                    if (++steps > MaxRunSteps)
                    {
                        output.Append(Loc.GetString("terminal-run-limit")).Append('\n');
                        RestoreRunVariable(vars, instruction.Variable, hadPreviousValue, previousValue);
                        return false;
                    }
                    vars[instruction.Variable] = value;

                    if (!ExecuteRunBlock(uid, comp, instruction.Body, vars, output, ref steps))
                    {
                        RestoreRunVariable(vars, instruction.Variable, hadPreviousValue, previousValue);
                        return false;
                    }
                }
                RestoreRunVariable(vars, instruction.Variable, hadPreviousValue, previousValue);
                continue;
            }
            var result = ExecuteRunCommand(uid, comp, instruction, vars);

            if (output.Length + result.Length > MaxRunOutputLength)
            {
                output.Append(Loc.GetString("terminal-run-limit")).Append('\n');
                return false;
            }
            output.Append(result);
        }
        return true;
    }

    private bool TryParseRunBlock(string[] lines, ref int index, int depth, out List<RunInstruction> instructions, out string? terminator, out string? error)
    {
        instructions = new List<RunInstruction>();
        terminator = null;
        error = null;
        if (depth > MaxRunNesting)
        {
            error = "Maximum block nesting exceeded";
            return false;
        }

        while (index < lines.Length)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();

            if (line.Length == 0 || line.Equals("rem", StringComparison.OrdinalIgnoreCase) || line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("::", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            if (line.Equals("else", StringComparison.OrdinalIgnoreCase) || line.Equals("endif", StringComparison.OrdinalIgnoreCase) || line.Equals("endfor", StringComparison.OrdinalIgnoreCase))
            {
                terminator = line.ToLowerInvariant();
                return true;
            }
            if (line.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
            {
                var instruction = new RunInstruction("if", line[3..].Trim(), string.Empty, lineNumber);
                index++;
                if (!TryParseRunBlock(lines, ref index, depth + 1, out instruction.Body, out var bodyTerminator, out error))
                    return false;
                if (bodyTerminator == "else")
                {
                    index++;
                    if (!TryParseRunBlock(lines, ref index, depth + 1, out instruction.ElseBody, out bodyTerminator, out error))
                        return false;
                }
                if (bodyTerminator != "endif")
                {
                    error = "Expected endif";
                    return false;
                }

                index++;
                instructions.Add(instruction);
                continue;
            }
            if (line.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
            {
                var match = RunForRegex.Match(line);
                if (!match.Success)
                {
                    error = "Expected: for NAME in (value1,value2)";
                    return false;
                }
                var instruction = new RunInstruction("for", match.Groups[2].Value, match.Groups[1].Value, lineNumber);
                index++;
                if (!TryParseRunBlock(lines, ref index, depth + 1, out instruction.Body, out var bodyTerminator, out error))
                    return false;
                if (bodyTerminator != "endfor")
                {
                    error = "Expected endfor";
                    return false;
                }

                index++;
                instructions.Add(instruction);
                continue;
            }
            instructions.Add(new RunInstruction("command", line, string.Empty, lineNumber));
            index++;
        }
        return true;
    }

    private string HandleRun(EntityUid uid, TerminalComponent comp, string[] args)
    {
        if (args.Length != 1)
            return $"{Loc.GetString("terminal-run-usage")}\n";
        if (!args[0].EndsWith(".run", StringComparison.OrdinalIgnoreCase))
            return $"{Loc.GetString("terminal-run-extension")}\n";
        if (!_directories.TryGetValue(uid, out var dirs) || !_files.TryGetValue(uid, out var files))
            return $"{Loc.GetString("terminal-fs-unaviable")}\n";

        var path = NormalizePath(comp.CurrentDir, args[0]);

        if (dirs.Contains(path) || !files.TryGetValue(path, out var content))
            return $"{Loc.GetString("terminal-run-not-found", ("path", args[0]))}\n";
        var lines = content.Replace("\r\n", "\n").Replace('\n', '\n').Split('\n');
        var index = 0;
        if (!TryParseRunBlock(lines, ref index, 0, out var instructions, out var terminator, out var error) || terminator != null)
        {
            var reason = error ?? "Unexpected block terminator";
            return $"{Loc.GetString("terminal-run-syntax", ("line", index + 1), ("reason", reason))}\n";
        }
        var output = new StringBuilder();
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var steps = 0;
        ExecuteRunBlock(uid, comp, instructions, vars, output, ref steps);
        return output.ToString();
    }

    private string HandleLS(EntityUid uid, TerminalComponent comp)
    {
        if (!_directories.TryGetValue(uid, out var dirs) || !_files.TryGetValue(uid, out var files))
            return $"{Loc.GetString("terminal-fs-unaviable")}";
        var currentDir = comp.CurrentDir;
        var prefix = currentDir == "/" ? "/" : $"{currentDir}/";

        var directories = dirs.Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path != currentDir).Select(path => path[prefix.Length..]).Where(path => !path.Contains('/'));
        var fileNames = files.Keys.Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(path => path[prefix.Length..]).Where(path => !path.Contains('/'));

        var entries = directories.Concat(fileNames).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path).ToArray();

        return entries.Length == 0 ? "\n" : $"{string.Join('\n', entries)}\n";
    }

    private string HandleCat(EntityUid uid, TerminalComponent comp, string[] args)
    {
        if (args.Length == 0)
            return $"{Loc.GetString("terminal-cat-usage")}\n";
        if (args.Length > 1)
            return $"{Loc.GetString("terminal-many-args")}\n";
        if (!_directories.TryGetValue(uid, out var dirs) || !_files.TryGetValue(uid, out var files))
            return $"{Loc.GetString("terminal-fs-unaviable")}\n";

        var filePath = NormalizePath(comp.CurrentDir, args[0]);

        if (dirs.Contains(filePath))
            return $"{Loc.GetString("terminal-is-directory", ("args", args[0]))}";
        if (!files.TryGetValue(filePath, out var content))
            return $"{Loc.GetString("terminal-no-such-file")}";
        if (string.IsNullOrEmpty(content))
            return "\n";
        return content.EndsWith('\n') ? content : $"{content}\n";
    }

    private static string NormalizePath(string currentDir, string path)
    {
        var combinedPath = path.StartsWith('/') ? path : $"{currentDir}/{path}";
        var parts = combinedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = new List<string>();

        foreach (var part in parts)
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                if (normalized.Count > 0)
                    normalized.RemoveAt(normalized.Count - 1);
                continue;
            }
            normalized.Add(part);
        }
        return normalized.Count == 0 ? "/" : $"/{string.Join('/', normalized)}";
    }

    private void OnSaveFile(EntityUid uid, TerminalComponent comp, TerminalSaveFileMessage msg)
    {
        if (!_directories.TryGetValue(uid, out var dirs) || !_files.TryGetValue(uid, out var files))
            return;

        var filePath = NormalizePath(comp.CurrentDir, msg.Path);
        var separator = filePath.LastIndexOf('/');
        var parentDir = separator <= 0 ? "/" : filePath[..separator];

        if (!dirs.Contains(parentDir))
            return;
        files[filePath] = msg.Content;

        _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-nano-file-saved", ("filePath", filePath))}\n"));
    }

    private string HandleCD(EntityUid uid, TerminalComponent comp, string[] args)
    {
        if (args.Length == 0)
            return $"{Loc.GetString("terminal-cd-usage")}\n";
        if (args.Length > 1)
            return $"{Loc.GetString("terminal-many-args")}\n";
        if (!_directories.TryGetValue(uid, out var dirs))
            return $"{Loc.GetString("terminal-fs-unaviable")}\n";
        var targetDir = NormalizePath(comp.CurrentDir, args[0]);

        if (!dirs.Contains(targetDir))
            return $"{Loc.GetString("terminal-cd-no-such-dir", ("path", args[0]))}\n";

        comp.CurrentDir = targetDir;
        Dirty(uid, comp);
        return string.Empty;
    }

    private string HandleTouch(EntityUid uid, TerminalComponent comp, string[] args)
    {
        if (args.Length == 0)
            return $"{Loc.GetString("terminal-touch-usage")}\n";
        if (args.Length > 1)
            return $"{Loc.GetString("terminal-many-args")}\n";
        if (!_directories.TryGetValue(uid, out var dirs) || !_files.TryGetValue(uid, out var files))
            return $"{Loc.GetString("terminal-fs-unaviable")}\n";
        var filepath = NormalizePath(comp.CurrentDir, args[0]);
        if (dirs.Contains(filepath))
            return $"{Loc.GetString("terminal-is-directory", ("args", args[0]))}";
        var separator = filepath.LastIndexOf('/');
        var parentDir = separator <= 0 ? "/" : filepath[..separator];

        if (!dirs.Contains(parentDir))
            return $"{Loc.GetString("terminal-parent-dir-not-exist", ("args", args[0]))}";
        if (!files.ContainsKey(filepath))
            files[filepath] = string.Empty;
        return string.Empty;
    }

    private string HandleMKDir(EntityUid uid, TerminalComponent comp, string[] args)
    {
        if (args.Length == 0)
            return $"{Loc.GetString("terminal-mkdir-usage")}\n";
        if (args.Length > 1)
            return $"{Loc.GetString("terminal-many-args")}\n";
        if (!_directories.TryGetValue(uid, out var dirs))
            return $"{Loc.GetString("terminal-fs-unaviable")}\n";
        var newDir = NormalizePath(comp.CurrentDir, args[0]);
        if (dirs.Contains(newDir))
            return $"{Loc.GetString("terminal-dir-exists", ("args", args[0]))}\n";
        var parentDir = newDir[..newDir.LastIndexOf('/')];
        if (string.IsNullOrEmpty(parentDir))
            parentDir = "/";
        if (!dirs.Contains(parentDir))
            return $"{Loc.GetString("terminal-parent-doesnt-exist", ("args", args[0]))}\n";
        dirs.Add(newDir);
        return string.Empty;
    }

    private void HandleNano(EntityUid uid, TerminalComponent comp, string[] args)
    {
        if (args.Length == 0)
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-nano-usage")}\n"));
            return;
        }
        if (args.Length > 1)
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-many-args")}\n"));
            return;
        }
        if (!_directories.TryGetValue(uid, out var dirs) || !_files.TryGetValue(uid, out var files))
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-fs-unaviable")}\n"));
            return;
        }
        var filePath = NormalizePath(comp.CurrentDir, args[0]);
        var separator = filePath.LastIndexOf('/');
        var parentDir = separator <= 0 ? "/" : filePath[..separator];

        if (!dirs.Contains(parentDir))
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-parent-dir-not-exist")}\n"));
            return;
        }

        files.TryGetValue(filePath, out var content);

        _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(string.Empty, filePath, content ?? string.Empty));
    }

    private void HandlePing(EntityUid sender, string[] args)
    {
        if (args.Length == 0)
        {
            _ui.SetUiState(sender, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-ping-usage")}\n"));
            return;
        }
        if (args.Length > 1)
        {
            _ui.SetUiState(sender, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-many-args")}\n"));
            return;
        }
        var address = args[0];
        EntityUid receiver;
        if (address.Equals("localhost", StringComparison.OrdinalIgnoreCase) || address == "127.0.0.1")
            receiver = sender;
        if (!TryFindByIp(address, out receiver))
        {
            _ui.SetUiState(sender, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-ping-host-not-found", ("ip", address))}\n"));
            return;
        }
        if (!TryComp<TerminalComponent>(sender, out var senderComp) || !TryComp<TerminalComponent>(receiver, out var receiverComp))
        {
            _ui.SetUiState(sender, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-ping-network-error")}\n"));
            return;
        }
        if (!senderComp.NetworkEnabled || !receiverComp.NetworkEnabled)
        {
            _ui.SetUiState(sender, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-ping-host-unreachable")}\n"));
            return;
        }
        var oneWayDelay = GetTransferDelay(sender, receiver, 64);

        if (float.IsPositiveInfinity(oneWayDelay))
        {
            _ui.SetUiState(sender, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"{Loc.GetString("terminal-ping-host-unreachable")}\n"));
            return;
        }
        var roundTripDelay = oneWayDelay * 2f;
        var milliseconds = Math.Max(1, (int)Math.Round(roundTripDelay * 1000f));

        SendWithDelay(sender, receiver, 64, () =>
        {
            if (!TryComp<TerminalComponent>(receiver, out var currentReceiver) || !currentReceiver.NetworkEnabled)
                return;
            SendWithDelay(receiver, sender, 64, () =>
            {
                if (!TryComp<TerminalComponent>(sender, out var currentSender) || !currentSender.NetworkEnabled)
                    return;
                _ui.SetUiState(sender, TerminalUiKey.Key, new TerminalBoundUserInterfaceState($"64 bytes from {address}: time={milliseconds} ms\n"));
            });
        });
    }

    private string HandleAgents(EntityUid terminal, string[] args)
    {
        if (args.Length != 0)
            return $"{Loc.GetString("agent-cargo-agents-usage")}\n";
        var agents = GetTraitors();
        _agentList[terminal] = agents;
        var output = $"{Loc.GetString("agent-cargo-agents-header")}\n";
        if (agents.Count == 0)
            return output + $"{Loc.GetString("agent-cargo-no-agents")}\n";
        for (var i = 0; i < agents.Count; i++)
            output += $"[{i + 1:00}]. {Name(agents[i])}\n";
        return output;
    }

    private string HandleOrders(EntityUid terminal, string[] args)
    {
        if (args.Length != 0)
            return $"{Loc.GetString("agent-cargo-orders-usage")}\n";
        if (!_agentList.TryGetValue(terminal, out var agents))
        {
            agents = GetTraitors();
            _agentList[terminal] = agents;
        }
        var output = $"{Loc.GetString("agent-cargo-orders-header")}\n";
        var found = false;

        for (var i = 0; i < agents.Count; i++)
        {
            if (!TryComp<TaipanAgentCargoComponent>(agents[i], out var agentComp) || string.IsNullOrWhiteSpace(agentComp.Request))
                continue;
            found = true;
            var status = agentComp.HasShipment ? Loc.GetString("agent-cargo-order-sent") : string.Empty;
            output += $"[{i + 1:00}] {Name(agents[i])} - " + $"{Loc.GetString("agent-cargo-order-requested")}: {agentComp.Request} {status}\n";
        }
        if (!found)
            output += $"{Loc.GetString("agent-cargo-no-orders")}\n";
        return output;
    }

    private string HandleSend(EntityUid terminal, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var agentNumber) || agentNumber < 1)
            return $"{Loc.GetString("agent-cargo-send-usage")}\n";
        if (!_agentList.TryGetValue(terminal, out var agents))
        {
            agents = GetTraitors();
            _agentList[terminal] = agents;
        }
        if (agentNumber > agents.Count)
            return $"{Loc.GetString("agent-cargo-agent-not-found")}\n";
        var agent = agents[agentNumber - 1];
        if (!TryComp<TaipanAgentCargoComponent>(agent, out var agentComp) || !TryComp<MobStateComponent>(agent, out var mobState) || !_mobState.IsAlive(agent, mobState))
            return $"{Loc.GetString("agent-cargo-agent-unavailable")}\n";
        if (agentComp.HasShipment)
            return $"{Loc.GetString("agent-cargo-shipment-pending")}\n";
        if (!TryComp(terminal, out TransformComponent? terminalComponent) || terminalComponent.GridUid is not { } gridUid)
            return $"{Loc.GetString("agent-cargo-pallet-not-found")}\n";
        agentComp.Buffer ??= _container.EnsureContainer<Container>(agent, TaipanAgentCargoComponent.BufferContainerId);

        var candidates = new HashSet<EntityUid>();
        var query = AllEntityQuery<CargoPalletComponent, TransformComponent>();

        while (query.MoveNext(out var palletUid, out var pallet, out var palletTransform))
        {
            if (palletTransform.ParentUid != gridUid || !palletTransform.Anchored || (pallet.PalletType & BuySellType.Sell) == 0)
                continue;
            _lookup.GetEntitiesIntersecting(palletUid, candidates, LookupFlags.Dynamic | LookupFlags.Sundries);
        }
        var sent = 0;
        foreach (var item in candidates)
        {
            if (!TryComp<ItemComponent>(item, out _) || HasComp<CargoSellBlacklistComponent>(item) || HasComp<MobStateComponent>(item) || !TryComp(item, out TransformComponent? itemTransform) || itemTransform.Anchored || _pricing.GetPrice(item) == 0)
                continue;
            if (_container.Insert(item, agentComp.Buffer))
                sent++;
        }
        if (sent == 0)
            return $"{Loc.GetString("agent-cargo-no-pallet-items")}\n";
        agentComp.HasShipment = true;
        _action.AddAction(agent, ref agentComp.ReceiveActionEntity, agentComp.ReceiveActionPrototype);
        return $"{Loc.GetString("agent-cargo-shipment-sent", ("agent", Name(agent)))}\n";
    }

    private void OnCommand(EntityUid uid, TerminalComponent component, TerminalCommandMessage message)
    {
        if (!TryTokenizeCommand(message.PromptText, out var parts))
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState("Unclosed quotation mark\n"));
            return;
        }
        if (parts.Count == 0)
            return;
        var command = parts[0];
        var arguments = parts.Skip(1).Where(x => !x.StartsWith('-')).ToArray();
        if (command.Equals("run", StringComparison.OrdinalIgnoreCase))
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(HandleRun(uid, component, arguments)));
            return;
        }
        if (command.Equals("ping", StringComparison.OrdinalIgnoreCase))
        {
            HandlePing(uid, arguments);
            return;
        }
        if (command.Equals("nano", StringComparison.OrdinalIgnoreCase))
        {
            HandleNano(uid, component, arguments);
            return;
        }
        if (command.Equals("agents", StringComparison.OrdinalIgnoreCase))
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(HandleAgents(uid, arguments)));
            return;
        }
        if (command.Equals("orders", StringComparison.OrdinalIgnoreCase))
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(HandleOrders(uid, arguments)));
            return;
        }
        if (command.Equals("send", StringComparison.OrdinalIgnoreCase))
        {
            _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(HandleSend(uid, arguments)));
            return;
        }

        var output = command switch
        {
            "help" => $"help\nls\nclear\nwhoami\nhostname\nifconfig\nping (IP адрес)\ncd (путь до директории)\npwd\nls\nmkdir (Название создаваемой директории)\ntouch (Название создаваемого файла)\ncat (название файла)\nnano (название файла)\nagents\norders\nsend (номер агента)\n",
            "clear" => "\x01CLEAR",
            "whoami" => $"User\n",
            "ifconfig" => $"{GetIp(component)}\n",
            "hostname" => $"TEMPUser{component.UserIndex}\n",
            "pwd" => $"{component.CurrentDir}\n",
            "ls" => HandleLS(uid, component),
            "cd" => HandleCD(uid, component, arguments),
            "mkdir" => HandleMKDir(uid, component, arguments),
            "touch" => HandleTouch(uid, component, arguments),
            "cat" => HandleCat(uid, component, arguments),
            _ => "command not found\n"
        };

        _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(output));
    }
}
