using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Terminal;

[Serializable, NetSerializable]
public enum TerminalUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class TerminalCommandMessage : BoundUserInterfaceMessage
{
    public string PromptText;

    public TerminalCommandMessage(string promptText)
    {
        PromptText = promptText;
    }
}

[Serializable, NetSerializable]
public sealed class TerminalBoundUserInterfaceState : BoundUserInterfaceState
{
    public string OutputText { get; }
    public string? EditorPath { get; }
    public string? EditorContent { get; }

    public TerminalBoundUserInterfaceState(string outputText, string? editorPath = null, string? editorContent = null)
    {
        OutputText = outputText;
        EditorPath = editorPath;
        EditorContent = editorContent;
    }
}

[Serializable, NetSerializable]
public sealed class TerminalSaveFileMessage : BoundUserInterfaceMessage
{
    public string Path { get; }
    public string Content { get; }

    public TerminalSaveFileMessage(string path, string content)
    {
        Path = path;
        Content = content;
    }
}


public sealed partial class AgentRequestActionEvent : InstantActionEvent;
public sealed partial class AgentReceiveActionEvent : InstantActionEvent;
