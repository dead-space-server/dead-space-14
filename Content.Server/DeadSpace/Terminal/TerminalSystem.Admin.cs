using System.Linq;
using System.Text;
using Content.Shared.DeadSpace.Terminal;

namespace Content.Server.DeadSpace.Terminal;

public sealed partial class TerminalSystem
{
    private string CmdUname(TerminalContext ctx, List<string> args)
    {
        var values = TryGetNode(ctx.Machine, "/etc/os-release", out var node) ? ParseKeyValues(node.Content) : new Dictionary<string, string>();
        var system = values.GetValueOrDefault("NAME", "Linux");
        var release = values.GetValueOrDefault("VERSION", "unknown");
        var (flags, _) = ParseFlags(args);

        if (flags.Contains('a'))
            return $"{system} {ctx.Comp.Hostname} {release} x86_64 GNU/Linux\n";

        var parts = new List<string>();
        if (flags.Contains('s') || flags.Count == 0)
            parts.Add(system);
        if (flags.Contains('n'))
            parts.Add(ctx.Comp.Hostname);
        if (flags.Contains('r'))
            parts.Add(release);
        if (flags.Contains('m'))
            parts.Add("x86_64");

        return $"{string.Join(' ', parts)}\n";
    }

    private string CmdId(TerminalContext ctx, List<string> args)
    {
        var name = args.Count > 0 ? args[0] : ctx.User;
        var user = FindUser(ctx.Machine, name);
        if (user == null)
            return Fail(ctx, $"id: '{name}': no such user");

        var groups = GetUserGroups(ctx.Machine, name);
        var primary = groups.FirstOrDefault(group => group.Gid == user.Gid)?.Name ?? user.Gid.ToString();
        var list = string.Join(',', groups.Select(group => $"{group.Gid}({group.Name})"));
        return $"uid={user.Uid}({user.Name}) gid={user.Gid}({primary}) groups={list}\n";
    }

    private string CmdGroups(TerminalContext ctx, List<string> args)
    {
        var name = args.Count > 0 ? args[0] : ctx.User;
        if (FindUser(ctx.Machine, name) == null)
            return Fail(ctx, $"groups: '{name}': no such user");

        return $"{string.Join(' ', GetUserGroups(ctx.Machine, name).Select(group => group.Name))}\n";
    }

    private string CmdClear(TerminalContext ctx)
    {
        ctx.Clear = true;
        return string.Empty;
    }

    private string CmdHostname(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return $"{ctx.Comp.Hostname}\n";

        if (ctx.User != "root")
            return Fail(ctx, "hostname: you must be root to change the host name");

        var error = WriteFile(ctx, "/etc/hostname", $"{args[0]}\n", false);
        return error == null ? string.Empty : Fail(ctx, $"hostname: {error}");
    }

    private void SwitchUser(TerminalContext ctx, string target, bool login)
    {
        ctx.Machine.UserStack.Push(ctx.Comp.User);
        ctx.Comp.User = target;
        ctx.User = target;

        if (!login)
            return;

        var home = GetHome(ctx.Machine, target);
        ctx.Comp.CurrentDir = TryGetNode(ctx.Machine, home, out var node) && node.IsDirectory ? home : "/";
    }

    private string CmdSu(TerminalContext ctx, List<string> args)
    {
        var login = args.Remove("-") | args.Remove("-l") | args.Remove("--login");
        var target = args.Count > 0 ? args[0] : "root";

        if (FindUser(ctx.Machine, target) == null)
            return Fail(ctx, $"su: user {target} does not exist");

        if (ctx.User == "root")
        {
            SwitchUser(ctx, target, login);
            return string.Empty;
        }

        return RequireRootPassword(ctx, "su", "Password: ", "su: Authentication failure", next =>
        {
            SwitchUser(next, target, login);
            return string.Empty;
        });
    }

