// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Silicons.StationAi;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.StationAI.Components;
using Content.Shared.DeadSpace.StationAI.UI;
using Content.Shared.Fax.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.StationAI.Systems;

public sealed partial class StationAiCentCommFaxSystem : EntitySystem
{
    private const string CentCommFaxPrototype = "FaxMachineCentcom";
    private const string SourceAddress = "station-ai-centcomm-uplink";
    private const string PaperPrototype = "PaperPrintedCentcomm";
    private const string StampState = "paper_stamp-centcom";

    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private FaxSystem _fax = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StationAiSystem _stationAi = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiCentCommFaxComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<StationAiCentCommFaxComponent, StationAiCentCommFaxSendMessage>(OnSendMessage);
    }

    private void OnUiOpened(Entity<StationAiCentCommFaxComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, StationAiCentCommFaxUiKey.Key))
            return;

        UpdateUi(ent);
    }

    private void OnSendMessage(Entity<StationAiCentCommFaxComponent> ent, ref StationAiCentCommFaxSendMessage args)
    {
        if (args.Actor != ent.Owner || !HasComp<StationAiHeldComponent>(ent.Owner))
            return;

        var content = args.Content.Trim();

        if (content.Length > StationAiCentCommFaxConstants.MaxContentLength)
            content = content[..StationAiCentCommFaxConstants.MaxContentLength];

        if (content.Length == 0)
        {
            UpdateUi(ent, Loc.GetString("station-ai-centcomm-fax-status-empty"));
            return;
        }

        if (TryGetCooldown(ent.Comp, out var remaining))
        {
            var status = Loc.GetString("station-ai-centcomm-fax-status-cooldown",
                ("seconds", GetRemainingSeconds(remaining)));
            UpdateUi(ent, status);
            PopupToAi(ent.Owner, status);
            return;
        }

        if (!TryGetCentCommFax(out var fax))
        {
            var status = Loc.GetString("station-ai-centcomm-fax-status-unavailable");
            UpdateUi(ent, status);
            PopupToAi(ent.Owner, status);
            return;
        }

        var sender = Name(ent.Owner);
        var stationName = GetStationName(ent.Owner);
        var shiftTime = _timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan).ToString(@"hh\:mm\:ss");
        var date = DateTime.UtcNow.AddHours(3).ToString("dd.MM") + ".2710";
        var document = Loc.GetString("station-ai-centcomm-fax-document",
            ("sender", sender),
            ("station", stationName),
            ("time", shiftTime),
            ("date", date),
            ("content", FormattedMessage.EscapeText(content)));

        var stamps = new List<StampDisplayInfo>
        {
            new()
            {
                StampedName = Loc.GetString("station-ai-centcomm-fax-stamp"),
                StampedColor = Color.FromHex("#2da7c9"),
            },
        };

        fax.Comp.KnownFaxes[SourceAddress] = Loc.GetString("station-ai-centcomm-fax-source-name", ("name", sender));

        var printout = new FaxPrintout(
            document,
            Loc.GetString("station-ai-centcomm-fax-document-name"),
            null,
            PaperPrototype,
            StampState,
            stamps,
            locked: true);

        _fax.Receive(fax.Owner, printout, SourceAddress, fax.Comp);

        ent.Comp.NextTransmissionTime = _timing.CurTime + ent.Comp.Cooldown;

        _adminLogger.Add(LogType.AdminMessage,
            LogImpact.Low,
            $"{ToPrettyString(ent.Owner):player} sent a station AI fax to CentComm: {content}");

        UpdateUi(ent, Loc.GetString("station-ai-centcomm-fax-status-sent"), clearForm: true);
    }

    private bool TryGetCentCommFax(out Entity<FaxMachineComponent> target)
    {
        var query = EntityQueryEnumerator<FaxMachineComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var fax, out var meta))
        {
            if (meta.EntityPrototype?.ID != CentCommFaxPrototype)
                continue;

            target = (uid, fax);
            return true;
        }

        target = default;
        return false;
    }

    private string GetStationName(EntityUid ai)
    {
        if (!_stationAi.TryGetCore(ai, out var core))
            return Loc.GetString("station-ai-centcomm-fax-station-unknown");

        var station = _station.GetOwningStation(core.Owner);
        return station == null
            ? Loc.GetString("station-ai-centcomm-fax-station-unknown")
            : Name(station.Value);
    }

    private void UpdateUi(
        Entity<StationAiCentCommFaxComponent> ent,
        string? statusOverride = null,
        bool clearForm = false)
    {
        var remainingSeconds = TryGetCooldown(ent.Comp, out var remaining)
            ? GetRemainingSeconds(remaining)
            : 0;
        var status = statusOverride ?? GetDefaultStatus(remainingSeconds);
        var state = new StationAiCentCommFaxUiState(
            remainingSeconds == 0,
            remainingSeconds,
            status,
            clearForm);

        _ui.SetUiState(ent.Owner, StationAiCentCommFaxUiKey.Key, state);
    }

    private string GetDefaultStatus(int remainingSeconds)
    {
        if (remainingSeconds > 0)
            return Loc.GetString("station-ai-centcomm-fax-status-cooldown", ("seconds", remainingSeconds));

        return Loc.GetString("station-ai-centcomm-fax-status-ready");
    }

    private bool TryGetCooldown(StationAiCentCommFaxComponent component, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (_timing.CurTime >= component.NextTransmissionTime)
            return false;

        remaining = component.NextTransmissionTime - _timing.CurTime;
        return true;
    }

    private static int GetRemainingSeconds(TimeSpan remaining)
    {
        return Math.Max(1, (int) Math.Ceiling(remaining.TotalSeconds));
    }

    private void PopupToAi(EntityUid ai, string message)
    {
        if (TryComp(ai, out ActorComponent? actor))
        {
            _chat.DispatchServerMessage(actor.PlayerSession, message);
            return;
        }

        _popup.PopupEntity(message, ai, PopupType.MediumCaution);
    }
}
