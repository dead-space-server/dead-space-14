// DS14-start
using Content.Client.Audio;
// DS14-end
using Content.Client._Donate.UI;
using Content.Client.DeadSpace.AshWalkers;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Lobby
{
    public sealed class LobbyState : Robust.Client.State.State
    {
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IVoteManager _voteManager = default!;
        [Dependency] private readonly ClientsidePlaytimeTrackingManager _playtimeTracking = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IPrototypeManager _protoMan = default!;

        private ClientGameTicker _gameTicker = default!;
        private AshWalkerLobbySystem _ashWalkers = default!;
        // DS14-start
        private ContentAudioSystem _contentAudioSystem = default!;
        // DS14-end

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            _ashWalkers = _entityManager.System<AshWalkerLobbySystem>();
            _ashWalkers.StateChanged += UpdateLobbyUi;
            // DS14-start
            _contentAudioSystem = _entityManager.System<ContentAudioSystem>();
            _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;
            // DS14-end

            chatController.SetMainChat(true);

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            UpdateLobbyUi();
            LoadMainScreen();

            Lobby.CharacterSetup.OnPressed += OnSetupPressed;
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;
            Lobby.AshWalkerCancelButton.OnPressed += OnAshWalkerCancel;
            _ashWalkers.RequestState();

            Lobby.DonateButton.OnPressed += OnDonatePressed;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;

            _cfg.OnValueChanged(CCCCVars.Background, OnBackgroundChanged, true);
        }

        private void OnDonatePressed(BaseButton.ButtonEventArgs obj)
        {
            var controller = _userInterfaceManager.GetUIController<DonateShopUIController>();
            controller.ToggleWindow();
        }

        private void OnBackgroundChanged(string obj)
        {
            LoadMainScreen();
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            _ashWalkers.StateChanged -= UpdateLobbyUi;
            // DS14-start
            _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;
            // DS14-end

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterSetup.OnPressed -= OnSetupPressed;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;
            Lobby.AshWalkerCancelButton.OnPressed -= OnAshWalkerCancel;

            Lobby.DonateButton.OnPressed -= OnDonatePressed;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            if (_ashWalkers.Waiting)
                _ashWalkers.Cancel();
            SetReady(false);
            Lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted || _ashWalkers.Waiting)
            {
                return;
            }

            new LateJoinGui().OpenCentered();
        }

        private void OnAshWalkerCancel(BaseButton.ButtonEventArgs args)
        {
            _ashWalkers.Cancel();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.StartTime.Text = string.Empty;
                Lobby!.StripeBack.Visible = false;
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                return;
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
            {
                text = Loc.GetString("lobby-state-paused");
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                Lobby!.StripeBack.Visible = true; // DS14
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StartTime.Text = text; // DS14
            Lobby!.StripeBack.Visible = true;
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyBackground();
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            ApplyReadyButtonStyle(); // DS14
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin || _ashWalkers.Waiting;
        }

        private void UpdateLobbyUi()
        {
            if (_gameTicker.IsGameStarted)
            {
                ApplyReadyButtonStyle(); // DS14

                Lobby!.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
                Lobby!.ReadyButton.ToggleMode = false;
                Lobby!.ReadyButton.Pressed = false;
                Lobby.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
                Lobby!.ObserveButton.Disabled = false;
            }
            else
            {
                ApplyReadyButtonStyle(); // DS14

                Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed ? "lobby-state-player-status-ready": "lobby-state-player-status-not-ready");
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ObserveButton.Disabled = true;
            }

            Lobby!.AshWalkerCancelButton.Visible = _ashWalkers.Waiting;
            Lobby.ReadyButton.ToolTip = _ashWalkers.Waiting ? Loc.GetString("ash-walker-lobby-preparing-description") : null;
            if (_ashWalkers.Waiting)
            {
                Lobby.ReadyButton.Text = Loc.GetString("ash-walker-lobby-preparing");
                Lobby.ReadyButton.ToggleMode = false;
                Lobby.ReadyButton.Disabled = true;
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
            }

            var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
            if (minutesToday > 60)
            {
                Lobby!.PlaytimeComment.Visible = true;

                var hoursToday = Math.Round(minutesToday / 60f, 1);

                var chosenString = minutesToday switch
                {
                    < 180 => "lobby-state-playtime-comment-normal",
                    < 360 => "lobby-state-playtime-comment-concerning",
                    < 720 => "lobby-state-playtime-comment-grasstouchless",
                    _ => "lobby-state-playtime-comment-selfdestructive"
                };

                Lobby.PlaytimeComment.SetMarkup(Loc.GetString(chosenString, ("hours", hoursToday)));
            }
            else
                Lobby!.PlaytimeComment.Visible = false;
        }

        // DS14-start
        private void ApplyReadyButtonStyle()
        {
            if (Lobby == null)
                return;

            if (_gameTicker.IsGameStarted)
            {
                Lobby.ReadyButton.StyleClasses.Clear();
                Lobby.ReadyButton.AddStyleClass(ContainerButton.StyleClassButton);
                Lobby.ReadyButton.AddStyleClass(DeadSpaceStyleClass.Action);
                Lobby.ReadyButton.AddStyleClass(DeadSpaceStyleClass.ActionPositive);
                return;
            }

            Lobby.ReadyButton.StyleClasses.Clear();
            Lobby.ReadyButton.AddStyleClass(ContainerButton.StyleClassButton);
            Lobby.ReadyButton.AddStyleClass(DeadSpaceStyleClass.Action);
            Lobby.ReadyButton.AddStyleClass(DeadSpaceStyleClass.Ready);
        }
        // DS14-end

        private void UpdateLobbyBackground()
        {
            if (_protoMan.TryIndex(_gameTicker.LobbyBackground, out var proto))
            {
                Lobby!.Background.Texture = _resourceCache.GetResource<TextureResource>(proto.Background);
            }
            else
            {
                Lobby!.Background.Texture = null;
            }
        }

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }

        private void LoadMainScreen()
        {
            var screenTypeString = _cfg.GetCVar(CCCCVars.Background);
            if (!Enum.TryParse(screenTypeString, out BackgroundType backgroundType))
            {
                backgroundType = default;
            }

            switch (backgroundType)
            {
                case BackgroundType.Image:
                    Lobby!.ParallaxControl.Visible = false;
                    Lobby!.Background.Visible = true;
                    break;
                case BackgroundType.Parallax:
                    Lobby!.ParallaxControl.Visible = true;
                    Lobby!.Background.Visible = false;
                    break;
            }
        }

        // DS14-start
        private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
        {
            if (ev.SoundtrackFilename == null)
            {
                Lobby!.LobbySong.SetMarkup(Loc.GetString("lobby-state-song-no-song-text"));
            }
            else if (
                ev.SoundtrackFilename != null
                && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
                )
            {
                var lobbyStream = lobbySongResource.AudioStream;

                var title = string.IsNullOrEmpty(lobbyStream.Title)
                    ? Loc.GetString("lobby-state-song-unknown-title")
                    : lobbyStream.Title;

                var artist = string.IsNullOrEmpty(lobbyStream.Artist)
                    ? Loc.GetString("lobby-state-song-unknown-artist")
                    : lobbyStream.Artist;

                var markup = Loc.GetString("lobby-state-song-text",
                    ("songTitle", title),
                    ("songArtist", artist));

                Lobby!.LobbySong.SetMarkup(markup);
            }
        }
        // DS14-end
    }
}
