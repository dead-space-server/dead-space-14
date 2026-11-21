using Content.Shared.DeadSpace.Terminal;

namespace Content.Server.DeadSpace.Terminal;

public sealed class TerminalNode
{
    public bool IsDirectory;
    public string Content = string.Empty;
    public string Owner = "root";
    public string Group = "root";
    public int Mode;

    public TerminalNode Clone()
    {
        return new TerminalNode
        {
            IsDirectory = IsDirectory,
            Content = Content,
            Owner = Owner,
            Group = Group,
            Mode = Mode,
        };
    }
}

public sealed record TerminalUser(string Name, int Uid, int Gid, string Home, string Shell);

public sealed record TerminalGroup(string Name, int Gid, List<string> Members);

public sealed record TerminalPackage(string Name, string Description, string Directory, string[] Commands, bool Default);

public sealed class TerminalPendingInput
{
    public string Prompt = string.Empty;
    public Func<TerminalContext, string, string> Handler = default!;
}

public sealed class TerminalMachine
{
    public readonly Dictionary<string, TerminalNode> Nodes = new(StringComparer.Ordinal);
    public readonly Stack<string> UserStack = new();
    public readonly List<string> History = new();
    public TerminalPendingInput? Pending;
    public string? EditorPath;
    public string? EditorUser;
    public TimeSpan BootTime;
}

public sealed class TerminalContext
{
    public EntityUid Uid;
    public TerminalComponent Comp = default!;
    public TerminalMachine Machine = default!;
    public EntityUid? Actor;
    public string User = "root";
    public bool Interactive = true;
    public bool Failed;
    public bool Deferred;
    public bool Clear;
    public bool Close;
    public string? EditorPath;
    public string? EditorContent;
    public int Depth;
    public int Steps;
}
