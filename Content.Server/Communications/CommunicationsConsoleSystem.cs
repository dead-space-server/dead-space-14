using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Server.Screens.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Content.Shared.Corvax.TTS;
using Content.Shared.Database;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Communications;
using Content.Shared.DeadSpace.Languages.Components;
using Content.Shared.DeadSpace.Languages.Prototypes;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Content.Server.DeadSpace.Languages;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.GameTicking; //DS14

namespace Content.Server.Communications
{
    public sealed class CommunicationsConsoleSystem : EntitySystem
    {
        [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
        [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
        [Dependency] private readonly EmergencyShuttleSystem _emergency = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly EmagSystem _emag = default!; // DS14
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // DS14
        private const float UIUpdateInterval = 5.0f;

        // DS14-start
        private const string DefaultEmagAnnouncementColor = "#1d8bad";
        private static readonly ProtoId<SoundCollectionPrototype> EmagAnnouncementSounds = "EmagAnnouncementSounds";
        // DS14-end

        public override void Initialize()
        {
            // All events that refresh the BUI
            SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
            SubscribeLocalEvent<RoundEndSystemChangedEvent>(_ => OnGenericBroadcastEvent());
            SubscribeLocalEvent<AlertLevelDelayFinishedEvent>(_ => OnGenericBroadcastEvent());

            // Messages from the BUI
            SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleSelectAlertLevelMessage>(OnSelectAlertLevelMessage);
            SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleAnnounceMessage>(OnAnnounceMessage);
            SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleBroadcastMessage>(OnBroadcastMessage);
            SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleCallEmergencyShuttleMessage>(OnCallShuttleMessage);
            SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleRecallEmergencyShuttleMessage>(OnRecallShuttleMessage);
            // DS14-start
            SubscribeLocalEvent<CommunicationsConsoleComponent, EmagCommunicationsConsoleAnnounceMessage>(OnEmagAnnounceMessage);
            SubscribeLocalEvent<CommunicationsConsoleComponent, EmagCommunicationsConsoleRequestAccessStateMessage>(OnRequestEmagAccessState);
            SubscribeLocalEvent<CommunicationsConsoleComponent, EmagCommunicationsConsoleSetPasswordMessage>(OnSetEmagPassword);
            SubscribeLocalEvent<CommunicationsConsoleComponent, EmagCommunicationsConsoleUnlockMessage>(OnUnlockEmagInterface);
            SubscribeLocalEvent<CommunicationsConsoleComponent, BoundUIOpenedEvent>(OnBoundUiOpened);
            SubscribeLocalEvent<CommunicationsConsoleComponent, BoundUIClosedEvent>(OnBoundUiClosed);

            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestartRound);
            // DS14-end

            // On console init, set cooldown
            SubscribeLocalEvent<CommunicationsConsoleComponent, MapInitEvent>(OnCommunicationsConsoleMapInit);

            SubscribeLocalEvent<CommunicationsConsoleComponent, GotEmaggedEvent>(OnEmagged); // DS14
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<CommunicationsConsoleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                // TODO refresh the UI in a less horrible way
                if (comp.AnnouncementCooldownRemaining >= 0f)
                {
                    comp.AnnouncementCooldownRemaining -= frameTime;
                }

                comp.UIUpdateAccumulator += frameTime;

                if (comp.UIUpdateAccumulator < UIUpdateInterval)
                    continue;

                comp.UIUpdateAccumulator -= UIUpdateInterval;

                if (_uiSystem.IsUiOpen(uid, CommunicationsConsoleUiKey.Key))
                    UpdateCommsConsoleInterface(uid, comp);
            }

            base.Update(frameTime);
        }

        public void OnCommunicationsConsoleMapInit(EntityUid uid, CommunicationsConsoleComponent comp, MapInitEvent args)
        {
            comp.AnnouncementCooldownRemaining = comp.InitialDelay;
            UpdateCommsConsoleInterface(uid, comp);
        }

        /// <summary>
        /// Update the UI of every comms console.
        /// </summary>
        private void OnGenericBroadcastEvent()
        {
            var query = EntityQueryEnumerator<CommunicationsConsoleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                UpdateCommsConsoleInterface(uid, comp);
            }
        }