    private string CmdSudo(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return Fail(ctx, "usage: sudo command [args...]");

        var command = args[0];
        var commandArgs = args.Skip(1).ToList();

        if (ctx.User == "root")
            return Dispatch(ctx, command, commandArgs);

        return RequireRootPassword(ctx, "sudo", "[sudo] password for root: ", "Sorry, try again.\nsudo: 1 incorrect password attempt", next => RunAsRoot(next, command, commandArgs));
    }

    private string RequireRootPassword(TerminalContext ctx, string command, string prompt, string failure, Func<TerminalContext, string> onSuccess)
    {
        if (IsLocked(GetPasswordHash(ctx.Machine, "root")))
            return Fail(ctx, $"{command}: Authentication failure");

        if (!ctx.Interactive)
            return Fail(ctx, $"{command}: a terminal is required to read the password");

        ctx.Machine.Pending = new TerminalPendingInput
        {
            Prompt = prompt,
            Handler = (next, input) =>
            {
                if (!CheckPassword(GetPasswordHash(next.Machine, "root"), input))
                    return Fail(next, failure);

                return onSuccess(next);
            },
        };

        return string.Empty;
    }

    private string RunAsRoot(TerminalContext ctx, string command, List<string> args)
    {
        ctx.User = "root";
        var output = Dispatch(ctx, command, args);
        ctx.User = ctx.Comp.User;
        return output;
    }

    private string CmdPasswd(TerminalContext ctx, List<string> args)
    {
        var target = args.Count > 0 ? args[0] : ctx.User;

        if (FindUser(ctx.Machine, target) == null)
            return Fail(ctx, $"passwd: user '{target}' does not exist");

        if (target != ctx.User && ctx.User != "root")
            return Fail(ctx, $"passwd: You may not view or modify password information for {target}.");

        var asRoot = ctx.User == "root";

        TerminalPendingInput AskNew()
        {
            return new TerminalPendingInput
            {
                Prompt = "New password: ",
                Handler = (next, first) =>
                {
                    if (first.Length == 0)
                        return Fail(next, "No password has been supplied.\npasswd: password unchanged");

                    next.Machine.Pending = new TerminalPendingInput
                    {
                        Prompt = "Retype new password: ",
                        Handler = (last, second) =>
                        {
                            if (first != second)
                                return Fail(last, "Sorry, passwords do not match.\npasswd: password unchanged");

                            SetPasswordHash(last.Machine, target, HashPassword(first));
                            return "passwd: password updated successfully\n";
                        },
                    };

                    return string.Empty;
                },
            };
        }

        var current = GetPasswordHash(ctx.Machine, target);
        if (asRoot || string.IsNullOrEmpty(current))
        {
            ctx.Machine.Pending = AskNew();
            return $"Changing password for {target}.\n";
        }

        ctx.Machine.Pending = new TerminalPendingInput
        {
            Prompt = "Current password: ",
            Handler = (next, input) =>
            {
                if (!CheckPassword(GetPasswordHash(next.Machine, target), input))
                    return Fail(next, "passwd: Authentication token manipulation error\npasswd: password unchanged");

                next.Machine.Pending = AskNew();
                return string.Empty;
            },
        };

        return $"Changing password for {target}.\n";
    }

