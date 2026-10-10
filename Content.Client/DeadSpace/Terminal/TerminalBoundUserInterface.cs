using Content.Shared.DeadSpace.Terminal;
using Robust.Client.GameStates;
using Robust.Client.UserInterface;
using System.Collections.Generic;

namespace Content.Client.DeadSpace.Terminal;

public sealed class TerminalBoundUserInterface : BoundUserInterface
{
    private TerminalWindow? _window;
    private readonly IEntityManager _entityManager = IoCManager.Resolve<IEntityManager>();
    private readonly Queue<string> _pendingCommands = new();
    private string _history = "";
    public TerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<TerminalWindow>();
        _window.CommandEntered += OnCommandEntered;
        _window.ClearOutput();
        _window.FocusInput();
        _window.FileSaved += OnFileSaved;
        _window.EditorClosed += OnEditorClosed;
        if (!_entityManager.TryGetComponent<TerminalComponent>(Owner, out var terminal))
            return;
        if (string.IsNullOrEmpty(_history))
        {
            _history = $"Welcome to TempOS 107.05 LTS\n" + $"System information\n" + $"Memory usage: {Random.Shared.Next(5, 10)}%\n" + $"IPv4 address for eth0: {terminal.IpAdress}\n" + $"[color=#16C60C]user@TEMPUser{terminal.UserIndex}[/color]:" + $"[color=#3B78FF]{terminal.CurrentDir}[/color]$ ";
        }
        _window.SetOutput(_history);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not TerminalBoundUserInterfaceState terminalState)
            return;
        if (!_entityManager.TryGetComponent<TerminalComponent>(Owner, out var terminal))
            return;
        if (_pendingCommands.TryDequeue(out var command))
        {
            _window?.AddCommand($"", command);
        }

        if (terminalState.EditorPath != null)
        {
            _window?.OpenEditor(terminalState.EditorPath, terminalState.EditorContent ?? string.Empty);
            return;
        }

        if (terminalState.OutputText == "\x01Clear")
        {
            _history = "";
        }
        else
        {
            _history += terminalState.OutputText;
            if (!terminalState.OutputText.EndsWith('\n'))
                _history += "\n";
        }

        _window?.AddOutput(terminalState.OutputText);
        var prompt = $"[color=#16C60C]user@TEMPUser{terminal.UserIndex}[/color]:" + $"[color=#3365D5]{terminal.CurrentDir}[/color]$ ";
        _history += prompt;
        _window?.AddPrompt(prompt);
    }

    private void OnFileSaved(string path, string content)
    {
        SendMessage(new TerminalSaveFileMessage(path, content));
    }

    private void OnEditorClosed()
    {
        _window?.FocusInput();
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

    private void OnCommandEntered(string command)
    {
        _pendingCommands.Enqueue(command);
        SendMessage(new TerminalCommandMessage(command));
    }
}
