using Content.Client.DeadSpace.Communications.UI; // DS14
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.AlertLevel;
using Content.Shared.Communications;
using Content.Shared.Station.Systems;
using Content.Shared.DeadSpace.CCCCVars; // DS14
using Content.Shared.DeadSpace.Communications; // DS14
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls; // DS14
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Communications.UI;

/// <summary>
/// The BUI for the communications console.
/// Handles sending messages back to the server to call the shuttle,
/// send messages, set the alert level, and set the text on screens.
/// </summary>
/// <seealso cref="CommunicationsConsoleComponent"/>
public sealed partial class CommunicationsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!; // DS14
    [Dependency] private StationSystem _station = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;

    [Dependency] private EntityQuery<AlertLevelComponent> _alertLevelQuery;

    [ViewVariables]
    private CommunicationsConsoleMenu? _menu;

    // DS14-start
    [ViewVariables]
    private EmagCommunicationsInterface? _emagMenu;
    private EmagCommunicationsUiMode _emagMode = EmagCommunicationsUiMode.Unavailable;
    private EmagCommunicationsConsoleAccessStateMessage? _pendingAccessState;
    private bool _showingEmagMenu;
    // DS14-end

    private static readonly EntProtoId FallbackScreen = "Screen";
    private static readonly ProtoId<AlertLevelPrototype> UnknownAlertLevel = "Unknown";

    /// <inheritdoc/>
    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CommunicationsConsoleMenu>();
        _menu.OnRadioAnnounce += RadioAnnounceButtonPressed;
        _menu.OnScreenBroadcast += ScreenBroadcastButtonPressed;
        _menu.OnAlertLevelChanged += AlertLevelSelected;
        _menu.OnShuttleCalled += CallShuttle;
        _menu.OnShuttleRecalled += RecallShuttle;

        SetBroadcastPreview(); // DS14: shared with ShowNormalMenu

        // DS14-start
        _menu.OnEmagChannel += ShowEmagMenu;

        _emagMenu = this.CreateDisposableControl<EmagCommunicationsInterface>();
        _emagMenu.OnClose += Close;
        _emagMenu.OnSetPasswordRequested += password =>
            SendMessage(new EmagCommunicationsConsoleSetPasswordMessage(password));
        _emagMenu.OnUnlockRequested += password =>
            SendMessage(new EmagCommunicationsConsoleUnlockMessage(password));
        _emagMenu.OnAnnounceRequested += SendMessage;
        _emagMenu.OnReturnToNormalRequested += ShowNormalMenu;

        if (_pendingAccessState != null)
        {
            ApplyEmagAccessState(_pendingAccessState);
            _pendingAccessState = null;
        }

        SendMessage(new EmagCommunicationsConsoleRequestAccessStateMessage());
        // DS14-end
    }

    public void AlertLevelSelected(ProtoId<AlertLevelPrototype> level)
    {
        // TODO: This does not work until the console UI is predicted and uses component states.
        // Also someone decided to send BUI states regularly in an update loop, so this just gets randomly bulldozed until the message reaches the server.
        // _menu.CurrentAlertLevel = level;
        // _menu.AlertLevelSelectable = false;
        // _menu.AlertLevelButton.Disabled = true;
        SendMessage(new CommunicationsConsoleSelectAlertLevelMessage(level));
    }

    public void RadioAnnounceButtonPressed(string message)
    {
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
        SendMessage(new CommunicationsConsoleAnnounceMessage(msg));
    }

    public void ScreenBroadcastButtonPressed(string message)
    {
        // DS14: retain the fork's separate broadcast length limit.
        var sanitized = SharedChatSystem.SanitizeAnnouncement(message, _cfg.GetCVar(CCCCVars.MaxBroadcastLength));
        SendMessage(new CommunicationsConsoleBroadcastMessage(sanitized));
    }

    public void CallShuttle()
    {
        SendMessage(new CommunicationsConsoleCallEmergencyShuttleMessage());
    }

    public void RecallShuttle()
    {
        SendMessage(new CommunicationsConsoleRecallEmergencyShuttleMessage());
    }

    // TODO: Use component states and update in an AfterAutoHandleState subscription
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null)
            return;

        if (state is not CommunicationsConsoleInterfaceState commsState)
            return;

        var stationUid = _station.GetOwningStation(Owner);

        ProtoId<AlertLevelPrototype> currentAlertLevel;
        List<ProtoId<AlertLevelPrototype>> selectableAlertLevels;
        bool canChangeAlertLevel;
        if (_alertLevelQuery.TryGetComponent(stationUid, out var alertComp))
        {
            currentAlertLevel = alertComp.CurrentAlertLevel;
            selectableAlertLevels = _alertLevel.GetSelectableAlertLevels((stationUid.Value, alertComp));
            canChangeAlertLevel = _alertLevel.CanChangeAlertLevel((stationUid.Value, alertComp));
        }
        else
        {
            currentAlertLevel = UnknownAlertLevel;
            selectableAlertLevels = new() { UnknownAlertLevel };
            canChangeAlertLevel = false;
        }

        _menu.UpdateState(commsState, currentAlertLevel, selectableAlertLevels, canChangeAlertLevel);
    }

    // DS14-start
    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is not EmagCommunicationsConsoleAccessStateMessage accessState)
            return;

        if (_menu == null || _emagMenu == null)
        {
            _pendingAccessState = accessState;
            return;
        }

        ApplyEmagAccessState(accessState);
    }

    private void ApplyEmagAccessState(EmagCommunicationsConsoleAccessStateMessage accessState)
    {
        if (_menu == null || _emagMenu == null)
        {
            _pendingAccessState = accessState;
            return;
        }

        _emagMode = accessState.Mode;
        _menu.EmagChannelButton.Visible = accessState.Mode != EmagCommunicationsUiMode.Unavailable;
        _emagMenu.SetAccessState(accessState.Mode, accessState.Error, accessState.CanAnnounce);

        if (accessState.Mode == EmagCommunicationsUiMode.Unavailable && _showingEmagMenu)
            ShowNormalMenu();
    }

    private void ShowEmagMenu()
    {
        if (_emagMode == EmagCommunicationsUiMode.Unavailable ||
            _menu == null ||
            _emagMenu == null ||
            _showingEmagMenu)
        {
            return;
        }

        DetachWindow(_menu);
        _emagMenu.OpenCentered();
        _showingEmagMenu = true;
    }

    private void ShowNormalMenu()
    {
        if (_menu == null || _emagMenu == null || !_showingEmagMenu)
            return;

        _emagMenu.PrepareToHide();
        DetachWindow(_emagMenu);
        SetBroadcastPreview();
        _menu.OpenCentered();
        _showingEmagMenu = false;
    }

    private void SetBroadcastPreview()
    {
        if (_menu == null)
            return;

        if (EntMan.TryGetComponent<CommunicationsConsoleComponent>(Owner, out var console))
            _menu.SetBroadcastDisplayEntity(console.ScreenDisplayId);
        else
            _menu.SetBroadcastDisplayEntity(FallbackScreen);
    }

    private void DetachWindow(BaseWindow window)
    {
        if (!window.IsOpen)
            return;

        ReleaseFocusWithin(window);

        // ControlHidden clears a WindowRoot's stored focus while descendants still have their Root.
        // Orphan alone clears Root first and can leave a stale stored focus after an OS-level alt-tab.
        window.Visible = false;
        window.Orphan();
        window.Visible = true;
    }

    private void ReleaseFocusWithin(BaseWindow window)
    {
        if (IsDescendantOf(_uiManager.KeyboardFocused, window))
            _uiManager.ReleaseKeyboardFocus();

        if (IsDescendantOf(_uiManager.ControlFocused, window))
            _uiManager.ControlFocused = null;
    }

    private static bool IsDescendantOf(Control? control, Control ancestor)
    {
        for (; control != null; control = control.Parent)
        {
            if (control == ancestor)
                return true;
        }

        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _emagMenu?.PrepareToHide();

            if (_menu != null)
                DetachWindow(_menu);

            if (_emagMenu != null)
                DetachWindow(_emagMenu);
        }

        base.Dispose(disposing);
    }
    // DS14-end
}