    private string CmdUseradd(TerminalContext ctx, List<string> args)
    {
        var (_, operands) = ParseFlags(args);
        if (operands.Count != 1)
            return Fail(ctx, "usage: useradd LOGIN");

        if (ctx.User != "root")
            return Fail(ctx, "useradd: Permission denied.");

        var name = operands[0];
        if (!NameRegex.IsMatch(name))
            return Fail(ctx, $"useradd: invalid user name '{name}'");

        if (FindUser(ctx.Machine, name) != null)
            return Fail(ctx, $"useradd: user '{name}' already exists");

        var users = GetUsers(ctx.Machine);
        var groups = GetGroups(ctx.Machine);
        var id = Math.Max(1000, Math.Max(users.Select(user => user.Uid).DefaultIfEmpty(0).Max(), groups.Select(group => group.Gid).DefaultIfEmpty(0).Max()) + 1);
        var home = $"/home/{name}";

        EditLines(ctx.Machine, "/etc/passwd", lines => { lines.Add($"{name}:x:{id}:{id}:{name}:{home}:/bin/tsh"); return lines; });
        if (groups.All(group => group.Name != name))
            EditLines(ctx.Machine, "/etc/group", lines => { lines.Add($"{name}:x:{id}:"); return lines; });
        SetPasswordHash(ctx.Machine, name, "!");

        if (!ctx.Machine.Nodes.ContainsKey("/home"))
            ctx.Machine.Nodes["/home"] = new TerminalNode { IsDirectory = true, Mode = ModeDirectory };
        if (!ctx.Machine.Nodes.ContainsKey(home))
            ctx.Machine.Nodes[home] = new TerminalNode { IsDirectory = true, Mode = ModeDirectory, Owner = name, Group = name };

        return string.Empty;
    }

    private string CmdUserdel(TerminalContext ctx, List<string> args)
    {
        var (flags, operands) = ParseFlags(args);
        if (operands.Count != 1)
            return Fail(ctx, "usage: userdel [-r] LOGIN");

        if (ctx.User != "root")
            return Fail(ctx, "userdel: Permission denied.");

        var name = operands[0];
        var user = FindUser(ctx.Machine, name);
        if (user == null)
            return Fail(ctx, $"userdel: user '{name}' does not exist");

        if (name == "root" || name == ctx.Comp.User || ctx.Machine.UserStack.Contains(name))
            return Fail(ctx, $"userdel: user {name} is currently used by process 1");

        EditLines(ctx.Machine, "/etc/passwd", lines => lines.Where(line => !line.StartsWith(name + ":", StringComparison.Ordinal)).ToList());
        EditLines(ctx.Machine, "/etc/shadow", lines => lines.Where(line => !line.StartsWith(name + ":", StringComparison.Ordinal)).ToList());
        EditLines(ctx.Machine, "/etc/group", lines => lines
            .Where(line => !line.StartsWith(name + ":", StringComparison.Ordinal))
            .Select(line => RemoveGroupMember(line, name))
            .ToList());

        if (flags.Contains('r'))
        {
            foreach (var key in ctx.Machine.Nodes.Keys.Where(key => IsInside(key, user.Home) && user.Home != "/").ToList())
                ctx.Machine.Nodes.Remove(key);
        }

        return string.Empty;
    }

    private static string RemoveGroupMember(string line, string member)
    {
        var fields = line.Split(':');
        if (fields.Length < 4)
            return line;

        fields[3] = string.Join(',', fields[3].Split(',', StringSplitOptions.RemoveEmptyEntries).Where(found => found != member));
        return string.Join(':', fields);
    }

    private string CmdUsermod(TerminalContext ctx, List<string> args)
    {
        var (flags, operands) = ParseFlags(args);
        if (!flags.Contains('G') || operands.Count != 2)
            return Fail(ctx, "usage: usermod [-a] -G GROUP[,GROUP...] LOGIN");

        if (ctx.User != "root")
            return Fail(ctx, "usermod: Permission denied.");

        var name = operands[1];
        if (FindUser(ctx.Machine, name) == null)
            return Fail(ctx, $"usermod: user '{name}' does not exist");

        var wanted = operands[0].Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var existing = GetGroups(ctx.Machine).Select(group => group.Name).ToHashSet();
        var missing = wanted.FirstOrDefault(group => !existing.Contains(group));
        if (missing != null)
            return Fail(ctx, $"usermod: group '{missing}' does not exist");

        var appendMode = flags.Contains('a');

        EditLines(ctx.Machine, "/etc/group", lines => lines.Select(line =>
        {
            var fields = line.Split(':');
            if (fields.Length < 3)
                return line;

            if (fields.Length < 4)
                fields = fields.Append(string.Empty).ToArray();

            var members = fields[3].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            var inGroup = members.Contains(name);

            if (wanted.Contains(fields[0]) && !inGroup)
                members.Add(name);
            else if (!appendMode && !wanted.Contains(fields[0]) && inGroup)
                members.Remove(name);

            fields[3] = string.Join(',', members);
            return string.Join(':', fields);
        }).ToList());

        return string.Empty;
    }

