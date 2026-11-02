using Content.Shared.DeadSpace.Terminal;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Terminal;

public sealed class TerminalBoundUserInterface : BoundUserInterface
{
    private const int MaxHistoryLength = 32_000;

    private TerminalWindow? _window;
    private readonly Queue<string> _pendingCommands = new();
    private string _history = string.Empty;
    private bool _hideNextInput;

    public TerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<TerminalWindow>();
        _window.CommandEntered += OnCommandEntered;
        _window.FileSaved += OnFileSaved;
        _window.EditorClosed += OnEditorClosed;
        _window.ClearOutput();
        _window.FocusInput();

        if (string.IsNullOrEmpty(_history) && EntMan.TryGetComponent<TerminalComponent>(Owner, out var terminal))
        {
            _history = FormattedMessage.EscapeText($"Welcome to {terminal.OsName}\n");
            if (!string.IsNullOrWhiteSpace(terminal.Motd))
                _history += FormattedMessage.EscapeText($"{terminal.Motd.TrimEnd()}\n");
            _history += FormattedMessage.EscapeText($" * Documentation: https://tos-service.online/tempos/docs.html\n * Support:       https://tos-service.online/tempos/\n");
            _history += FormattedMessage.EscapeText($"\nSystem information\nMemory usage: {Random.Shared.Next(5, 10)}%\nIPv4 address for eth0: {terminal.IpAdress}\n\n");
            _history += terminal.Prompt;
        }

        _window.SetOutput(_history);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not TerminalBoundUserInterfaceState terminalState)
            return;

        if (_pendingCommands.TryDequeue(out var command))
            _history += FormattedMessage.EscapeText(command) + "\n";

        if (terminalState.Clear)
            _history = string.Empty;

        if (!string.IsNullOrEmpty(terminalState.OutputText))
        {
            _history += FormattedMessage.EscapeText(terminalState.OutputText);
            if (!terminalState.OutputText.EndsWith('\n'))
                _history += "\n";
        }

        _history += terminalState.Prompt;
        _hideNextInput = terminalState.HideInput;

        if (_history.Length > MaxHistoryLength)
        {
            var cut = _history.IndexOf('\n', _history.Length - MaxHistoryLength);
            _history = cut < 0 ? _history[^MaxHistoryLength..] : _history[(cut + 1)..];
        }

        _window?.SetOutput(_history);

        if (terminalState.EditorPath != null)
            _window?.OpenEditor(terminalState.EditorPath, terminalState.EditorContent ?? string.Empty);
    }

    private void OnFileSaved(string path, string content)
    {
        SendMessage(new TerminalSaveFileMessage(path, content));
    }

    private void OnEditorClosed()
    {
        _window?.FocusInput();
    }

    private void OnCommandEntered(string command)
    {
        _pendingCommands.Enqueue(_hideNextInput ? string.Empty : command);
        _hideNextInput = false;
        SendMessage(new TerminalCommandMessage(command));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.CommandEntered -= OnCommandEntered;
            _window.FileSaved -= OnFileSaved;
            _window.EditorClosed -= OnEditorClosed;
        }

        base.Dispose(disposing);
    }
}
