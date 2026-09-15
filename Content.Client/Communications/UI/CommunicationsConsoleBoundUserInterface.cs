using Content.Client.DeadSpace.Communications.UI;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Communications;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;

namespace Content.Client.Communications.UI
{
    /// <summary>
    /// The BUI for the communications console.
    /// Handles sending messages back to the server to call the shuttle,
    /// send messages, set the alert level, and set the text on screens.
    /// </summary>
    /// <seealso cref="CommunicationsConsoleComponent"/>
    public sealed class CommunicationsConsoleBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IUserInterfaceManager _uiManager = default!; // DS14

        [ViewVariables]
        private CommunicationsConsoleMenu? _menu;

        // DS14-start
        [ViewVariables]
        private EmagCommunicationsInterface? _emagMenu;
        private EmagCommunicationsUiMode _emagMode = EmagCommunicationsUiMode.Unavailable;
        private EmagCommunicationsConsoleAccessStateMessage? _pendingAccessState;
        private bool _showingEmagMenu;
        // DS14-end

        public CommunicationsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

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

            SetBroadcastPreview();

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

        public void AlertLevelSelected(string level)
        {
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

        // DS14-start - alert levels are still server-owned on this engine/content branch.
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not CommunicationsConsoleInterfaceState commsState || _menu == null)
                return;

            var canChangeAlertLevel = commsState.AlertLevels != null &&
                                      !float.IsNaN(commsState.CurrentAlertDelay) &&
                                      commsState.CurrentAlertDelay <= 0;

            _menu.UpdateState(commsState,
                commsState.CurrentAlert,
                commsState.CurrentAlertColor,
                commsState.AlertLevels,
                canChangeAlertLevel);
        }
        // DS14-end

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
            if (_menu != null && EntMan.TryGetComponent<CommunicationsConsoleComponent>(Owner, out var console))
                _menu.SetBroadcastDisplayEntity(console.ScreenDisplayId);
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
}
