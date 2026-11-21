using System.Linq;
using System.Text;
using Content.Shared.DeadSpace.Terminal;

namespace Content.Server.DeadSpace.Terminal;

public sealed partial class TerminalSystem
{
    private static readonly string[] Builtins = { "cd", "pwd", "echo", "exit", "logout", "help", "history" };

    private static readonly HashSet<string> InteractiveOnly = new(StringComparer.Ordinal)
    {
        "ping", "nano", "clear", "passwd", "reboot",
    };

    private static string Fail(TerminalContext ctx, string message)
    {
        ctx.Failed = true;
        return message.EndsWith('\n') ? message : message + "\n";
    }

    private static string Line(string message)
    {
        return message.Length == 0 || message.EndsWith('\n') ? message : message + "\n";
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

    private static List<(string Command, bool RequiresSuccess)> SplitChain(string line)
    {
        var result = new List<(string, bool)>();
        var current = new StringBuilder();
        var inQuotes = false;
        var requiresSuccess = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '"')
                inQuotes = !inQuotes;

            if (!inQuotes && character == ';')
            {
                result.Add((current.ToString(), requiresSuccess));
                current.Clear();
                requiresSuccess = false;
                continue;
            }

            if (!inQuotes && character == '&' && i + 1 < line.Length && line[i + 1] == '&')
            {
                result.Add((current.ToString(), requiresSuccess));
                current.Clear();
                requiresSuccess = true;
                i++;
                continue;
            }

            current.Append(character);
        }

