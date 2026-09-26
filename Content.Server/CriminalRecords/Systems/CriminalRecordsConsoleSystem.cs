using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.CriminalRecords.Systems;
using Content.Shared.DeadSpace.Photocopier; // DS14
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Robust.Shared.Audio.Systems; // DS14
using Robust.Server.GameObjects;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.IdentityManagement;
using Content.Shared.Security.Components;
using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Roles.Jobs;
using Content.Server.GameTicking; // DS14
using Content.Shared.Paper; // DS14
using Robust.Shared.ContentPack; // DS14
using Robust.Shared.Prototypes; // DS14
using Robust.Shared.Timing; // DS14

namespace Content.Server.CriminalRecords.Systems;

/// <summary>
/// Handles all UI for criminal records console
/// </summary>
public sealed class CriminalRecordsConsoleSystem : SharedCriminalRecordsConsoleSystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!; // DS14
    [Dependency] private readonly CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!; // DS14
    [Dependency] private readonly SharedIdCardSystem _idCard = default!; // DS14
    [Dependency] private readonly PaperSystem _paperSystem = default!; // DS14
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!; // DS14
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!; // DS14
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // DS14
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, RecordModifiedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, AfterGeneralRecordCreatedEvent>(UpdateUserInterface);

        Subs.BuiEvents<CriminalRecordsConsoleComponent>(CriminalRecordsConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            subs.Event<CriminalRecordChangeStatus>(OnChangeStatus);
            subs.Event<CriminalRecordAddHistory>(OnAddHistory);
            subs.Event<CriminalRecordDeleteHistory>(OnDeleteHistory);
            subs.Event<CriminalRecordSetStatusFilter>(OnStatusFilterPressed);
            subs.Event<CriminalRecordPrintDocument>(OnPrintDocument); // DS14
        });
    }

    private void UpdateUserInterface<T>(Entity<CriminalRecordsConsoleComponent> ent, ref T args)
    {
        // TODO: this is probably wasteful, maybe better to send a message to modify the exact state?
        UpdateUserInterface(ent);
    }

    private void OnKeySelected(Entity<CriminalRecordsConsoleComponent> ent, ref SelectStationRecord msg)
    {
        // no concern of sus client since record retrieval will fail if invalid id is given
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(ent);
    }
    private void OnStatusFilterPressed(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordSetStatusFilter msg)
    {
        ent.Comp.FilterStatus = msg.FilterStatus;
        UpdateUserInterface(ent);
    }

    private void OnFiltersChanged(Entity<CriminalRecordsConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null ||
            ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent);
        }
    }

    private void GetOfficer(EntityUid uid, out string officer)
    {
        var tryGetIdentityShortInfoEvent = new TryGetIdentityShortInfoEvent(null, uid);
        RaiseLocalEvent(tryGetIdentityShortInfoEvent);
        officer = tryGetIdentityShortInfoEvent.Title ?? Loc.GetString("criminal-records-console-unknown-officer");
    }

    private void OnChangeStatus(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordChangeStatus msg)
    {
        // prevent malf client violating wanted/reason nullability
        if (msg.Status == SecurityStatus.Wanted != (msg.Reason != null) &&
            msg.Status == SecurityStatus.Suspected != (msg.Reason != null) &&
            msg.Status == SecurityStatus.Hostile != (msg.Reason != null))
            return;

        // DS14-start
        if (msg.Status == SecurityStatus.Detained != (msg.Articles != null) ||
            msg.Status == SecurityStatus.Detained != (msg.Sentence != null))
            return;
        // DS14-end

        if (!CheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        if (!_records.TryGetRecord<CriminalRecord>(key.Value, out var record) || record.Status == msg.Status)
            return;

        // validate the reason
        string? reason = null;
        if (msg.Reason != null)
        {
            reason = msg.Reason.Trim();
            if (reason.Length < 1 || reason.Length > ent.Comp.MaxStringLength)
                return;
        }

        // DS14-start
        string? articles = null;
        string? sentence = null;
        if (msg.Articles != null && msg.Sentence != null)
        {
            articles = msg.Articles.Trim();
            sentence = msg.Sentence.Trim();
            if (articles.Length < 1 || articles.Length > ent.Comp.MaxStringLength ||
                sentence.Length < 1 || sentence.Length > ent.Comp.MaxStringLength)
                return;
        }
        // DS14-end

        var oldStatus = record.Status;

        var name = _records.RecordName(key.Value);
        GetOfficer(mob.Value, out var officer);

        // when arresting someone add it to history automatically
        // fallback exists if the player was not set to wanted beforehand
        if (msg.Status == SecurityStatus.Detained)
        {
            // DS14: prefer the freshly entered articles over the stale wanted reason, if given
            var oldReason = articles ?? record.Reason ?? Loc.GetString("criminal-records-console-unspecified-reason");
            var history = Loc.GetString("criminal-records-console-auto-history", ("reason", oldReason));
            _criminalRecords.TryAddHistory(key.Value, history, officer, sentence); // DS14: record the sentence alongside the article
        }

        // will probably never fail given the checks above
        name = _records.RecordName(key.Value);
        officer = Loc.GetString("criminal-records-console-unknown-officer");
        var jobName = "Unknown";

        _records.TryGetRecord<GeneralStationRecord>(key.Value, out var entry);
        if (entry != null)
            jobName = entry.JobTitle;

        var tryGetIdentityShortInfoEvent = new TryGetIdentityShortInfoEvent(null, mob.Value);
        RaiseLocalEvent(tryGetIdentityShortInfoEvent);
        if (tryGetIdentityShortInfoEvent.Title != null)
            officer = tryGetIdentityShortInfoEvent.Title;

        _criminalRecords.TryChangeStatus(key.Value, msg.Status, msg.Reason, officer, articles, sentence); // DS14: pass articles/sentence

        (string, object)[] args;
        if (reason != null)
            args = new (string, object)[] { ("name", name), ("officer", officer), ("reason", reason), ("job", jobName) };
        else
            args = new (string, object)[] { ("name", name), ("officer", officer), ("job", jobName) };

        // figure out which radio message to send depending on transition
        var statusString = (oldStatus, msg.Status) switch
        {
            (_, SecurityStatus.Hostile) => "hostile",
            (_, SecurityStatus.Eliminated) => "eliminated",
            // person has been detained
            (_, SecurityStatus.Detained) => "detained",
            // person did something sus
            (_, SecurityStatus.Suspected) => "suspected",
            // released on parole
            // (_, SecurityStatus.Paroled) => "paroled", // DS14-no-paroled
            // prisoner did their time
            (_, SecurityStatus.Discharged) => "released",
            // going from any other state to wanted, AOS or prisonbreak / lazy secoff never set them to released and they reoffended
            (_, SecurityStatus.Wanted) => "wanted",
            (SecurityStatus.Hostile, SecurityStatus.None) => "not-hostile",
            (SecurityStatus.Eliminated, SecurityStatus.None) => "not-eliminated",
            // person is no longer sus
            (SecurityStatus.Suspected, SecurityStatus.None) => "not-suspected",
            // going from wanted to none, must have been a mistake
            (SecurityStatus.Wanted, SecurityStatus.None) => "not-wanted",
            // criminal status removed
            (SecurityStatus.Detained, SecurityStatus.None) => "released",
            // criminal is no longer on parole
            // (SecurityStatus.Paroled, SecurityStatus.None) => "not-parole", // DS14-no-paroled
            // this is impossible
            _ => "not-wanted"
        };
        _radio.SendRadioMessage(ent,
            Loc.GetString($"criminal-records-console-{statusString}", args),
            ent.Comp.SecurityChannel,
            ent);

        _adminLogger.Add(LogType.Identity, LogImpact.Low, $"{ToPrettyString(mob.Value):name} changed criminal status for {name} to \"{statusString}\"");

        UpdateUserInterface(ent);
    }

    private void OnAddHistory(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordAddHistory msg)
    {
        if (!CheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        var line = msg.Line.Trim();
        if (line.Length < 1 || line.Length > ent.Comp.MaxStringLength)
            return;

        GetOfficer(mob.Value, out var officer);

        if (!_criminalRecords.TryAddHistory(key.Value, line, officer))
            return;

        // no radio message since its not crucial to officers patrolling

        UpdateUserInterface(ent);
    }

    private void OnDeleteHistory(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordDeleteHistory msg)
    {
        if (!CheckSelected(ent, msg.Actor, out _, out var key))
            return;

        if (!_criminalRecords.TryDeleteHistory(key.Value, msg.Index))
            return;

        // a bit sus but not crucial to officers patrolling

        UpdateUserInterface(ent);
    }

    // DS14-start
    private void OnPrintDocument(Entity<CriminalRecordsConsoleComponent> ent, ref CriminalRecordPrintDocument msg)
    {
        if (!ent.Comp.HasPrinter)
            return;

        if (!CheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        if (_timing.CurTime < ent.Comp.NextPrintTime)
            return;

        if (!_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general))
            return;

        if (!_records.TryGetRecord<CriminalRecord>(key.Value, out var record))
            return;

        var form = record.Status switch
        {
            SecurityStatus.Wanted => ent.Comp.ArrestWarrantForm,
            SecurityStatus.Eliminated => ent.Comp.ArrestWarrantForm,
            SecurityStatus.Detained => ent.Comp.VerdictForm,
            _ => (ProtoId<PaperworkFormPrototype>?) null,
        };

        if (form is not { } formId || !_prototype.TryIndex(formId, out var formPrototype))
            return;

        PrintDocument(ent, mob.Value, general, record, formPrototype);
    }

    private void PrintDocument(Entity<CriminalRecordsConsoleComponent> ent, EntityUid actor, GeneralStationRecord general, CriminalRecord record, PaperworkFormPrototype formPrototype)
    {
        var text = _resourceManager.ContentFileReadText(formPrototype.Text).ReadToEnd();

        var stationName = _station.GetOwningStation(ent) is { } station ? Name(station) : null;
        text = PaperworkTextSubstitutions.ApplyBase(text, Loc.GetString(formPrototype.Name), _gameTicker.RoundDuration(), stationName);

        var authorName = Loc.GetString("criminal-records-console-unknown-officer");
        var authorJob = string.Empty;
        if (_idCard.TryFindIdCard(actor, out var authorCard))
        {
            if (!string.IsNullOrWhiteSpace(authorCard.Comp.FullName))
                authorName = authorCard.Comp.FullName;
            authorJob = authorCard.Comp.LocalizedJobTitle ?? string.Empty;
        }

        text = text.Replace("{{AUTHOR.NAME}}", authorName);
        text = text.Replace("{{AUTHOR.JOB}}", authorJob);
        text = text.Replace("{{TARGET.NAME}}", general.Name);
        text = text.Replace("{{TARGET.JOB}}", general.JobTitle ?? string.Empty);

        text = record.Status switch
        {
            SecurityStatus.Wanted or SecurityStatus.Eliminated => text
                .Replace("{{ARTICLES}}", record.Reason ?? string.Empty),
            SecurityStatus.Detained => text
                .Replace("{{ARTICLES}}", record.Articles ?? string.Empty)
                .Replace("{{SENTENCE}}", record.Sentence ?? string.Empty),
            _ => text,
        };

        var printed = Spawn(formPrototype.PaperPrototype, Transform(ent).Coordinates);
        if (TryComp<PaperComponent>(printed, out var paper))
            _paperSystem.SetContent((printed, paper), text);

        _audio.PlayPvs(ent.Comp.PrintSound, ent);
        ent.Comp.NextPrintTime = _timing.CurTime + ent.Comp.PrintDelay;
    }
    // DS14-end

    private void UpdateUserInterface(Entity<CriminalRecordsConsoleComponent> ent)
    {
        var (uid, console) = ent;
        var owningStation = _station.GetOwningStation(uid);

        if (!TryComp<StationRecordsComponent>(owningStation, out var stationRecords))
        {
            _ui.SetUiState(uid, CriminalRecordsConsoleKey.Key, new CriminalRecordsConsoleState());
            return;
        }

        // get the listing of records to display
        var listing = _records.BuildListing((owningStation.Value, stationRecords), console.Filter);

        // filter the listing by the selected criminal record status
        //if NONE, dont filter by status, just show all crew
        if (console.FilterStatus != SecurityStatus.None)
        {
            listing = listing
                .Where(x => _records.TryGetRecord<CriminalRecord>(new StationRecordKey(x.Key, owningStation.Value), out var record) && record.Status == console.FilterStatus)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        var state = new CriminalRecordsConsoleState(listing, console.Filter);
        if (console.ActiveKey is { } id)
        {
            // get records to display when a crewmember is selected
            var key = new StationRecordKey(id, owningStation.Value);
            _records.TryGetRecord(key, out state.StationRecord, stationRecords);
            _records.TryGetRecord(key, out state.CriminalRecord, stationRecords);
            state.SelectedKey = id;
        }

        // Set the Current Tab aka the filter status type for the records list
        state.FilterStatus = console.FilterStatus;

        // DS14: printable while a Wanted/Eliminated (-> warrant) or Detained (-> verdict) record is selected
        state.CanPrint = console.HasPrinter &&
            state.CriminalRecord is { Status: SecurityStatus.Wanted or SecurityStatus.Eliminated or SecurityStatus.Detained };

        _ui.SetUiState(uid, CriminalRecordsConsoleKey.Key, state);
    }

    /// <summary>
    /// Boilerplate that most actions use, if they require that a record be selected.
    /// Obviously shouldn't be used for selecting records.
    /// </summary>
    private bool CheckSelected(Entity<CriminalRecordsConsoleComponent> ent, EntityUid user,
        [NotNullWhen(true)] out EntityUid? mob, [NotNullWhen(true)] out StationRecordKey? key)
    {
        key = null;
        mob = null;

        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("criminal-records-permission-denied"), ent, user);
            return false;
        }

        if (ent.Comp.ActiveKey is not { } id)
            return false;

        // checking the console's station since the user might be off-grid using on-grid console
        if (_station.GetOwningStation(ent) is not { } station)
            return false;

        key = new StationRecordKey(id, station);
        mob = user;
        return true;
    }
}
