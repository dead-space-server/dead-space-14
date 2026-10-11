// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.IdentityManagement;
using Content.Shared.DeadSpace.MedicalRecords;
using Content.Shared.DeadSpace.MedicalRecords.Components;
using Content.Shared.DeadSpace.MedicalRecords.Systems;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.MedicalRecords.Systems;

/// <summary>
/// Handles all UI and permission logic for the Medical Records console.
/// </summary>
/// 
/// <summary>
/// Nothing about a patient's record is trusted from the client; the server re-validates every action on receipt.
/// </summary>
public sealed class MedicalRecordsConsoleSystem : SharedMedicalRecordsConsoleSystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly MedicalRecordsSystem _medicalRecords = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedicalRecordsConsoleComponent, RecordModifiedEvent>(OnRecordBroadcast);
        SubscribeLocalEvent<MedicalRecordsConsoleComponent, AfterGeneralRecordCreatedEvent>(OnRecordBroadcast);

        Subs.BuiEvents<MedicalRecordsConsoleComponent>(MedicalRecordsConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            subs.Event<MedicalRecordSetStatusFilter>(OnStatusFilterPressed);
            subs.Event<MedicalRecordChangeStatus>(OnChangeStatus);
            subs.Event<MedicalRecordAddCase>(OnAddCase);
            subs.Event<MedicalRecordEditCase>(OnEditCase);
            subs.Event<MedicalRecordDeleteCase>(OnDeleteCase);
        });
    }

    #region BUI plumbing

    private void OnRecordBroadcast<T>(Entity<MedicalRecordsConsoleComponent> ent, ref T args)
    {
        // Every record change anywhere refreshes every open console - same trade-off
        // CriminalRecordsConsoleSystem makes for the same reason (no per-key push channel).
        UpdateUserInterface(ent);
    }

    private void OnUiOpened(Entity<MedicalRecordsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent, args.Actor);
    }

    private void OnKeySelected(Entity<MedicalRecordsConsoleComponent> ent, ref SelectStationRecord msg)
    {
        // No concern of a sus client here: record retrieval fails harmlessly on an invalid id.
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(ent, msg.Actor);
    }

    private void OnFiltersChanged(Entity<MedicalRecordsConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null || ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent, msg.Actor);
        }
    }

    private void OnStatusFilterPressed(Entity<MedicalRecordsConsoleComponent> ent, ref MedicalRecordSetStatusFilter msg)
    {
        ent.Comp.FilterStatus = msg.FilterStatus;
        UpdateUserInterface(ent, msg.Actor);
    }

    #endregion

    #region Actions

    private void OnChangeStatus(Entity<MedicalRecordsConsoleComponent> ent, ref MedicalRecordChangeStatus msg)
    {
        if (!CheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        if (_timing.CurTime < ent.Comp.NextActionTime)
            return;

        if (!CanEdit(mob.Value, ent))
        {
            DenyAccess(ent, mob.Value);
            return;
        }

        if (!_medicalRecords.TryChangeStatus(key.Value, msg.Status))
            return;

        ent.Comp.NextActionTime = _timing.CurTime + ent.Comp.ActionDelay;

        if (_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general))
        {
            _adminLogger.Add(LogType.Identity,
                LogImpact.Medium,
                $"{ToPrettyString(mob.Value):actor} set the medical status of {general.Name} to {msg.Status} ({ToPrettyString(ent):console})");
        }

        UpdateUserInterface(ent, mob.Value);
    }

    private void OnAddCase(Entity<MedicalRecordsConsoleComponent> ent, ref MedicalRecordAddCase msg)
    {
        if (!CheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        if (_timing.CurTime < ent.Comp.NextActionTime)
            return;

        if (!CanEdit(mob.Value, ent))
        {
            DenyAccess(ent, mob.Value);
            return;
        }

        if (!_records.TryGetRecord<MedicalRecord>(key.Value, out var record))
            return;

        if (record.History.Count >= ent.Comp.MaxCases)
        {
            _popup.PopupEntity(Loc.GetString("medical-records-console-too-many-cases"), ent, mob.Value);
            return;
        }

        if (!TrySanitizeCase(ent.Comp, msg.AdmissionState, msg.Diagnosis, msg.Treatment,
                msg.Specialists, msg.Recommendations, msg.DischargeState,
                out var admission, out var diagnosis, out var treatment,
                out var specialists, out var recommendations, out var discharge))
        {
            _popup.PopupEntity(Loc.GetString("medical-records-console-invalid-case"), ent, mob.Value);
            return;
        }

        var author = GetAuthorName(mob.Value);

        if (!_medicalRecords.TryAddCase(key.Value, admission, diagnosis, treatment,
                msg.NeedsContinuedTreatment, msg.NeedsForcedTreatment, specialists,
                recommendations, discharge, author))
            return;

        ent.Comp.NextActionTime = _timing.CurTime + ent.Comp.ActionDelay;

        if (_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general))
        {
            _adminLogger.Add(LogType.Identity,
                LogImpact.Medium,
                $"{ToPrettyString(mob.Value):actor} opened a medical case on {general.Name}: {diagnosis} ({ToPrettyString(ent):console})");
        }

        UpdateUserInterface(ent, mob.Value);
    }

    private void OnEditCase(Entity<MedicalRecordsConsoleComponent> ent, ref MedicalRecordEditCase msg)
    {
        if (!CheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        if (_timing.CurTime < ent.Comp.NextActionTime)
            return;

        if (!CanEdit(mob.Value, ent))
        {
            DenyAccess(ent, mob.Value);
            return;
        }

        if (!TrySanitizeCase(ent.Comp, msg.AdmissionState, msg.Diagnosis, msg.Treatment,
                msg.Specialists, msg.Recommendations, msg.DischargeState,
                out var admission, out var diagnosis, out var treatment,
                out var specialists, out var recommendations, out var discharge))
        {
            _popup.PopupEntity(Loc.GetString("medical-records-console-invalid-case"), ent, mob.Value);
            return;
        }

        if (!_medicalRecords.TryEditCase(key.Value, msg.Index, admission, diagnosis, treatment,
                msg.NeedsContinuedTreatment, msg.NeedsForcedTreatment, specialists,
                recommendations, discharge, msg.Open))
            return;

        ent.Comp.NextActionTime = _timing.CurTime + ent.Comp.ActionDelay;

        if (_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general))
        {
            _adminLogger.Add(LogType.Identity,
                LogImpact.Medium,
                $"{ToPrettyString(mob.Value):actor} edited medical case {msg.Index} of {general.Name}: {diagnosis} ({ToPrettyString(ent):console})");
        }

        UpdateUserInterface(ent, mob.Value);
    }

    private void OnDeleteCase(Entity<MedicalRecordsConsoleComponent> ent, ref MedicalRecordDeleteCase msg)
    {
        if (!CheckSelected(ent, msg.Actor, out var mob, out var key))
            return;

        if (_timing.CurTime < ent.Comp.NextActionTime)
            return;

        if (!_records.TryGetRecord<MedicalRecord>(key.Value, out var record))
            return;

        if (msg.Index < 0 || msg.Index >= record.History.Count)
            return;

        var caseKind = record.History[msg.Index].Kind;
        var diagnosis = record.History[msg.Index].Diagnosis;

        // Deletion is checked separately from, and strictly after, the ordinary edit rights: a
        // deleted auto-populated deviation cannot be put back within the round, so the button is
        // gated to the head of Medical. Checked here and again inside the state rebuild - the
        // client-side flag only decides whether to draw the button.
        if (!CanDelete(mob.Value, ent))
        {
            DenyAccess(ent, mob.Value);
            return;
        }

        if (!_medicalRecords.TryDeleteCase(key.Value, msg.Index))
            return;

        ent.Comp.NextActionTime = _timing.CurTime + ent.Comp.ActionDelay;

        if (_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general))
        {
            _adminLogger.Add(LogType.Identity,
                LogImpact.High,
                $"{ToPrettyString(mob.Value):actor} deleted a {caseKind} medical case of {general.Name}: {diagnosis} ({ToPrettyString(ent):console})");
        }

        UpdateUserInterface(ent, mob.Value);
    }

    #endregion

    #region Validation

    private static bool TrySanitizeCase(
        MedicalRecordsConsoleComponent console,
        string? admissionState,
        string? diagnosis,
        string? treatment,
        List<string>? specialists,
        string? recommendations,
        string? dischargeState,
        out string admission,
        out string diag,
        out string treat,
        out List<string> specs,
        out string recs,
        out string discharge)
    {
        admission = Sanitize(admissionState, console.MaxStringLength);
        diag = Sanitize(diagnosis, console.MaxStringLength);
        treat = Sanitize(treatment, console.MaxStringLength);
        recs = Sanitize(recommendations, console.MaxStringLength);
        discharge = Sanitize(dischargeState, console.MaxStringLength);

        if (diag.Length == 0)
        {
            specs = new List<string>();
            return false;
        }

        specs = new List<string>();

        if (specialists != null)
        {
            foreach (var specialist in specialists)
            {
                if (specs.Count >= console.MaxSpecialists)
                    break;

                var name = Sanitize(specialist, console.MaxStringLength);
                if (name.Length > 0)
                    specs.Add(name);
            }
        }

        return true;
    }

    private static string Sanitize(string? input, uint maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var trimmed = input.Trim();

        return trimmed.Length > maxLength ? trimmed[..(int) maxLength] : trimmed;
    }

    #endregion

    #region Permissions

    private bool CheckSelected(Entity<MedicalRecordsConsoleComponent> ent, EntityUid user,
        [NotNullWhen(true)] out EntityUid? mob, [NotNullWhen(true)] out StationRecordKey? key)
    {
        key = null;
        mob = null;

        if (!_access.IsAllowed(user, ent))
        {
            DenyAccess(ent, user);
            return false;
        }

        if (ent.Comp.ActiveKey is not { } id)
            return false;

        if (_station.GetOwningStation(ent) is not { } station)
            return false;

        key = new StationRecordKey(id, station);
        mob = user;
        return true;
    }

    private bool CanEdit(EntityUid user, Entity<MedicalRecordsConsoleComponent> ent) =>
        _access.IsAllowed(user, ent);

    private bool CanDelete(EntityUid user, Entity<MedicalRecordsConsoleComponent> ent) =>
        CanEdit(user, ent) && HasAccessTag(user, ent.Comp.DeleteAccess);

    private bool HasAccessTag(EntityUid user, ProtoId<AccessLevelPrototype> tag)
    {
        // Hand-rolled rather than FindAccessTags(...).Contains(...) - the result is an
        // ICollection<T>, not a List<T>, so Contains would silently bind to the LINQ extension and
        // drag a System.Linq dependency into a file that has no other use for it.
        foreach (var candidate in _access.FindAccessTags(user))
        {
            if (candidate == tag)
                return true;
        }

        return false;
    }

    private void DenyAccess(Entity<MedicalRecordsConsoleComponent> ent, EntityUid user) =>
        _popup.PopupEntity(Loc.GetString("medical-records-console-permission-denied"), ent, user);

    private string GetAuthorName(EntityUid uid)
    {
        if (_idCard.TryFindIdCard(uid, out var idCard) && !string.IsNullOrWhiteSpace(idCard.Comp.FullName))
            return idCard.Comp.FullName;

        return Loc.GetString("medical-records-console-unknown-doctor");
    }

    #endregion

    #region State

    private void UpdateUserInterface(Entity<MedicalRecordsConsoleComponent> ent)
    {
        var actor = TryComp<ActivatableUIComponent>(ent, out var activatable) ? activatable.CurrentSingleUser : null;
        UpdateUserInterface(ent, actor ?? EntityUid.Invalid);
    }

    private void UpdateUserInterface(Entity<MedicalRecordsConsoleComponent> ent, EntityUid actor)
    {
        var (uid, console) = ent;
        var owningStation = _station.GetOwningStation(uid);

        if (!TryComp<StationRecordsComponent>(owningStation, out var stationRecords))
        {
            _ui.SetUiState(uid, MedicalRecordsConsoleKey.Key, new MedicalRecordsConsoleState());
            return;
        }

        var hasAccess = _access.IsAllowed(actor, ent);

        // An empty listing rather than a null one when access is missing: the window is already open
        // at this point (the AccessReader on the computer is what normally keeps it shut), and a
        // null listing reads as "the database could not be reached" rather than "you may not look".
        var listing = hasAccess
            ? _records.BuildListing((owningStation.Value, stationRecords), console.Filter)
                .Where(x => IsVisible(x.Key, owningStation.Value, console, stationRecords))
                .ToDictionary(x => x.Key, x => x.Value)
            : new Dictionary<uint, string>();

        if (console.FilterStatus != MedicalStatus.None)
        {
            // Only a patient who actually has a medical record can match a status filter, so the
            // TryGetRecord guard is doing real work here rather than just a null check.
            listing = listing
                .Where(x => _records.TryGetRecord<MedicalRecord>(new StationRecordKey(x.Key, owningStation.Value), out var record)
                    && record!.Status == console.FilterStatus)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        var state = new MedicalRecordsConsoleState(listing, console.Filter)
        {
            FilterStatus = console.FilterStatus,
        };

        if (hasAccess)
        {
            state.CanEdit = true;
            state.CanPrint = true;
            state.CanDelete = CanDelete(actor, ent);
        }

        if (console.ActiveKey is { } id)
        {
            var key = new StationRecordKey(id, owningStation.Value);
            if (hasAccess
                && _records.TryGetRecord<GeneralStationRecord>(key, out var general, stationRecords)
                && IsVisible(id, owningStation.Value, console, stationRecords))
            {
                state.StationRecord = general;

                if (_records.TryGetRecord(key, out MedicalRecord? medical, stationRecords))
                    state.MedicalRecord = medical;

                state.SelectedKey = id;
            }
            else
            {
                // ActiveKey is shared by the console entity, not by an individual BUI session.
                // Never retain a selection that the current user cannot see: otherwise the next
                // state update could disclose another station's records.
                console.ActiveKey = null;
            }
        }

        _ui.SetUiState(uid, MedicalRecordsConsoleKey.Key, state);
    }

    private bool IsVisible(uint id, EntityUid station, MedicalRecordsConsoleComponent console, StationRecordsComponent records)
    {
        var key = new StationRecordKey(id, station);
        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var general, records))
            return false;

        if (console.ExcludedJobs.Contains(general.JobPrototype))
            return false;

        // Silicons have a medical record like anyone else (silicon "deviations" are real medical
        // history in a station full of cyborgs), so the exclusion list is the only thing standing
        // between them and the crew listing - it is not extended to them by default.
        return true;
    }

    #endregion

    #region External accessors

    // MedicalRecordsConsoleComponent is [Access(typeof(SharedMedicalRecordsConsoleSystem))], so
    // MedicalPrintingSystem cannot read its fields despite needing the same print config. It goes
    // through these instead of having its own copy of the config, so the two can never disagree.

    public bool TryCheckSelected(Entity<MedicalRecordsConsoleComponent> ent, EntityUid user,
        [NotNullWhen(true)] out EntityUid? mob, [NotNullWhen(true)] out StationRecordKey? key) =>
        CheckSelected(ent, user, out mob, out key);

    public bool CanPrint(EntityUid user, Entity<MedicalRecordsConsoleComponent> ent) => CanEdit(user, ent);

    public TimeSpan GetNextPrintTime(MedicalRecordsConsoleComponent console) => console.NextPrintTime;

    public ProtoId<PaperworkFormPrototype> GetConclusionForm(MedicalRecordsConsoleComponent console) =>
        console.ConclusionForm;

    public TimeSpan GetPrintDelay(MedicalRecordsConsoleComponent console) => console.PrintDelay;

    public SoundSpecifier GetPrintSound(MedicalRecordsConsoleComponent console) => console.PrintSound;

    public void SetNextPrintTime(MedicalRecordsConsoleComponent console, TimeSpan time) =>
        console.NextPrintTime = time;

    #endregion
}