        /// <summary>
        /// Updates all comms consoles belonging to the station that the alert level was set on
        /// </summary>
        /// <param name="args">Alert level changed event arguments</param>
        private void OnAlertLevelChanged(AlertLevelChangedEvent args)
        {
            var query = EntityQueryEnumerator<CommunicationsConsoleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                var entStation = _stationSystem.GetOwningStation(uid);
                if (args.Station == entStation)
                    UpdateCommsConsoleInterface(uid, comp);
            }
        }

        /// <summary>
        /// Updates the UI for all comms consoles.
        /// </summary>
        public void UpdateCommsConsoleInterface()
        {
            var query = EntityQueryEnumerator<CommunicationsConsoleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                UpdateCommsConsoleInterface(uid, comp);
            }
        }

        /// <summary>
        /// Updates the UI for a particular comms console.
        /// </summary>
        public void UpdateCommsConsoleInterface(EntityUid uid, CommunicationsConsoleComponent comp)
        {
            var stationUid = _stationSystem.GetOwningStation(uid);
            List<string>? levels = null;
            var currentLevel = string.Empty;
            var currentLevelColor = Color.White; // DS14 - send server-only alert prototype color to the client UI.
            float currentDelay = 0;

            if (stationUid != null)
            {
                if (TryComp(stationUid.Value, out AlertLevelComponent? alertComp) &&
                    alertComp.AlertLevels != null)
                {
                    if (alertComp.IsSelectable)
                    {
                        levels = new();
                        foreach (var (id, detail) in alertComp.AlertLevels.Levels)
                        {
                            if (detail.Selectable)
                            {
                                levels.Add(id);
                            }
                        }
                    }

                    currentLevel = alertComp.CurrentLevel;
                    if (alertComp.AlertLevels.Levels.TryGetValue(currentLevel, out var currentLevelDetails))
                        currentLevelColor = currentLevelDetails.Color; // DS14
                    currentDelay = _alertLevelSystem.GetAlertLevelDelay(stationUid.Value, alertComp);
                }
            }
            _uiSystem.SetUiState(uid, CommunicationsConsoleUiKey.Key, new CommunicationsConsoleInterfaceState(
                CanAnnounce(comp),
                CanCallOrRecall(comp),
                levels,
                currentLevel,
                currentLevelColor, // DS14
                currentDelay,
                _roundEndSystem.ExpectedCountdownEnd
            ));

            SendEmagAccessStateToOpenActors(uid, comp); // DS14
        }

        private static bool CanAnnounce(CommunicationsConsoleComponent comp)
        {
            return comp.AnnouncementCooldownRemaining <= 0f;
        }

        private bool CanUse(EntityUid user, EntityUid console)
        {
            if (TryComp<AccessReaderComponent>(console, out var accessReaderComponent))
            {
                return _accessReaderSystem.IsAllowed(user, console, accessReaderComponent);
            }
            return true;
        }

        private bool CanCallOrRecall(CommunicationsConsoleComponent comp)
        {
            //DS14-start
            if (_cfg.GetCVar(CCVars.EvacLocked))
                return false;
            //DS14-end

            // Defer to what the round end system thinks we should be able to do.
            if (_emergency.EmergencyShuttleArrived || !_roundEndSystem.CanCallOrRecall())
                return false;

            // Ensure that we can communicate with the shuttle (either call or recall)
            if (!comp.CanShuttle)
                return false;

            // Calling shuttle checks
            if (_roundEndSystem.ExpectedCountdownEnd is null)
                return true;

            // Recalling shuttle checks
            var recallThreshold = _cfg.GetCVar(CCVars.EmergencyRecallTurningPoint);

            // shouldn't really be happening if we got here
            if (_roundEndSystem.ShuttleTimeLeft is not { } left
                || _roundEndSystem.ExpectedShuttleLength is not { } expected)
                return false;

            return !(left.TotalSeconds / expected.TotalSeconds < recallThreshold);
        }

        private void OnSelectAlertLevelMessage(EntityUid uid, CommunicationsConsoleComponent comp, CommunicationsConsoleSelectAlertLevelMessage message)
        {
            if (message.Actor is not { Valid: true } mob)
                return;

            if (!CanUse(mob, uid))
            {
                _popupSystem.PopupCursor(Loc.GetString("comms-console-permission-denied"), message.Actor, PopupType.Medium);
                return;
            }

            var stationUid = _stationSystem.GetOwningStation(uid);
            if (stationUid != null)
            {
                _alertLevelSystem.SetLevel(stationUid.Value, message.Level, true, true);
            }
        }

        private void OnAnnounceMessage(EntityUid uid, CommunicationsConsoleComponent comp,
            CommunicationsConsoleAnnounceMessage message)
        {
            var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
            var msg = SharedChatSystem.SanitizeAnnouncement(message.Message, maxLength);
            string originalMessage = msg; // DS14-TTS
            var author = Loc.GetString("comms-console-announcement-unknown-sender");
            if (message.Actor is { Valid: true } mob)
            {
                if (!CanAnnounce(comp))
                {
                    return;
                }

                if (!CanUse(mob, uid))
                {
                    _popupSystem.PopupEntity(Loc.GetString("comms-console-permission-denied"), uid, message.Actor);
                    return;
                }

                var tryGetIdentityShortInfoEvent = new TryGetIdentityShortInfoEvent(uid, mob);
                RaiseLocalEvent(tryGetIdentityShortInfoEvent);
                author = tryGetIdentityShortInfoEvent.Title;
            }

            comp.AnnouncementCooldownRemaining = comp.Delay;
            UpdateCommsConsoleInterface(uid, comp);

            var ev = new CommunicationConsoleAnnouncementEvent(uid, comp, msg, message.Actor);
            RaiseLocalEvent(ref ev);

            // allow admemes with vv
            Loc.TryGetString(comp.Title, out var title);
            title ??= comp.Title;

            // DS14-Languages-start
            var languageId = LanguageSystem.DefaultLanguageId;
            var voice = string.Empty;

            if (TryComp<LanguageComponent>(message.Actor, out var languageComponent))
                languageId = languageComponent.SelectedLanguage;

            if (TryComp<TTSComponent>(message.Actor, out var tts))
                voice = tts.VoicePrototypeId;
            // DS14-Languages-end

            if (comp.AnnounceSentBy)
                msg += "\n" + Loc.GetString("comms-console-announcement-sent-by") + " " + author;

            if (comp.Global)
            {
                _chatSystem.DispatchGlobalAnnouncement(msg, title, announcementSound: comp.Sound, colorOverride: comp.Color, originalMessage: originalMessage, author: message.Actor, languageId: languageId); // DS14-TTS

                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(message.Actor):player} has sent the following global announcement: {msg}");
                return;
            }

            _chatSystem.DispatchStationAnnouncement(uid,
                msg,
                title,
                announcementSound:
                comp.Sound,
                colorOverride:
                comp.Color,
                voice: voice,
                languageId: languageId);

            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(message.Actor):player} has sent the following station announcement: {msg}");

        }

        private void OnBroadcastMessage(EntityUid uid, CommunicationsConsoleComponent component, CommunicationsConsoleBroadcastMessage message)
        {
            // DS14-start
            var maxLength = Math.Max(0, _cfg.GetCVar(CCCCVars.MaxBroadcastLength));
            var msg = SharedChatSystem.SanitizeAnnouncement(message.Message ?? string.Empty, maxLength);
            if (msg.Length > maxLength)
                msg = msg[..maxLength];
            // DS14-end

            if (!TryComp<DeviceNetworkComponent>(uid, out var net))
                return;

            var payload = new NetworkPayload
            {
                [ScreenMasks.Text] = msg //DS14
            };

            _deviceNetworkSystem.QueuePacket(uid, null, payload, net.TransmitFrequency);

            _adminLogger.Add(LogType.DeviceNetwork, LogImpact.Low, $"{ToPrettyString(message.Actor):player} has sent the following broadcast: {msg:msg}"); //DS14
        }

        private void OnCallShuttleMessage(EntityUid uid, CommunicationsConsoleComponent comp, CommunicationsConsoleCallEmergencyShuttleMessage message)
        {
            if (!CanCallOrRecall(comp))
                return;

            var mob = message.Actor;

            if (!CanUse(mob, uid))
            {
                _popupSystem.PopupEntity(Loc.GetString("comms-console-permission-denied"), uid, message.Actor);
                return;
            }

            var ev = new CommunicationConsoleCallShuttleAttemptEvent(uid, comp, mob);
            RaiseLocalEvent(ref ev);
            if (ev.Cancelled)
            {
                _popupSystem.PopupEntity(ev.Reason ?? Loc.GetString("comms-console-shuttle-unavailable"), uid, message.Actor);
                return;
            }

            _roundEndSystem.RequestRoundEnd(mob, uid);
            _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(mob):player} has called the shuttle.");
        }

        private void OnRecallShuttleMessage(EntityUid uid, CommunicationsConsoleComponent comp, CommunicationsConsoleRecallEmergencyShuttleMessage message)
        {
            if (!CanCallOrRecall(comp))
                return;

            var mob = message.Actor;

            if (!CanUse(mob, uid))
            {
                _popupSystem.PopupEntity(Loc.GetString("comms-console-permission-denied"), uid, message.Actor);
                return;
            }

            _roundEndSystem.CancelRoundEndCountdown(mob, uid);
            _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(message.Actor):player} has recalled the shuttle.");
        }
        // DS14-start
        private void OnRestartRound(RoundRestartCleanupEvent ev)
        {
            _cfg.SetCVar(CCVars.EvacLocked, false);
        }

        private void OnBoundUiOpened(EntityUid uid, CommunicationsConsoleComponent component, BoundUIOpenedEvent args)
        {
            if (!args.UiKey.Equals(CommunicationsConsoleUiKey.Key))
                return;

            SendEmagAccessState(uid, component, args.Actor);
        }

        private void OnBoundUiClosed(EntityUid uid, CommunicationsConsoleComponent component, BoundUIClosedEvent args)
        {
            if (!args.UiKey.Equals(CommunicationsConsoleUiKey.Key))
                return;

            component.AuthorizedEmagActors.Remove(args.Actor);
        }

        private void OnRequestEmagAccessState(
            EntityUid uid,
            CommunicationsConsoleComponent component,
            EmagCommunicationsConsoleRequestAccessStateMessage message)
        {
            var actor = message.Actor;
            if (!actor.Valid || !_uiSystem.IsUiOpen(uid, CommunicationsConsoleUiKey.Key, actor))
                return;

            SendEmagAccessState(uid, component, actor);
        }

        public void OnEmagged(EntityUid uid, CommunicationsConsoleComponent component, ref GotEmaggedEvent args)
        {
            if (!_emag.CompareFlag(args.Type, EmagType.Interaction) ||
                _emag.CheckFlag(uid, EmagType.Interaction))
            {
                return;
            }

            args.Handled = true;

            foreach (var actor in _uiSystem.GetActors(uid, CommunicationsConsoleUiKey.Key))
            {
                SendEmagAccessState(
                    uid,
                    component,
                    actor,
                    EmagCommunicationsUiMode.PasswordSetup);
            }
        }

        private void OnSetEmagPassword(
            EntityUid uid,
            CommunicationsConsoleComponent component,
            EmagCommunicationsConsoleSetPasswordMessage message)
        {
            var actor = message.Actor;
            if (!actor.Valid || !_uiSystem.IsUiOpen(uid, CommunicationsConsoleUiKey.Key, actor))
                return;

            if (!_emag.CheckFlag(uid, EmagType.Interaction))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.Unavailable);
                return;
            }

            if (component.EmagPassword != null)
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.PasswordAlreadySet);
                return;
            }

            if (!IsValidEmagPassword(message.Password))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidPasswordFormat);
                return;
            }

            component.EmagPassword = message.Password;
            component.AuthorizedEmagActors.Add(actor);
            SendEmagAccessStateToOpenActors(uid, component);
        }

        private void OnUnlockEmagInterface(
            EntityUid uid,
            CommunicationsConsoleComponent component,
            EmagCommunicationsConsoleUnlockMessage message)
        {
            var actor = message.Actor;
            if (!actor.Valid || !_uiSystem.IsUiOpen(uid, CommunicationsConsoleUiKey.Key, actor))
                return;

            if (!_emag.CheckFlag(uid, EmagType.Interaction))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.Unavailable);
                return;
            }

            if (component.EmagPassword == null)
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidRequest);
                return;
            }

            if (!IsValidEmagPassword(message.Password))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidPasswordFormat);
                return;
            }

            if (!string.Equals(component.EmagPassword, message.Password, StringComparison.Ordinal))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.IncorrectPassword);
                return;
            }

            component.AuthorizedEmagActors.Add(actor);
            SendEmagAccessState(uid, component, actor);
        }

        private void OnEmagAnnounceMessage(
            EntityUid uid,
            CommunicationsConsoleComponent component,
            EmagCommunicationsConsoleAnnounceMessage message)
        {
            var actor = message.Actor;
            if (!actor.Valid || !_uiSystem.IsUiOpen(uid, CommunicationsConsoleUiKey.Key, actor))
                return;

            if (!_emag.CheckFlag(uid, EmagType.Interaction))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.Unavailable);
                return;
            }

            if (!component.AuthorizedEmagActors.Contains(actor))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidRequest);
                return;
            }

            if (!CanAnnounce(component))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.Cooldown);
                return;
            }

            if (string.IsNullOrWhiteSpace(message.Announcement))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidAnnouncement);
                return;
            }

            var announcement = SharedChatSystem.SanitizeAnnouncement(
                message.Announcement,
                _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength));

            if (string.IsNullOrWhiteSpace(announcement))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidAnnouncement);
                return;
            }

            if (!_prototypeManager.HasIndex<LanguagePrototype>(message.LanguageId))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidLanguage);
                return;
            }

            if (!TryGetEmagAnnouncementSound(message.SoundPath, out var sound, out var soundPath))
            {
                SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidSound);
                return;
            }

            string? voice = null;
            var usePresetTts = false;
            if (message.EnableTts)
            {
                if (message.UseCustomTts)
                {
                    voice = message.VoiceId?.Trim();
                    if (string.IsNullOrEmpty(voice) || !_prototypeManager.HasIndex<TTSVoicePrototype>(voice))
                    {
                        SendEmagAccessState(uid, component, actor, error: EmagCommunicationsUiError.InvalidVoice);
                        return;
                    }
                }
                else
                {
                    usePresetTts = true;
                }
            }

            var announcer = NormalizeAttribution(message.Announcer);
            if (string.IsNullOrWhiteSpace(announcer))
                announcer = Loc.GetString("chat-manager-sender-announcement");
            var escapedAnnouncer = FormattedMessage.EscapeText(announcer);

            var signature = NormalizeAttribution(message.Signature);
            var announcementWithSignature = GetAnnouncementWithSignature(announcement, signature);
            var (hex, color) = GetEmagAnnouncementColor(message.ColorHex);

            component.AnnouncementCooldownRemaining = component.Delay;
            UpdateCommsConsoleInterface(uid, component);

            _chatSystem.DispatchStationAnnouncement(
                uid,
                announcementWithSignature,
                escapedAnnouncer,
                playDefaultSound: true,
                announcementSound: sound,
                colorOverride: color,
                voice: voice,
                languageId: message.LanguageId,
                usePresetTTS: usePresetTts);

            _adminLogger.Add(
                LogType.Chat,
                LogImpact.Low,
                $"{ToPrettyString(actor):player} sent an emag announcement " +
                $"[color={hex}] [sound={soundPath}] [announcer=\"{announcer}\"] " +
                $"[signature=\"{signature}\"]: {announcement}");
        }

        private void SendEmagAccessStateToOpenActors(EntityUid uid, CommunicationsConsoleComponent component)
        {
            foreach (var actor in _uiSystem.GetActors(uid, CommunicationsConsoleUiKey.Key))
            {
                SendEmagAccessState(uid, component, actor);
            }
        }

        private void SendEmagAccessState(
            EntityUid uid,
            CommunicationsConsoleComponent component,
            EntityUid actor,
            EmagCommunicationsUiMode? mode = null,
            EmagCommunicationsUiError error = EmagCommunicationsUiError.None)
        {
            mode ??= GetEmagUiMode(uid, component, actor);
            _uiSystem.ServerSendUiMessage(
                uid,
                CommunicationsConsoleUiKey.Key,
                new EmagCommunicationsConsoleAccessStateMessage(mode.Value, error, CanAnnounce(component)),
                actor);
        }

        private EmagCommunicationsUiMode GetEmagUiMode(
            EntityUid uid,
            CommunicationsConsoleComponent component,
            EntityUid actor)
        {
            if (!_emag.CheckFlag(uid, EmagType.Interaction))
                return EmagCommunicationsUiMode.Unavailable;

            if (component.EmagPassword == null)
                return EmagCommunicationsUiMode.PasswordSetup;

            return component.AuthorizedEmagActors.Contains(actor)
                ? EmagCommunicationsUiMode.Authorized
                : EmagCommunicationsUiMode.Locked;
        }

        private static bool IsValidEmagPassword(string? password)
        {
            return !string.IsNullOrWhiteSpace(password) &&
                   password.Length <= EmagCommunicationsConsoleConstants.MaxPasswordLength &&
                   !password.Any(char.IsControl);
        }

        private static string NormalizeAttribution(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
            return normalized.Length <= EmagCommunicationsConsoleConstants.MaxAttributionLength
                ? normalized
                : normalized[..EmagCommunicationsConsoleConstants.MaxAttributionLength];
        }

        private static (string Hex, Color Color) GetEmagAnnouncementColor(string? colorHex)
        {
            var hex = colorHex?.Trim();
            if (string.IsNullOrWhiteSpace(hex))
                hex = DefaultEmagAnnouncementColor;
            else if (!hex.StartsWith('#'))
                hex = $"#{hex}";

            return Color.TryFromHex(hex) is { } color
                ? (hex, color)
                : (DefaultEmagAnnouncementColor, Color.FromHex(DefaultEmagAnnouncementColor));
        }

        private bool TryGetEmagAnnouncementSound(
            string? requestedPath,
            out SoundSpecifier sound,
            out string soundPath)
        {
            sound = default!;
            soundPath = requestedPath?.Trim() ?? string.Empty;
            var normalizedPath = soundPath;

            if (!_prototypeManager.TryIndex(EmagAnnouncementSounds, out var collection) ||
                !collection.PickFiles.Any(path => string.Equals(path.ToString(), normalizedPath, StringComparison.Ordinal)))
            {
                return false;
            }

            sound = new SoundPathSpecifier(soundPath);
            return true;
        }

        private string GetAnnouncementWithSignature(string announcement, string signature)
        {
            if (string.IsNullOrEmpty(signature))
                return announcement;

            return $"{announcement}\n{Loc.GetString("comms-console-announcement-sent-by")} {signature}";
        }
        public void ToggleLockEvac()
        {
            _cfg.SetCVar(CCVars.EvacLocked, !_cfg.GetCVar(CCVars.EvacLocked));
            UpdateCommsConsoleInterface();
        }
        // DS14-end
    }

    /// <summary>
    /// Raised on announcement
    /// </summary>
    [ByRefEvent]
    public record struct CommunicationConsoleAnnouncementEvent(EntityUid Uid, CommunicationsConsoleComponent Component, string Text, EntityUid? Sender)
    {
        public EntityUid Uid = Uid;
        public CommunicationsConsoleComponent Component = Component;
        public EntityUid? Sender = Sender;
        public string Text = Text;
    }

    /// <summary>
    /// Raised on shuttle call attempt. Can be cancelled
    /// </summary>
    [ByRefEvent]
    public record struct CommunicationConsoleCallShuttleAttemptEvent(EntityUid Uid, CommunicationsConsoleComponent Component, EntityUid? Sender)
    {
        public bool Cancelled = false;
        public EntityUid Uid = Uid;
        public CommunicationsConsoleComponent Component = Component;
        public EntityUid? Sender = Sender;
        public string? Reason;
    }
}
