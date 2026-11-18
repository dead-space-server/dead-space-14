// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.CentCommFax;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.CentCommFax;

public sealed partial class CentCommFaxSystem : EntitySystem
{
    public const string UplinkAddress = "centcomm-uplink";

    private const int MaxInbox = 200;
    private static readonly SoundSpecifier NotifySound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FaxSystem _fax = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private StationSystem _station = default!;

    private readonly List<CentCommFaxEntry> _inbox = new();
    private int _nextId;

    public event Action? InboxChanged;

    public IReadOnlyList<CentCommFaxEntry> Inbox => _inbox;

    public bool UplinkActive => !_cfg.GetCVar(CCCCVars.CentCommEnabled);

    public string UplinkName => Loc.GetString("centcomm-fax-uplink-name");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());
    }

    private void Reset()
    {
        _inbox.Clear();
        InboxChanged?.Invoke();
    }

    public void AddUplink(FaxMachineComponent fax)
    {
        if (UplinkActive)
            fax.KnownFaxes[UplinkAddress] = UplinkName;
    }

    public void Record(EntityUid? senderFax, string senderName, FaxPrintout printout, bool notify)
    {
        var entry = new CentCommFaxEntry
        {
            Id = ++_nextId,
            Time = _ticker.RoundDuration().ToString(@"hh\:mm\:ss"),
            Sender = senderName,
            Station = GetStationName(senderFax),
            SenderFax = senderFax is { } fax ? GetNetEntity(fax) : null,
            Title = printout.Name,
            Content = printout.Content,
            Stamps = new List<StampDisplayInfo>(printout.StampedBy),
            Signatures = new List<string>(printout.Signatures ?? new List<string>()),
        };

        _inbox.Add(entry);
        if (_inbox.Count > MaxInbox)
            _inbox.RemoveAt(0);

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Low,
            $"CentComm fax #{entry.Id} from \"{senderName}\": {printout.Name}: {printout.Content}");

        if (notify)
        {
            _chat.SendAdminAnnouncement(Loc.GetString("centcomm-fax-admin-notify",
                ("fax", senderName),
                ("title", printout.Name)));
            _audio.PlayGlobal(NotifySound, Filter.Empty().AddPlayers(_admins.ActiveAdmins), false, AudioParams.Default.WithVolume(-8f));
        }

        InboxChanged?.Invoke();
    }

    public EntityUid? FindFax(string? address)
    {
        if (address == null)
            return null;

        var query = EntityQueryEnumerator<FaxMachineComponent, DeviceNetworkComponent>();
        while (query.MoveNext(out var uid, out _, out var device))
        {
            if (device.Address == address)
                return uid;
        }

        return null;
    }

    public bool Reply(EntityUid target, FaxPrintout printout, int replyTo, ICommonSession admin)
    {
        if (!TryComp<FaxMachineComponent>(target, out var fax))
            return false;

        fax.KnownFaxes[UplinkAddress] = UplinkName;
        _fax.Receive(target, printout, UplinkAddress, fax);

        foreach (var entry in _inbox)
        {
            if (entry.Id == replyTo)
                entry.Replies++;
        }

        _adminLogger.Add(LogType.AdminMessage, LogImpact.Low,
            $"{admin:player} replied from CentComm to \"{fax.FaxName}\" {ToPrettyString(target):fax}: {printout.Name}: {printout.Content}");

        InboxChanged?.Invoke();
        return true;
    }

    public string GetStationName(EntityUid? fax)
    {
        if (fax is not { } uid || _station.GetOwningStation(uid) is not { } station)
            return string.Empty;

        return Name(station);
    }
}
