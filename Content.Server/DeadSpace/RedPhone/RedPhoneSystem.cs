// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Server.Telephone;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.DeadSpace.RedPhone;
using Content.Shared.Popups;
using Content.Shared.Telephone;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.RedPhone;

public sealed partial class RedPhoneSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier AdminNotificationSound = new SoundPathSpecifier("/Audio/_DeadSpace/Misc/pew-connor.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RedPhoneComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<RedPhoneComponent, RedPhoneStartCallMessage>(OnStartCall);
        SubscribeLocalEvent<RedPhoneComponent, RedPhoneAnswerCallMessage>(OnAnswerCall);
        SubscribeLocalEvent<RedPhoneComponent, RedPhoneEndCallMessage>(OnEndCall);
        SubscribeLocalEvent<RedPhoneComponent, RedPhoneSubmitReportMessage>(OnSubmitReport);
        SubscribeLocalEvent<RedPhoneComponent, TelephoneCallEndedEvent>(OnCallEnded);
        SubscribeLocalEvent<RedPhoneComponent, TelephoneStateChangeEvent>(OnTelephoneStateChanged);
        SubscribeLocalEvent<RedPhoneComponent, ComponentShutdown>(OnComponentShutdown);
    }

    public bool TrySubmitReport(EntityUid phoneUid, EntityUid user, ICommonSession session, string message)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            !TryComp<RedPhoneComponent>(phoneUid, out var redPhone) ||
            redPhone.Kind != RedPhoneKind.Ordinary ||
            !TryComp<TelephoneComponent>(phoneUid, out var telephone))
        {
            return false;
        }

        message = message.Trim();

        if (_timing.CurTime < redPhone.NextReportTime)
        {
            _popup.PopupEntity(Loc.GetString("red-phone-popup-cooldown"), phoneUid, user, PopupType.Medium);
            return false;
        }

        redPhone.NextReportTime = _timing.CurTime + redPhone.ReportCooldown;

        var callerInfo = _telephone.GetNameAndJobOfCallingEntity(user);
        redPhone.LastReportedCallerName = callerInfo.Item1 ?? Name(user);
        redPhone.LastReportedCallerJob = callerInfo.Item2;
        redPhone.LastReportedDeviceName = GetDeviceDisplayName(phoneUid);
        redPhone.LastReportTime = _timing.CurTime;
        redPhone.HasReportedThisRound = true;

        _popup.PopupEntity(Loc.GetString("prayer-popup-notify-centcom-sent"), phoneUid, session, PopupType.Medium);
        _chat.SendAdminAnnouncement($"{Loc.GetString("prayer-chat-notify-centcom")} <{session.Name}>: {message}");
        _audio.PlayGlobal(AdminNotificationSound, Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false, AudioParams.Default.WithVolume(-6f));
        _adminLogger.Add(LogType.AdminMessage, LogImpact.Low, $"{ToPrettyString(user):player} reported through red phone {ToPrettyString(phoneUid)}: {message}");

        ReplayReportOnCentCommPhones(user, message);
        RefreshCentCommPhoneUis();
        return true;
    }

    private void OnCallEnded(Entity<RedPhoneComponent> ent, ref TelephoneCallEndedEvent args)
    {
        if (ent.Comp.Kind != RedPhoneKind.Ordinary)
            return;

        _popup.PopupEntity(Loc.GetString("red-phone-emote-call-ended"), ent.Owner, PopupType.Medium);
    }

    private void ReplayReportOnCentCommPhones(EntityUid messageSource, string message)
    {
        var query = EntityQueryEnumerator<RedPhoneComponent, TelephoneComponent>();
        while (query.MoveNext(out var uid, out var redPhone, out var telephone))
        {
            if (redPhone.Kind != RedPhoneKind.CentComm)
                continue;

            var receiver = (uid, telephone);
            if (!_telephone.IsTelephonePowered(receiver))
                continue;

            _telephone.RelayTelephoneSpeech(messageSource, message, receiver, rangeOverride: ChatTransmitRange.GhostRangeLimit);
        }
    }

    private void OnBeforeUiOpen(Entity<RedPhoneComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        if (ent.Comp.Kind != RedPhoneKind.CentComm)
            return;

        UpdateUserInterface(ent.Owner);
    }

    private void OnStartCall(Entity<RedPhoneComponent> ent, ref RedPhoneStartCallMessage args)
    {
        if (args.Actor == EntityUid.Invalid ||
            !TryGetEntity(args.Receiver, out var receiverUidNet) ||
            receiverUidNet == null)
        {
            return;
        }

        TryStartCall(ent.Owner, receiverUidNet.Value, args.Actor);
    }

    private void OnAnswerCall(Entity<RedPhoneComponent> ent, ref RedPhoneAnswerCallMessage args)
    {
        if (ent.Comp.Kind != RedPhoneKind.Ordinary ||
            args.Actor == EntityUid.Invalid ||
            !TryComp<TelephoneComponent>(ent.Owner, out var telephone))
        {
            return;
        }

        _telephone.AnswerTelephone((ent.Owner, telephone), args.Actor);
        RefreshCentCommPhoneUis();
    }

    private void OnSubmitReport(Entity<RedPhoneComponent> ent, ref RedPhoneSubmitReportMessage args)
    {
        if (ent.Comp.Kind != RedPhoneKind.Ordinary ||
            args.Actor == EntityUid.Invalid ||
            !TryComp<ActorComponent>(args.Actor, out var actor))
        {
            return;
        }

        if (TrySubmitReport(ent.Owner, args.Actor, actor.PlayerSession, args.Message))
            _ui.CloseUi(ent.Owner, RedPhoneUiKey.Report, args.Actor);
    }

    private void OnEndCall(Entity<RedPhoneComponent> ent, ref RedPhoneEndCallMessage args)
    {
        if (args.Actor == EntityUid.Invalid ||
            !TryComp<TelephoneComponent>(ent.Owner, out var telephone))
        {
            return;
        }

        if (ent.Comp.Kind == RedPhoneKind.CentComm &&
            !HasCentCommAccess(args.Actor, ent.Owner))
        {
            return;
        }

        _telephone.EndTelephoneCalls((ent.Owner, telephone));
        RefreshCentCommPhoneUis();
    }

    public bool TryStartCall(EntityUid centCommPhoneUid, EntityUid ordinaryPhoneUid, EntityUid user)
    {
        if (!TryComp<RedPhoneComponent>(centCommPhoneUid, out var centCommPhone) ||
            centCommPhone.Kind != RedPhoneKind.CentComm ||
            !TryComp<TelephoneComponent>(centCommPhoneUid, out var callerTelephone) ||
            !HasCentCommAccess(user, centCommPhoneUid) ||
            !TryComp<RedPhoneComponent>(ordinaryPhoneUid, out var receiverRedPhone) ||
            receiverRedPhone.Kind != RedPhoneKind.Ordinary ||
            !receiverRedPhone.HasReportedThisRound ||
            !TryComp<TelephoneComponent>(ordinaryPhoneUid, out var receiverTelephone))
        {
            return false;
        }

        var receiver = (ordinaryPhoneUid, receiverTelephone);
        var caller = (centCommPhoneUid, callerTelephone);

        if (!_telephone.IsTelephonePowered(receiver) ||
            (_telephone.IsTelephoneEngaged(receiver) && !_telephone.IsSourceConnectedToReceiver(receiver, caller)) ||
            _telephone.IsTelephoneEngaged(caller))
        {
            _popup.PopupEntity(Loc.GetString("red-phone-popup-unavailable"), centCommPhoneUid, user, PopupType.Medium);
            return false;
        }

        _telephone.CallTelephone(
            caller,
            receiver,
            user,
            new TelephoneCallOptions
            {
                IgnoreRange = true
            });

        RefreshCentCommPhoneUis();
        return true;
    }

    private void OnTelephoneStateChanged(Entity<RedPhoneComponent> ent, ref TelephoneStateChangeEvent args)
    {
        if (ent.Comp.Kind == RedPhoneKind.Ordinary &&
            args.NewState == TelephoneState.Ringing)
        {
            _popup.PopupEntity(Loc.GetString("red-phone-emote-incoming-call"), ent.Owner, PopupType.Medium);
        }

        RefreshCentCommPhoneUis();
    }

    private void OnComponentShutdown(Entity<RedPhoneComponent> ent, ref ComponentShutdown args)
    {
        RefreshCentCommPhoneUis();
    }

    private bool HasCentCommAccess(EntityUid user, EntityUid phoneUid)
    {
        return _access.IsAllowed(user, phoneUid);
    }

    private void RefreshCentCommPhoneUis()
    {
        var query = EntityQueryEnumerator<RedPhoneComponent>();
        while (query.MoveNext(out var uid, out var redPhone))
        {
            if (redPhone.Kind != RedPhoneKind.CentComm)
                continue;

            UpdateUserInterface(uid);
        }
    }

    private void UpdateUserInterface(EntityUid phoneUid)
    {
        var state = GetUiState(phoneUid);
        if (state == null ||
            !TryComp<UserInterfaceComponent>(phoneUid, out var uiComp) ||
            !_ui.HasUi(phoneUid, RedPhoneUiKey.Key))
        {
            return;
        }

        _ui.SetUiState((phoneUid, uiComp), RedPhoneUiKey.Key, state);
    }

    public RedPhoneBoundUiState? GetUiState(EntityUid phoneUid)
    {
        if (!TryComp<RedPhoneComponent>(phoneUid, out var redPhone) ||
            redPhone.Kind != RedPhoneKind.CentComm ||
            !TryComp<TelephoneComponent>(phoneUid, out var telephone))
        {
            return null;
        }

        return BuildUiState(phoneUid, telephone);
    }

    private RedPhoneBoundUiState BuildUiState(EntityUid phoneUid, TelephoneComponent telephone)
    {
        var contacts = new List<RedPhoneContactEntry>();
        var candidates = new List<(EntityUid Uid, RedPhoneComponent RedPhone, TelephoneComponent Telephone)>();

        var query = EntityQueryEnumerator<RedPhoneComponent, TelephoneComponent>();
        while (query.MoveNext(out var uid, out var redPhone, out var receiverTelephone))
        {
            if (redPhone.Kind != RedPhoneKind.Ordinary || !redPhone.HasReportedThisRound)
                continue;

            candidates.Add((uid, redPhone, receiverTelephone));
        }

        candidates.Sort(static (left, right) => right.RedPhone.LastReportTime.CompareTo(left.RedPhone.LastReportTime));
        NetEntity? activePhone = null;

        foreach (var candidate in candidates)
        {
            var candidateTelephone = (candidate.Uid, candidate.Telephone);
            var isActiveTarget = _telephone.IsSourceConnectedToReceiver(candidateTelephone, (phoneUid, telephone));
            var available = _telephone.IsTelephonePowered((candidate.Uid, candidate.Telephone)) &&
                            (!_telephone.IsTelephoneEngaged(candidateTelephone) || isActiveTarget);

            contacts.Add(new RedPhoneContactEntry(
                GetNetEntity(candidate.Uid),
                GetContactLabel(candidate.RedPhone),
                available));

            if (isActiveTarget && activePhone == null)
                activePhone = GetNetEntity(candidate.Uid);
        }

        return new RedPhoneBoundUiState(contacts, telephone.CurrentState, activePhone);
    }

    private string GetContactLabel(RedPhoneComponent redPhone)
    {
        var name = redPhone.LastReportedCallerName;
        var job = redPhone.LastReportedCallerJob;
        var device = redPhone.LastReportedDeviceName;

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(job) && !string.IsNullOrWhiteSpace(device))
        {
            return Loc.GetString("red-phone-contact-label-full",
                ("name", name),
                ("job", job),
                ("device", device));
        }

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(job))
            return Loc.GetString("red-phone-contact-label-name-job", ("name", name), ("job", job));

        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(device))
            return Loc.GetString("red-phone-contact-label-name-device", ("name", name), ("device", device));

        if (!string.IsNullOrWhiteSpace(name))
            return name;

        if (!string.IsNullOrWhiteSpace(device))
            return Loc.GetString("red-phone-contact-label-device-only", ("device", device));

        return Loc.GetString("red-phone-contact-label-unknown");
    }

    private string GetDeviceDisplayName(EntityUid phoneUid)
    {
        return _telephone.GetTelephoneDeviceName(phoneUid) ?? Name(phoneUid);
    }
}
