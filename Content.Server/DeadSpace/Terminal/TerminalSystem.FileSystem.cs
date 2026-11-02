using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.DeadSpace.Terminal;

namespace Content.Server.DeadSpace.Terminal;

public sealed partial class TerminalSystem
{
    private const int ModeDirectory = 493;
    private const int ModeFile = 420;
    private const int ModeExecutable = 493;
    private const int ModePrivateDirectory = 448;
    private const int ModePrivateFile = 384;
    private const int ModeOpen = 511;
    private const int MaxFileLength = 16_384;
    private const int MaxNodes = 1024;
    private const string BinaryPrefix = "ELF ";
    private const string DefaultUser = "user";

    private static readonly Regex HostnameSanitizer = new(@"[^A-Za-z0-9.\-]", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new(@"^[a-z_][a-z0-9_\-]{0,31}$", RegexOptions.Compiled);

    private static readonly string[] PathDirectories = { "/bin", "/usr/bin" };

    private static readonly TerminalPackage[] Packages =
    {
        new("coreutils", "GNU core utilities", "/bin", new[] { "ls", "cat", "touch", "mkdir", "rmdir", "rm", "cp", "mv", "chmod", "chown", "uname", "whoami", "id", "groups", "clear", "hostname" }, true),
        new("login", "user and session management", "/bin", new[] { "su", "passwd", "useradd", "userdel", "usermod" }, true),
        new("sudo", "execute commands as the superuser", "/usr/bin", new[] { "sudo" }, true),
        new("net-tools", "networking toolkit", "/bin", new[] { "ifconfig", "ping" }, true),
        new("nano", "small and friendly text editor", "/usr/bin", new[] { "nano" }, true),
        new("tsh", "TempOS script interpreter", "/usr/bin", new[] { "run" }, true),
        new("apt", "package manager", "/usr/bin", new[] { "apt" }, true),
        new("systemd", "system and service manager", "/bin", new[] { "reboot" }, true),
        new("neofetch", "system information tool", "/usr/bin", new[] { "neofetch" }, false),
    };

    private TerminalMachine GetMachine(EntityUid uid, TerminalComponent comp)
    {
        if (_machines.TryGetValue(uid, out var machine))
            return machine;

        machine = CreateMachine(comp);
        _machines[uid] = machine;
        return machine;
    }

    private TerminalMachine CreateMachine(TerminalComponent comp)
    {
        var machine = new TerminalMachine { BootTime = _timing.CurTime };
        var nodes = machine.Nodes;

        void Dir(string path, int mode, string owner = "root")
        {
            nodes[path] = new TerminalNode { IsDirectory = true, Mode = mode, Owner = owner, Group = owner };
        }

        void File(string path, string content, int mode = ModeFile, string owner = "root")
        {
            nodes[path] = new TerminalNode { Content = content, Mode = mode, Owner = owner, Group = owner };
        }

        Dir("/", ModeDirectory);
        Dir("/bin", ModeDirectory);
        Dir("/etc", ModeDirectory);
        Dir("/home", ModeDirectory);
        Dir($"/home/{DefaultUser}", ModeDirectory, DefaultUser);
        Dir("/root", ModePrivateDirectory);
        Dir("/tmp", ModeOpen);
        Dir("/usr", ModeDirectory);
        Dir("/usr/bin", ModeDirectory);
        Dir("/var", ModeDirectory);
        Dir("/var/lib", ModeDirectory);
        Dir("/var/lib/dpkg", ModeDirectory);

        var osName = comp.OsName.Trim();
        var space = osName.IndexOf(' ');
        var name = space > 0 ? osName[..space] : osName;
        var version = space > 0 ? osName[(space + 1)..] : string.Empty;

        File("/etc/hostname", $"TEMPUser{comp.UserIndex}\n");
        File("/etc/motd", $"Unauthorized access is prohibited.\nTempOS by TOS Service: https://tos-service.online/tempos/\n");
        File("/etc/os-release", $"NAME=\"{name}\"\nVERSION=\"{version}\"\nPRETTY_NAME=\"{osName}\"\nHOME_URL=\"https://tos-service.online/tempos/\"\nDOCUMENTATION_URL=\"https://tos-service.online/tempos/docs.html\"\n");
        File("/etc/passwd", $"root:x:0:0:root:/root:/bin/tsh\n{DefaultUser}:x:1000:1000:{DefaultUser}:/home/{DefaultUser}:/bin/tsh\n");
        File("/etc/group", $"root:x:0:\n{DefaultUser}:x:1000:\n");
        File("/etc/shadow", $"root:{HashPassword(RootPassword)}\n{DefaultUser}:\n", ModePrivateFile);

        var installed = new StringBuilder();
        foreach (var package in Packages)
        {
            if (!package.Default)
                continue;

            installed.Append(package.Name).Append('\n');
            foreach (var command in package.Commands)
                File($"{package.Directory}/{command}", $"{BinaryPrefix}{command}\n", ModeExecutable);
        }

        File("/var/lib/dpkg/status", installed.ToString());
        return machine;
    }

    private void ResetSession(TerminalComponent comp, TerminalMachine machine)
    {
        machine.UserStack.Clear();
        machine.Pending = null;
        machine.EditorPath = null;
        machine.EditorUser = null;
        machine.BootTime = _timing.CurTime;

        var users = GetUsers(machine);
        var login = users.Where(user => user.Uid >= 1000).OrderBy(user => user.Uid).FirstOrDefault();
        comp.User = login?.Name ?? "root";

        var home = GetHome(machine, comp.User);
        comp.CurrentDir = TryGetNode(machine, home, out var node) && node.IsDirectory ? home : "/";
    }

    private void ApplySystemFiles(TerminalComponent comp, TerminalMachine machine)
    {
        var hostname = string.Empty;
        if (TryGetNode(machine, "/etc/hostname", out var hostnameNode) && !hostnameNode.IsDirectory)
            hostname = HostnameSanitizer.Replace(FirstLine(hostnameNode.Content), string.Empty);
        if (hostname.Length > 32)
            hostname = hostname[..32];
        comp.Hostname = hostname.Length == 0 ? "localhost" : hostname;

        var osName = string.Empty;
        if (TryGetNode(machine, "/etc/os-release", out var releaseNode) && !releaseNode.IsDirectory)
        {
            var values = ParseKeyValues(releaseNode.Content);
            if (!values.TryGetValue("PRETTY_NAME", out osName!) || osName.Length == 0)
                values.TryGetValue("NAME", out osName!);
        }
        osName ??= string.Empty;
        if (osName.Length > 64)
            osName = osName[..64];
        comp.OsName = osName.Length == 0 ? "Unknown OS" : osName;

        var motd = string.Empty;
        if (TryGetNode(machine, "/etc/motd", out var motdNode) && !motdNode.IsDirectory)
            motd = motdNode.Content.TrimEnd();
        if (motd.Length > 1024)
            motd = motd[..1024];
        comp.Motd = motd;

        comp.Home = GetHome(machine, comp.User);
    }

    private static Dictionary<string, string> ParseKeyValues(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            var equals = line.IndexOf('=');
            if (equals <= 0 || line.StartsWith('#'))
                continue;

            var value = line[(equals + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];

            values[line[..equals].Trim()] = value;
        }

        return values;
    }

    private static string FirstLine(string content)
    {
        var newline = content.IndexOf('\n');
        return (newline < 0 ? content : content[..newline]).Trim();
    }

    private static string NormalizePath(string currentDir, string path)
    {
        var combinedPath = path.StartsWith('/') ? path : $"{currentDir}/{path}";
        var normalized = new List<string>();

        foreach (var part in combinedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
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

    private string ResolvePath(TerminalContext ctx, string path)
    {
        if (path == "~")
            return GetHome(ctx.Machine, ctx.User);

        if (path.StartsWith("~/", StringComparison.Ordinal))
            return NormalizePath(GetHome(ctx.Machine, ctx.User), path[2..]);

        return NormalizePath(ctx.Comp.CurrentDir, path);
    }

    private static string GetParent(string path)
    {
        if (path == "/")
            return "/";

        var separator = path.LastIndexOf('/');
        return separator <= 0 ? "/" : path[..separator];
    }

    private static string GetName(string path)
    {
        return path == "/" ? "/" : path[(path.LastIndexOf('/') + 1)..];
    }

    private static bool IsInside(string path, string root)
    {
        return root == "/" || path == root || path.StartsWith(root + "/", StringComparison.Ordinal);
    }

    private static bool TryGetNode(TerminalMachine machine, string path, out TerminalNode node)
    {
        return machine.Nodes.TryGetValue(path, out node!);
    }

    private static IEnumerable<string> GetChildren(TerminalMachine machine, string dir)
    {
        return machine.Nodes.Keys.Where(path => path != "/" && GetParent(path) == dir);
    }

    private static List<TerminalUser> GetUsers(TerminalMachine machine)
    {
        var users = new List<TerminalUser>();
        if (!TryGetNode(machine, "/etc/passwd", out var node) || node.IsDirectory)
            return users;

        foreach (var raw in node.Content.Split('\n'))
        {
            var fields = raw.Trim().Split(':');
            if (fields.Length < 6 || fields[0].Length == 0 || !int.TryParse(fields[2], out var uid) || !int.TryParse(fields[3], out var gid))
                continue;

            users.Add(new TerminalUser(fields[0], uid, gid, fields[5], fields.Length > 6 ? fields[6] : "/bin/tsh"));
        }

        return users;
    }

    private static List<TerminalGroup> GetGroups(TerminalMachine machine)
    {
        var groups = new List<TerminalGroup>();
        if (!TryGetNode(machine, "/etc/group", out var node) || node.IsDirectory)
            return groups;

        foreach (var raw in node.Content.Split('\n'))
        {
            var fields = raw.Trim().Split(':');
            if (fields.Length < 3 || fields[0].Length == 0 || !int.TryParse(fields[2], out var gid))
                continue;

            var members = fields.Length > 3
                ? fields[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : new List<string>();
            groups.Add(new TerminalGroup(fields[0], gid, members));
        }

        return groups;
    }

    private static TerminalUser? FindUser(TerminalMachine machine, string name)
    {
        return GetUsers(machine).FirstOrDefault(user => user.Name == name);
    }

    private static string GetHome(TerminalMachine machine, string user)
    {
        return FindUser(machine, user)?.Home ?? "/";
    }

    private static string GetPrimaryGroup(TerminalMachine machine, string user)
    {
        var found = FindUser(machine, user);
        if (found == null)
            return user;

        return GetGroups(machine).FirstOrDefault(group => group.Gid == found.Gid)?.Name ?? user;
    }

    private static List<TerminalGroup> GetUserGroups(TerminalMachine machine, string user)
    {
        var found = FindUser(machine, user);
        return GetGroups(machine).Where(group => group.Members.Contains(user) || found != null && group.Gid == found.Gid).ToList();
    }

    private static bool IsInGroup(TerminalMachine machine, string user, string group)
    {
        return GetUserGroups(machine, user).Any(found => found.Name == group);
    }

    private static string? GetPasswordHash(TerminalMachine machine, string user)
    {
        if (!TryGetNode(machine, "/etc/shadow", out var node) || node.IsDirectory)
            return null;

        foreach (var raw in node.Content.Split('\n'))
        {
            var line = raw.Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0 || line[..separator] != user)
                continue;

            var rest = line[(separator + 1)..];
            var end = rest.IndexOf(':');
            return end < 0 ? rest : rest[..end];
        }

        return null;
    }

    private static bool IsLocked(string? hash)
    {
        return hash == null || hash.StartsWith('!') || hash.StartsWith('*');
    }

    private static string HashPassword(string password)
    {
        return "$5$" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
    }

    private static bool CheckPassword(string? hash, string password)
    {
        if (IsLocked(hash))
            return false;

        return hash!.Length == 0 ? password.Length == 0 : hash == HashPassword(password);
    }

    private static void SetPasswordHash(TerminalMachine machine, string user, string hash)
    {
        if (!TryGetNode(machine, "/etc/shadow", out var node) || node.IsDirectory)
        {
            node = new TerminalNode { Mode = ModePrivateFile };
            machine.Nodes["/etc/shadow"] = node;
        }

        var lines = node.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var index = lines.FindIndex(line => line.StartsWith(user + ":", StringComparison.Ordinal));
        if (index >= 0)
            lines[index] = $"{user}:{hash}";
        else
            lines.Add($"{user}:{hash}");

        node.Content = string.Join('\n', lines) + "\n";
    }

    private static void EditLines(TerminalMachine machine, string path, Func<List<string>, List<string>> edit)
    {
        if (!TryGetNode(machine, path, out var node) || node.IsDirectory)
        {
            node = new TerminalNode { Mode = ModeFile };
            machine.Nodes[path] = node;
        }

        var lines = edit(node.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        node.Content = lines.Count == 0 ? string.Empty : string.Join('\n', lines) + "\n";
    }

    private bool HasAccess(TerminalContext ctx, TerminalNode node, int bit)
    {
        if (ctx.User == "root")
            return bit != 1 || node.IsDirectory || (node.Mode & 73) != 0;

        if (node.Owner == ctx.User)
            return ((node.Mode >> 6) & bit) != 0;

        if (IsInGroup(ctx.Machine, ctx.User, node.Group))
            return ((node.Mode >> 3) & bit) != 0;

        return (node.Mode & bit) != 0;
    }

    private bool CanRead(TerminalContext ctx, TerminalNode node) => HasAccess(ctx, node, 4);

    private bool CanWrite(TerminalContext ctx, TerminalNode node) => HasAccess(ctx, node, 2);

    private bool CanExecute(TerminalContext ctx, TerminalNode node) => HasAccess(ctx, node, 1);

    private bool CanTraverse(TerminalContext ctx, string path)
    {
        var current = GetParent(path);
        var chain = new List<string>();

        while (true)
        {
            chain.Add(current);
            if (current == "/")
                break;
            current = GetParent(current);
        }

        foreach (var dir in chain)
        {
            if (!TryGetNode(ctx.Machine, dir, out var node) || !node.IsDirectory || !CanExecute(ctx, node))
                return false;
        }

        return true;
    }

    private string? CheckCreate(TerminalContext ctx, string path, string command, string display)
    {
        if (ctx.Machine.Nodes.Count >= MaxNodes)
            return $"{command}: cannot create '{display}': No space left on device";

        var parent = GetParent(path);
        if (!TryGetNode(ctx.Machine, parent, out var parentNode) || !parentNode.IsDirectory)
            return $"{command}: cannot create '{display}': No such file or directory";

        if (!CanTraverse(ctx, path) || !CanWrite(ctx, parentNode) || !CanExecute(ctx, parentNode))
            return $"{command}: cannot create '{display}': Permission denied";

        return null;
    }

    private string? CheckRemove(TerminalContext ctx, string path, string command, string display)
    {
        if (path == "/")
            return $"{command}: cannot remove '/': Device or resource busy";

        var parent = GetParent(path);
        if (!TryGetNode(ctx.Machine, parent, out var parentNode) || !CanTraverse(ctx, path) || !CanWrite(ctx, parentNode))
            return $"{command}: cannot remove '{display}': Permission denied";

        return null;
    }

    private TerminalNode NewNode(TerminalContext ctx, bool directory)
    {
        return new TerminalNode
        {
            IsDirectory = directory,
            Owner = ctx.User,
            Group = GetPrimaryGroup(ctx.Machine, ctx.User),
            Mode = directory ? ModeDirectory : ModeFile,
        };
    }

    private string? WriteFile(TerminalContext ctx, string path, string content, bool append)
    {
        var display = path;
        if (content.Length > MaxFileLength)
            return $"{display}: File too large";

        if (TryGetNode(ctx.Machine, path, out var node))
        {
            if (node.IsDirectory)
                return $"{display}: Is a directory";
            if (!CanTraverse(ctx, path) || !CanWrite(ctx, node))
                return $"{display}: Permission denied";

            var result = append ? node.Content + content : content;
            if (result.Length > MaxFileLength)
                return $"{display}: File too large";

            node.Content = result;
            return null;
        }

        if (CheckCreate(ctx, path, "write", display) is { } error)
            return error;

        var created = NewNode(ctx, false);
        created.Content = content;
        ctx.Machine.Nodes[path] = created;
        return null;
    }

    private static string FormatMode(TerminalNode node)
    {
        var builder = new StringBuilder(node.IsDirectory ? "d" : "-");
        const string letters = "rwx";

        for (var i = 8; i >= 0; i--)
            builder.Append((node.Mode >> i & 1) != 0 ? letters[2 - i % 3] : '-');

        return builder.ToString();
    }

    private HashSet<string> GetInstalledPackages(TerminalMachine machine)
    {
        var installed = new HashSet<string>(StringComparer.Ordinal);
        if (!TryGetNode(machine, "/var/lib/dpkg/status", out var node) || node.IsDirectory)
            return installed;

        foreach (var raw in node.Content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length > 0)
                installed.Add(line);
        }

        return installed;
    }
}