        result.Add((current.ToString(), requiresSuccess));
        return result;
    }

    private string ExecuteLine(TerminalContext ctx, string line)
    {
        var output = new StringBuilder();
        var previousFailed = false;

        foreach (var (command, requiresSuccess) in SplitChain(line))
        {
            if (requiresSuccess && previousFailed)
                continue;

            ctx.Failed = false;
            output.Append(ExecuteSimple(ctx, command));
            previousFailed = ctx.Failed;

            if (ctx.Close || ctx.Machine.Pending != null || ctx.EditorPath != null)
                break;
        }

        return output.ToString();
    }

    private string ExecuteSimple(TerminalContext ctx, string line)
    {
        if (!TryTokenizeCommand(line, out var tokens))
            return Fail(ctx, "tsh: syntax error: unterminated quoted string");

        if (tokens.Count == 0)
            return string.Empty;

        var args = new List<string>();
        string? target = null;
        var append = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token == ">" || token == ">>")
            {
                if (i + 1 >= tokens.Count)
                    return Fail(ctx, "tsh: syntax error near unexpected token `newline'");

                append = token == ">>";
                target = tokens[++i];
                continue;
            }

            if (token.StartsWith(">>", StringComparison.Ordinal) && token.Length > 2)
            {
                append = true;
                target = token[2..];
                continue;
            }

            if (token.StartsWith('>') && token.Length > 1)
            {
                append = false;
                target = token[1..];
                continue;
            }

            args.Add(token);
        }

        if (args.Count == 0)
            return Fail(ctx, "tsh: syntax error: missing command");

        var command = args[0];
        args.RemoveAt(0);

        if (target == null)
            return Dispatch(ctx, command, args);

        var interactive = ctx.Interactive;
        ctx.Interactive = false;
        var output = Dispatch(ctx, command, args);
        ctx.Interactive = interactive;

        if (ctx.Failed)
            return output;

        var error = WriteFile(ctx, ResolvePath(ctx, target), output, append);
        return error == null ? string.Empty : Fail(ctx, $"tsh: {error}");
    }

    private string Dispatch(TerminalContext ctx, string command, List<string> args)
    {
        if (++ctx.Steps > MaxRunSteps)
            return Fail(ctx, Loc.GetString("terminal-run-limit"));

        switch (command)
        {
            case "cd":
                return BuiltinCd(ctx, args);
            case "pwd":
                return $"{ctx.Comp.CurrentDir}\n";
            case "echo":
                return $"{string.Join(' ', args)}\n";
            case "exit":
            case "logout":
                return BuiltinExit(ctx);
            case "help":
                return BuiltinHelp(ctx);
            case "history":
                return BuiltinHistory(ctx);
        }

        if (!TryResolveCommand(ctx, command, out var path, out var node, out var error))
            return Fail(ctx, error);

        if (node.Content.StartsWith(BinaryPrefix, StringComparison.Ordinal))
            return RunBinary(ctx, FirstLine(node.Content[BinaryPrefix.Length..]), command, args);

        if (!CanRead(ctx, node))
            return Fail(ctx, $"tsh: {command}: Permission denied");

        return ExecuteScript(ctx, path, node.Content);
    }

    private static bool IsRunScript(string path)
    {
        return path.EndsWith(".run", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanLaunch(TerminalContext ctx, string path, TerminalNode node)
    {
        return CanExecute(ctx, node) || IsRunScript(path) && CanRead(ctx, node);
    }

    private bool TryResolveCommand(TerminalContext ctx, string command, out string path, out TerminalNode node, out string error)
    {
        path = string.Empty;
        node = default!;
        error = $"{command}: command not found";

        var candidates = command.Contains('/')
            ? new[] { ResolvePath(ctx, command) }
            : PathDirectories.Select(dir => $"{dir}/{command}").ToArray();

        foreach (var candidate in candidates)
        {
            if (!TryGetNode(ctx.Machine, candidate, out var found))
                continue;

            if (found.IsDirectory)
            {
                error = $"tsh: {command}: Is a directory";
                return false;
            }

            if (!CanTraverse(ctx, candidate) || !CanLaunch(ctx, candidate, found))
            {
                error = $"tsh: {command}: Permission denied";
                return false;
            }

            path = candidate;
            node = found;
            return true;
        }

        if (command.Contains('/'))
            error = $"tsh: {command}: No such file or directory";

        return false;
    }

    private string RunBinary(TerminalContext ctx, string binary, string command, List<string> args)
    {
        if (!ctx.Interactive && InteractiveOnly.Contains(binary))
            return Fail(ctx, Loc.GetString("terminal-run-command-unsupported", ("command", command)));

        return binary switch
        {
            "ls" => CmdLs(ctx, args),
            "cat" => CmdCat(ctx, args),
            "touch" => CmdTouch(ctx, args),
            "mkdir" => CmdMkdir(ctx, args),
            "rmdir" => CmdRmdir(ctx, args),
            "rm" => CmdRm(ctx, args),
            "cp" => CmdCopy(ctx, args, false),
            "mv" => CmdCopy(ctx, args, true),
            "chmod" => CmdChmod(ctx, args),
            "chown" => CmdChown(ctx, args),
            "uname" => CmdUname(ctx, args),
            "whoami" => $"{ctx.User}\n",
            "id" => CmdId(ctx, args),
            "groups" => CmdGroups(ctx, args),
            "clear" => CmdClear(ctx),
            "hostname" => CmdHostname(ctx, args),
            "su" => CmdSu(ctx, args),
            "passwd" => CmdPasswd(ctx, args),
            "useradd" => CmdUseradd(ctx, args),
            "userdel" => CmdUserdel(ctx, args),
            "usermod" => CmdUsermod(ctx, args),
            "sudo" => CmdSudo(ctx, args),
            "ifconfig" => CmdIfconfig(ctx, args),
            "ping" => CmdPing(ctx, args),
            "nano" => CmdNano(ctx, args),
            "run" => CmdRun(ctx, args),
            "apt" => CmdApt(ctx, args),
            "reboot" => CmdReboot(ctx),
            "neofetch" => CmdNeofetch(ctx),
            _ => Fail(ctx, $"tsh: {command}: cannot execute binary file: Exec format error"),
        };
    }

    private static (HashSet<char> Flags, List<string> Operands) ParseFlags(List<string> args)
    {
        var flags = new HashSet<char>();
        var operands = new List<string>();
        var endOfFlags = false;

        foreach (var arg in args)
        {
            if (!endOfFlags && arg == "--")
            {
                endOfFlags = true;
                continue;
            }

            if (!endOfFlags && arg.Length > 1 && arg[0] == '-' && arg[1] != '-')
            {
                foreach (var flag in arg[1..])
                    flags.Add(flag);
                continue;
            }

            operands.Add(arg);
        }

        return (flags, operands);
    }

    private string BuiltinCd(TerminalContext ctx, List<string> args)
    {
        if (args.Count > 1)
            return Fail(ctx, Loc.GetString("terminal-many-args"));

        var target = args.Count == 0 ? GetHome(ctx.Machine, ctx.User) : ResolvePath(ctx, args[0]);
        var display = args.Count == 0 ? target : args[0];

        if (!TryGetNode(ctx.Machine, target, out var node))
            return Fail(ctx, Loc.GetString("terminal-cd-no-such-dir", ("path", display)));

        if (!node.IsDirectory)
            return Fail(ctx, $"cd: {display}: Not a directory");

        if (!CanTraverse(ctx, target) || !CanExecute(ctx, node))
            return Fail(ctx, $"cd: {display}: Permission denied");

        ctx.Comp.CurrentDir = target;
        return string.Empty;
    }

    private string BuiltinExit(TerminalContext ctx)
    {
        if (!ctx.Interactive)
            return Fail(ctx, Loc.GetString("terminal-run-command-unsupported", ("command", "exit")));

        if (ctx.Machine.UserStack.TryPop(out var previous))
        {
            ctx.Comp.User = previous;
            ctx.User = previous;
            return "logout\n";
        }

        ctx.Close = true;
        return string.Empty;
    }

    private string BuiltinHelp(TerminalContext ctx)
    {
        var commands = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var dir in PathDirectories)
        {
            if (!TryGetNode(ctx.Machine, dir, out var dirNode) || !dirNode.IsDirectory)
                continue;

            foreach (var child in GetChildren(ctx.Machine, dir))
            {
                if (TryGetNode(ctx.Machine, child, out var node) && !node.IsDirectory && ((node.Mode & 73) != 0 || IsRunScript(child)))
                    commands.Add(GetName(child));
            }
        }

        var builder = new StringBuilder();
        builder.Append("Shell builtins:\n  ").Append(string.Join(' ', Builtins)).Append('\n');
        builder.Append("Commands in PATH:\n  ").Append(commands.Count == 0 ? "(none)" : string.Join(' ', commands)).Append('\n');
        builder.Append("Operators: cmd1 ; cmd2, cmd1 && cmd2, cmd > file, cmd >> file\n");
        builder.Append($"Documentation: https://tos-service.online/tempos/docs.html\n");
        return builder.ToString();
    }

    private string BuiltinHistory(TerminalContext ctx)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < ctx.Machine.History.Count; i++)
            builder.Append($"{i + 1,4}  {ctx.Machine.History[i]}\n");
        return builder.ToString();
    }

    private string CmdLs(TerminalContext ctx, List<string> args)
    {
        var (flags, operands) = ParseFlags(args);
        var showAll = flags.Contains('a');
        var longFormat = flags.Contains('l');

        if (operands.Count == 0)
            operands.Add(".");

        var builder = new StringBuilder();

        foreach (var operand in operands)
        {
            var path = ResolvePath(ctx, operand);

            if (!TryGetNode(ctx.Machine, path, out var node))
            {
                builder.Append(Fail(ctx, $"ls: cannot access '{operand}': No such file or directory"));
                continue;
            }

            if (!CanTraverse(ctx, path))
            {
                builder.Append(Fail(ctx, $"ls: cannot access '{operand}': Permission denied"));
                continue;
            }

            if (!node.IsDirectory)
            {
                builder.Append(FormatEntry(operand, node, longFormat));
                continue;
            }

            if (!CanRead(ctx, node))
            {
                builder.Append(Fail(ctx, $"ls: cannot open directory '{operand}': Permission denied"));
                continue;
            }

            if (operands.Count > 1)
                builder.Append($"{operand}:\n");

            var children = GetChildren(ctx.Machine, path)
                .Where(child => showAll || !GetName(child).StartsWith('.'))
                .OrderBy(child => child, StringComparer.Ordinal)
                .ToList();

            if (longFormat)
            {
                foreach (var child in children)
                    builder.Append(FormatEntry(GetName(child), ctx.Machine.Nodes[child], true));
            }
            else if (children.Count > 0)
            {
                builder.Append(string.Join("  ", children.Select(GetName))).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string FormatEntry(string name, TerminalNode node, bool longFormat)
    {
        if (!longFormat)
            return $"{name}\n";

        var size = node.IsDirectory ? 4096 : Encoding.UTF8.GetByteCount(node.Content);
        return $"{FormatMode(node)} {node.Owner,-8} {node.Group,-8} {size,6} {name}{(node.IsDirectory ? "/" : string.Empty)}\n";
    }

    private string CmdCat(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return Fail(ctx, Loc.GetString("terminal-cat-usage"));

        var builder = new StringBuilder();

        foreach (var arg in args)
        {
            var path = ResolvePath(ctx, arg);

            if (!TryGetNode(ctx.Machine, path, out var node))
            {
                builder.Append(Fail(ctx, $"cat: {arg}: No such file or directory"));
                continue;
            }

            if (node.IsDirectory)
            {
                builder.Append(Fail(ctx, $"cat: {arg}: Is a directory"));
                continue;
            }

            if (!CanTraverse(ctx, path) || !CanRead(ctx, node))
            {
                builder.Append(Fail(ctx, $"cat: {arg}: Permission denied"));
                continue;
            }

            builder.Append(Line(node.Content));
        }

        return builder.ToString();
    }

    private string CmdTouch(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return Fail(ctx, Loc.GetString("terminal-touch-usage"));

        var builder = new StringBuilder();

        foreach (var arg in args)
        {
            var path = ResolvePath(ctx, arg);
            if (ctx.Machine.Nodes.ContainsKey(path))
                continue;

            if (CheckCreate(ctx, path, "touch", arg) is { } error)
            {
                builder.Append(Fail(ctx, error));
                continue;
            }

            ctx.Machine.Nodes[path] = NewNode(ctx, false);
        }

        return builder.ToString();
    }

    private string CmdMkdir(TerminalContext ctx, List<string> args)
    {
        var (flags, operands) = ParseFlags(args);
        if (operands.Count == 0)
            return Fail(ctx, Loc.GetString("terminal-mkdir-usage"));

        var parents = flags.Contains('p');
        var builder = new StringBuilder();

        foreach (var operand in operands)
        {
            var path = ResolvePath(ctx, operand);

            if (parents)
            {
                var chain = new List<string>();
                for (var current = path; current != "/" && !ctx.Machine.Nodes.ContainsKey(current); current = GetParent(current))
                    chain.Add(current);

                chain.Reverse();

                foreach (var dir in chain)
                {
                    if (CheckCreate(ctx, dir, "mkdir", operand) is { } chainError)
                    {
                        builder.Append(Fail(ctx, chainError));
                        break;
                    }

                    ctx.Machine.Nodes[dir] = NewNode(ctx, true);
                }

                continue;
            }

            if (ctx.Machine.Nodes.ContainsKey(path))
            {
                builder.Append(Fail(ctx, Loc.GetString("terminal-dir-exists", ("args", operand))));
                continue;
            }

            if (CheckCreate(ctx, path, "mkdir", operand) is { } error)
            {
                builder.Append(Fail(ctx, error));
                continue;
            }

            ctx.Machine.Nodes[path] = NewNode(ctx, true);
        }

        return builder.ToString();
    }

    private string CmdRmdir(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return Fail(ctx, "rmdir: missing operand");

        var builder = new StringBuilder();

        foreach (var arg in args)
        {
            var path = ResolvePath(ctx, arg);

            if (!TryGetNode(ctx.Machine, path, out var node))
            {
                builder.Append(Fail(ctx, $"rmdir: failed to remove '{arg}': No such file or directory"));
                continue;
            }

            if (!node.IsDirectory)
            {
                builder.Append(Fail(ctx, $"rmdir: failed to remove '{arg}': Not a directory"));
                continue;
            }

            if (GetChildren(ctx.Machine, path).Any())
            {
                builder.Append(Fail(ctx, $"rmdir: failed to remove '{arg}': Directory not empty"));
                continue;
            }

            if (CheckRemove(ctx, path, "rmdir", arg) is { } error)
            {
                builder.Append(Fail(ctx, error));
                continue;
            }

            ctx.Machine.Nodes.Remove(path);
        }

        return builder.ToString();
    }

    private string CmdRm(TerminalContext ctx, List<string> args)
    {
        var (flags, operands) = ParseFlags(args);
        var recursive = flags.Contains('r') || flags.Contains('R');
        var force = flags.Contains('f');
        var noPreserveRoot = operands.Remove("--no-preserve-root");

        if (operands.Count == 0)
            return force ? string.Empty : Fail(ctx, "rm: missing operand");

        var builder = new StringBuilder();

        foreach (var operand in operands)
        {
            var path = ResolvePath(ctx, operand);

            if (!TryGetNode(ctx.Machine, path, out var node))
            {
                if (!force)
                    builder.Append(Fail(ctx, $"rm: cannot remove '{operand}': No such file or directory"));
                continue;
            }

            if (node.IsDirectory && !recursive)
            {
                builder.Append(Fail(ctx, $"rm: cannot remove '{operand}': Is a directory"));
                continue;
            }

            if (path == "/")
            {
                if (!noPreserveRoot)
                {
                    builder.Append(Fail(ctx, "rm: it is dangerous to operate recursively on '/'\nrm: use --no-preserve-root to override this failsafe"));
                    continue;
                }

                if (ctx.User != "root")
                {
                    builder.Append(Fail(ctx, "rm: cannot remove '/': Permission denied"));
                    continue;
                }

                ctx.Machine.Nodes.Clear();
                ctx.Machine.Nodes["/"] = new TerminalNode { IsDirectory = true, Mode = ModeDirectory };
                ctx.Comp.CurrentDir = "/";
                continue;
            }

            if (CheckRemove(ctx, path, "rm", operand) is { } error)
            {
                builder.Append(Fail(ctx, error));
                continue;
            }

            var subtree = ctx.Machine.Nodes.Keys.Where(key => IsInside(key, path)).ToList();
            var blocked = subtree.FirstOrDefault(key => key != path && ctx.Machine.Nodes[key].IsDirectory && !CanWrite(ctx, ctx.Machine.Nodes[key]));
            if (blocked != null)
            {
                builder.Append(Fail(ctx, $"rm: cannot remove '{blocked}': Permission denied"));
                continue;
            }

            foreach (var key in subtree)
                ctx.Machine.Nodes.Remove(key);

            if (IsInside(ctx.Comp.CurrentDir, path))
                ctx.Comp.CurrentDir = GetParent(path);
        }

        return builder.ToString();
    }

    private string CmdCopy(TerminalContext ctx, List<string> args, bool move)
    {
        var name = move ? "mv" : "cp";
        var (flags, operands) = ParseFlags(args);
        var recursive = move || flags.Contains('r') || flags.Contains('R');

        if (operands.Count < 2)
            return Fail(ctx, $"{name}: missing destination file operand");

        var destination = ResolvePath(ctx, operands[^1]);
        var destinationIsDir = TryGetNode(ctx.Machine, destination, out var destinationNode) && destinationNode.IsDirectory;

        if (operands.Count > 2 && !destinationIsDir)
            return Fail(ctx, $"{name}: target '{operands[^1]}' is not a directory");

        var builder = new StringBuilder();

        foreach (var operand in operands.Take(operands.Count - 1))
        {
            var source = ResolvePath(ctx, operand);

            if (!TryGetNode(ctx.Machine, source, out var sourceNode))
            {
                builder.Append(Fail(ctx, $"{name}: cannot stat '{operand}': No such file or directory"));
                continue;
            }

            if (sourceNode.IsDirectory && !recursive)
            {
                builder.Append(Fail(ctx, $"{name}: -r not specified; omitting directory '{operand}'"));
                continue;
            }

            if (!CanTraverse(ctx, source) || !CanRead(ctx, sourceNode))
            {
                builder.Append(Fail(ctx, $"{name}: cannot open '{operand}' for reading: Permission denied"));
                continue;
            }

            var target = destinationIsDir ? NormalizePath(destination, GetName(source)) : destination;

            if (target == source)
            {
                builder.Append(Fail(ctx, $"{name}: '{operand}' and '{operands[^1]}' are the same file"));
                continue;
            }

            if (sourceNode.IsDirectory && IsInside(target, source))
            {
                builder.Append(Fail(ctx, $"{name}: cannot copy a directory, '{operand}', into itself"));
                continue;
            }

            if (move && CheckRemove(ctx, source, name, operand) is { } removeError)
            {
                builder.Append(Fail(ctx, removeError));
                continue;
            }

            if (TryGetNode(ctx.Machine, target, out var existing))
            {
                if (existing.IsDirectory || sourceNode.IsDirectory)
                {
                    builder.Append(Fail(ctx, $"{name}: cannot overwrite '{target}'"));
                    continue;
                }

                if (!CanWrite(ctx, existing))
                {
                    builder.Append(Fail(ctx, $"{name}: cannot create regular file '{target}': Permission denied"));
                    continue;
                }
            }
            else if (CheckCreate(ctx, target, name, target) is { } createError)
            {
                builder.Append(Fail(ctx, createError));
                continue;
            }

            var subtree = ctx.Machine.Nodes.Where(pair => IsInside(pair.Key, source)).ToList();

            if (!move && ctx.Machine.Nodes.Count + subtree.Count > MaxNodes)
            {
                builder.Append(Fail(ctx, $"{name}: cannot create '{target}': No space left on device"));
                continue;
            }

            foreach (var (key, node) in subtree)
            {
                var newPath = target + key[source.Length..];
                var copy = node.Clone();

                if (!move)
                {
                    copy.Owner = ctx.User;
                    copy.Group = GetPrimaryGroup(ctx.Machine, ctx.User);
                }

                if (move)
                    ctx.Machine.Nodes.Remove(key);

                ctx.Machine.Nodes[newPath] = copy;
            }

            if (move && IsInside(ctx.Comp.CurrentDir, source))
                ctx.Comp.CurrentDir = target + ctx.Comp.CurrentDir[source.Length..];
        }

        return builder.ToString();
    }

    private static bool TryParseMode(string text, int current, out int mode)
    {
        mode = current;

        if (text.Length is 3 or 4 && text.All(c => c is >= '0' and <= '7'))
        {
            mode = Convert.ToInt32(text, 8) & ModeOpen;
            return true;
        }

        foreach (var clause in text.Split(','))
        {
            var operatorIndex = clause.IndexOfAny(new[] { '+', '-', '=' });
            if (operatorIndex < 0)
                return false;

            var who = clause[..operatorIndex];
            var op = clause[operatorIndex];
            var perms = clause[(operatorIndex + 1)..];

            if (who.Any(c => "ugoa".IndexOf(c) < 0) || perms.Any(c => "rwx".IndexOf(c) < 0))
                return false;

            if (who.Length == 0)
                who = "a";

            var bits = 0;
            foreach (var perm in perms)
                bits |= perm switch { 'r' => 4, 'w' => 2, _ => 1 };

            var mask = 0;
            var value = 0;
            foreach (var target in who)
            {
                var shifts = target switch
                {
                    'u' => new[] { 6 },
                    'g' => new[] { 3 },
                    'o' => new[] { 0 },
                    _ => new[] { 6, 3, 0 },
                };

                foreach (var shift in shifts)
                {
                    mask |= 7 << shift;
                    value |= bits << shift;
                }
            }

            mode = op switch
            {
                '+' => mode | value,
                '-' => mode & ~value,
                _ => (mode & ~mask) | value,
            };
        }

        return true;
    }

    private string CmdChmod(TerminalContext ctx, List<string> args)
    {
        var recursive = args.Remove("-R");
        if (args.Count < 2)
            return Fail(ctx, "chmod: missing operand\nusage: chmod [-R] MODE FILE...");

        var builder = new StringBuilder();

        foreach (var operand in args.Skip(1))
        {
            var path = ResolvePath(ctx, operand);

            if (!TryGetNode(ctx.Machine, path, out var node))
            {
                builder.Append(Fail(ctx, $"chmod: cannot access '{operand}': No such file or directory"));
                continue;
            }

            if (ctx.User != "root" && node.Owner != ctx.User)
            {
                builder.Append(Fail(ctx, $"chmod: changing permissions of '{operand}': Operation not permitted"));
                continue;
            }

            var targets = recursive ? ctx.Machine.Nodes.Where(pair => IsInside(pair.Key, path)).Select(pair => pair.Value).ToList() : new List<TerminalNode> { node };

            foreach (var target in targets)
            {
                if (ctx.User != "root" && target.Owner != ctx.User)
                    continue;

                if (!TryParseMode(args[0], target.Mode, out var mode))
                    return Fail(ctx, $"chmod: invalid mode: '{args[0]}'");

                target.Mode = mode;
            }
        }

        return builder.ToString();
    }

    private string CmdChown(TerminalContext ctx, List<string> args)
    {
        var recursive = args.Remove("-R");
        if (args.Count < 2)
            return Fail(ctx, "chown: missing operand\nusage: chown [-R] OWNER[:GROUP] FILE...");

        if (ctx.User != "root")
            return Fail(ctx, "chown: Operation not permitted");

        var spec = args[0].Split(':');
        var owner = spec[0];
        var group = spec.Length > 1 ? spec[1] : null;

        if (owner.Length > 0 && FindUser(ctx.Machine, owner) == null)
            return Fail(ctx, $"chown: invalid user: '{args[0]}'");

        if (!string.IsNullOrEmpty(group) && GetGroups(ctx.Machine).All(found => found.Name != group))
            return Fail(ctx, $"chown: invalid group: '{args[0]}'");

        var builder = new StringBuilder();

        foreach (var operand in args.Skip(1))
        {
            var path = ResolvePath(ctx, operand);

            if (!ctx.Machine.Nodes.ContainsKey(path))
            {
                builder.Append(Fail(ctx, $"chown: cannot access '{operand}': No such file or directory"));
                continue;
            }

            foreach (var (key, node) in ctx.Machine.Nodes)
            {
                if (key != path && (!recursive || !IsInside(key, path)))
                    continue;

                if (owner.Length > 0)
                    node.Owner = owner;
                if (!string.IsNullOrEmpty(group))
                    node.Group = group;
            }
        }

        return builder.ToString();
    }

    private string CmdNano(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return Fail(ctx, Loc.GetString("terminal-nano-usage"));
        if (args.Count > 1)
            return Fail(ctx, Loc.GetString("terminal-many-args"));

        var path = ResolvePath(ctx, args[0]);
        var parent = GetParent(path);

        if (!TryGetNode(ctx.Machine, parent, out var parentNode) || !parentNode.IsDirectory)
            return Fail(ctx, Loc.GetString("terminal-parent-dir-not-exist", ("args", args[0])));

        var content = string.Empty;
        if (TryGetNode(ctx.Machine, path, out var node))
        {
            if (node.IsDirectory)
                return Fail(ctx, Loc.GetString("terminal-is-directory", ("args", args[0])));

            if (!CanTraverse(ctx, path) || !CanRead(ctx, node))
                return Fail(ctx, $"nano: {args[0]}: Permission denied");

            content = node.Content;
        }

        ctx.Machine.EditorPath = path;
        ctx.Machine.EditorUser = ctx.User;
        ctx.EditorPath = path;
        ctx.EditorContent = content;
        return string.Empty;
    }
}
