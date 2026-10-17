using Content.Shared.DeadSpace.Terminal;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Terminal;

public sealed partial class TerminalWindow : DefaultWindow
{
    public event Action<string>? CommandEntered;
    private readonly LineEdit _commandInput;
    private readonly RichTextLabel _output;
    private readonly ScrollContainer _outputScroll;
    private readonly TextEdit _editor;
    private readonly Label _editorStatus;
    private readonly BoxContainer _editorButtons;
    private readonly Button _saveButton;
    private readonly Button _exitButton;
    private readonly PanelContainer _terminalPanel;
    private readonly PanelContainer _editorPanel;

    private string? _editorPath;
    public bool IsEditing { get; private set; }
    public event Action<string, string>? FileSaved;
    public event Action? EditorClosed;

    public TerminalWindow()
    {
        RobustXamlLoader.Load(this);

        _commandInput = FindControl<LineEdit>("CommandInput");
        _output = FindControl<RichTextLabel>("Output");
        _outputScroll = FindControl<ScrollContainer>("OutputScroll");
        _editor = FindControl<TextEdit>("Editor");
        _editorStatus = FindControl<Label>("EditorStatus");
        _editorButtons = FindControl<BoxContainer>("EditorButtons");
        _saveButton = FindControl<Button>("SaveButton");
        _exitButton = FindControl<Button>("ExitButton");
        _terminalPanel = FindControl<PanelContainer>("TerminalPanel");
        _editorPanel = FindControl<PanelContainer>("EditorPanel");

        _saveButton.OnPressed += _ => SaveEditor();
        _exitButton.OnPressed += _ => CloseEditor();

        _commandInput.OnTextEntered += OnCommandEntered;
    }

    public void ClearOutput()
    {
        _output.SetMessage(string.Empty);
        _outputScroll.VScroll = 0;
    }

    public void SetOutput(string text)
    {
        _output.SetMessage(FormattedMessage.FromMarkupOrThrow(text));
        ScrollToBottom();
    }
    private void ScrollToBottom()
    {
        UserInterfaceManager.DeferAction(() =>
        {
            var maxScroll = Math.Max(0f, _output.PixelHeight - _outputScroll.PixelHeight);
            _outputScroll.VScrollTarget = maxScroll;
        });
    }

    public void AddPrompt(string prompt)
    {
        _output.Text += $"{prompt}";
        ScrollToBottom();
    }

    public void AddCommand(string prompt, string command)
    {
        _output.Text += $"{prompt}{command}\n";
        ScrollToBottom();
    }

    public void AddOutput(string text)
    {
        if (text == "\x01CLEAR")
        {
            _output.SetMessage(string.Empty);
            ScrollToBottom();
            return;
        }

        _output.Text += text;

        if (!text.EndsWith('\n'))
            _output.Text += "\n";
        ScrollToBottom();
    }

    private void OnCommandEntered(LineEdit.LineEditEventArgs args)
    {
        CommandEntered?.Invoke(args.Text);

        _commandInput.Clear();
        _commandInput.GrabKeyboardFocus();
    }

    public void OpenEditor(string path, string content)
    {
        _editorPath = path;
        _editor.TextRope = new Rope.Leaf(content);
        IsEditing = true;

        _terminalPanel.Visible = false;
        _editorPanel.Visible = true;
        _commandInput.Visible = false;

        _editorStatus.Text = $"{path}";
        _editor.GrabKeyboardFocus();
    }

    private void SaveEditor()
    {
        if (_editorPath == null)
            return;
        FileSaved?.Invoke(_editorPath, Rope.Collapse(_editor.TextRope));
    }

    private void CloseEditor()
    {
        IsEditing = false;
        _editorPath = null;
        _editorPanel.Visible = false;
        _terminalPanel.Visible = true;
        _commandInput.Visible = true;
        EditorClosed?.Invoke();
        _commandInput.GrabKeyboardFocus();
    }

    public void FocusInput()
    {
        _commandInput.GrabKeyboardFocus();
    }
}