    private string CmdApt(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return "apt 2.4.0 (x86_64)\nUsage: apt [list|search|show|install|remove|update|upgrade] ...\n";

        var action = args[0];
        var operands = args.Skip(1).Where(arg => !arg.StartsWith('-')).ToList();
        var installed = GetInstalledPackages(ctx.Machine);

        switch (action)
        {
            case "list":
            {
                var onlyInstalled = args.Contains("--installed");
                var builder = new StringBuilder("Listing... Done\n");
                foreach (var package in Packages)
                {
                    var isInstalled = installed.Contains(package.Name);
                    if (onlyInstalled && !isInstalled)
                        continue;

                    builder.Append($"{package.Name}/stable 1.0 amd64{(isInstalled ? " [installed]" : string.Empty)}\n");
                }
                return builder.ToString();
            }
            case "search":
            {
                var term = operands.FirstOrDefault() ?? string.Empty;
                var builder = new StringBuilder("Sorting... Done\nFull Text Search... Done\n");
                foreach (var package in Packages.Where(package => package.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || package.Description.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    builder.Append($"{package.Name}/stable 1.0 amd64\n  {package.Description}\n\n");
                return builder.ToString();
            }
            case "show":
            {
                var builder = new StringBuilder();
                foreach (var name in operands)
                {
                    var package = Packages.FirstOrDefault(found => found.Name == name);
                    if (package == null)
                    {
                        builder.Append(Fail(ctx, $"E: No packages found for '{name}'"));
                        continue;
                    }

                    builder.Append($"Package: {package.Name}\nVersion: 1.0\nMaintainer: TOS Service <https://tos-service.online/tempos/>\nHomepage: https://tos-service.online/tempos/\nDescription: {package.Description}\nProvides: {string.Join(", ", package.Commands.Select(command => $"{package.Directory}/{command}"))}\n\n");
                }
                return builder.ToString();
            }
            case "update":
                if (!ctx.Comp.NetworkEnabled)
                    return Fail(ctx, $"Err:1 https://tos-service.online/tempos/repo stable InRelease\n  Temporary failure resolving 'tos-service.online'");
                return $"Hit:1 https://tos-service.online/tempos/repo stable InRelease\nReading package lists... Done\nAll packages are up to date.\n";
            case "upgrade":
                return "Reading package lists... Done\n0 upgraded, 0 newly installed, 0 to remove and 0 not upgraded.\n";
            case "install":
            case "remove":
            case "purge":
                break;
            default:
                return Fail(ctx, $"E: Invalid operation {action}");
        }

        if (ctx.User != "root")
            return Fail(ctx, "E: Could not open lock file /var/lib/dpkg/lock-frontend - open (13: Permission denied)\nE: Unable to acquire the dpkg frontend lock, are you root?");

        if (operands.Count == 0)
            return Fail(ctx, $"E: No packages specified for {action}");

        var packages = new List<TerminalPackage>();
        foreach (var name in operands)
        {
            var package = Packages.FirstOrDefault(found => found.Name == name);
            if (package == null)
                return Fail(ctx, $"Reading package lists... Done\nE: Unable to locate package {name}");
            packages.Add(package);
        }

        var output = new StringBuilder("Reading package lists... Done\nBuilding dependency tree... Done\n");

        if (action == "install")
        {
            if (!ctx.Comp.NetworkEnabled)
                return Fail(ctx, $"E: Failed to fetch https://tos-service.online/tempos/repo  Temporary failure resolving 'tos-service.online'");

            foreach (var package in packages)
            {
                if (!ctx.Machine.Nodes.ContainsKey(package.Directory))
                {
                    var chain = new List<string>();
                    for (var current = package.Directory; current != "/" && !ctx.Machine.Nodes.ContainsKey(current); current = GetParent(current))
                        chain.Add(current);
                    chain.Reverse();
                    foreach (var dir in chain)
                        ctx.Machine.Nodes[dir] = new TerminalNode { IsDirectory = true, Mode = ModeDirectory };
                }

                foreach (var command in package.Commands)
                    ctx.Machine.Nodes[$"{package.Directory}/{command}"] = new TerminalNode { Content = $"{BinaryPrefix}{command}\n", Mode = ModeExecutable };

                installed.Add(package.Name);
                output.Append($"Get:1 https://tos-service.online/tempos/repo stable/main amd64 {package.Name} 1.0\nSetting up {package.Name} (1.0) ...\n");
            }
        }
        else
        {
            foreach (var package in packages)
            {
                if (!installed.Remove(package.Name))
                {
                    output.Append($"Package '{package.Name}' is not installed, so not removed\n");
                    continue;
                }

                foreach (var command in package.Commands)
                {
                    var path = $"{package.Directory}/{command}";
                    if (TryGetNode(ctx.Machine, path, out var node) && node.Content.StartsWith(BinaryPrefix, StringComparison.Ordinal))
                        ctx.Machine.Nodes.Remove(path);
                }

                output.Append($"Removing {package.Name} (1.0) ...\n");
            }
        }

        var status = string.Join('\n', installed.OrderBy(name => name, StringComparer.Ordinal));
        if (!ctx.Machine.Nodes.ContainsKey("/var/lib/dpkg"))
        {
            foreach (var dir in new[] { "/var", "/var/lib", "/var/lib/dpkg" })
            {
                if (!ctx.Machine.Nodes.ContainsKey(dir))
                    ctx.Machine.Nodes[dir] = new TerminalNode { IsDirectory = true, Mode = ModeDirectory };
            }
        }

        ctx.Machine.Nodes["/var/lib/dpkg/status"] = new TerminalNode { Content = status.Length == 0 ? string.Empty : status + "\n", Mode = ModeFile };
        return output.ToString();
    }

    private string CmdReboot(TerminalContext ctx)
    {
        if (ctx.User != "root")
            return Fail(ctx, "reboot: Interactive authentication required.");

        ResetSession(ctx.Comp, ctx.Machine);
        ApplySystemFiles(ctx.Comp, ctx.Machine);
        ctx.User = ctx.Comp.User;
        ctx.Clear = true;

        var motd = string.IsNullOrWhiteSpace(ctx.Comp.Motd) ? string.Empty : $"{ctx.Comp.Motd}\n";
        return $"Welcome to {ctx.Comp.OsName}\n{motd}\n";
    }

    private string CmdNeofetch(TerminalContext ctx)
    {
        var uptime = _timing.CurTime - ctx.Machine.BootTime;
        var packages = GetInstalledPackages(ctx.Machine).Count;
        string[] logo =
        {
            "   _____   ",
            "  |_   _|  ",
            "    | |    ",
            "    | |    ",
            "    |_|    ",
            "           ",
            "           ",
            "           ",
        };
        string[] info =
        {
            $"{ctx.User}@{ctx.Comp.Hostname}",
            $"OS: {ctx.Comp.OsName} x86_64",
            $"Uptime: {(int) uptime.TotalHours}h {uptime.Minutes}m",
            $"Packages: {packages} (apt)",
            "Shell: tsh",
            $"IP: {ctx.Comp.IpAdress}",
            $"Users: {GetUsers(ctx.Machine).Count}",
            $"Web: https://tos-service.online/tempos/",
        };

        var builder = new StringBuilder();
        for (var i = 0; i < logo.Length; i++)
            builder.Append(logo[i]).Append(' ').Append(info[i]).Append('\n');
        return builder.ToString();
    }
}
