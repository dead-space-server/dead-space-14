// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.StationRecords.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.DeadSpace.MedicalRecords;
using Content.Shared.DeadSpace.MedicalRecords.Systems;
using Content.Shared.Preferences;
using Content.Shared.StationRecords;
using Content.Shared.Traits;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.MedicalRecords.Systems;

/// <summary>
/// Owns the <see cref="MedicalRecord"/> riding alongside every crewmember's
/// <c>GeneralStationRecord</c>, and the history-editing logic the console system calls into. No
/// permission checking happens here - <c>MedicalRecordsConsoleSystem</c> checks every action first.
/// </summary>
public sealed class MedicalRecordsSystem : SharedMedicalRecordsSystem
{
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnGeneralRecordCreated);
    }

    private void OnGeneralRecordCreated(AfterGeneralRecordCreatedEvent ev)
    {
        // Respawning as the same character reuses the existing general record and re-raises this
        // event. Keep whatever the record already has - re-seeding it would wipe a treatment the
        // patient was given earlier in the shift.
        if (_records.TryGetRecord<MedicalRecord>(ev.Key, out _))
            return;

        var record = new MedicalRecord
        {
            Sex = ev.Profile.Sex,
            History = BuildDeviations(ev.Profile),
        };

        _records.AddRecordEntry(ev.Key, record);
        _records.Synchronize(ev.Key);
    }

    private List<MedicalCase> BuildDeviations(HumanoidCharacterProfile profile)
    {
        var deviations = new List<MedicalCase>();

        foreach (var traitId in profile.TraitPreferences)
        {
            if (!_prototypeManager.TryIndex(traitId, out var trait))
                continue;

            if (trait.Category == null)
                continue;

            deviations.Add(new MedicalCase
            {
                Kind = MedicalCaseKind.Deviation,
                Diagnosis = _loc.GetString(trait.Name),
                // Deviations are standing facts rather than something being treated right now, so
                // they start closed. A doctor ticks "needs continued treatment" when they take the
                // patient on for management of one.
                Open = false,
            });
        }

        return deviations;
    }

    public bool TryAddCase(
        StationRecordKey key,
        string admissionState,
        string diagnosis,
        string treatment,
        bool needsContinuedTreatment,
        bool needsForcedTreatment,
        List<string> specialists,
        string recommendations,
        string dischargeState,
        string? authorName)
    {
        if (!_records.TryGetRecord<MedicalRecord>(key, out var record))
            return false;

        record.History.Add(new MedicalCase
        {
            Kind = MedicalCaseKind.Illness,
            AddTime = _gameTiming.CurTime,
            AdmissionState = admissionState,
            Diagnosis = diagnosis,
            Treatment = treatment,
            NeedsContinuedTreatment = needsContinuedTreatment,
            NeedsForcedTreatment = needsForcedTreatment,
            Specialists = specialists,
            Recommendations = recommendations,
            DischargeState = dischargeState,
            AuthorName = authorName,
            Open = true,
        });

        Finalize(key, record);
        return true;
    }

    public bool TryEditCase(
        StationRecordKey key,
        int index,
        string admissionState,
        string diagnosis,
        string treatment,
        bool needsContinuedTreatment,
        bool needsForcedTreatment,
        List<string> specialists,
        string recommendations,
        string dischargeState,
        bool open)
    {
        if (!_records.TryGetRecord<MedicalRecord>(key, out var record))
            return false;

        if (!TryGetCase(record, index, out var medicalCase))
            return false;

        medicalCase.AdmissionState = admissionState;
        medicalCase.Diagnosis = diagnosis;
        medicalCase.Treatment = treatment;
        medicalCase.NeedsContinuedTreatment = needsContinuedTreatment;
        medicalCase.NeedsForcedTreatment = needsForcedTreatment;
        medicalCase.Specialists = specialists;
        medicalCase.Recommendations = recommendations;
        medicalCase.DischargeState = dischargeState;
        medicalCase.Open = open;

        Finalize(key, record);
        return true;
    }

    public bool TryDeleteCase(StationRecordKey key, int index)
    {
        if (!_records.TryGetRecord<MedicalRecord>(key, out var record))
            return false;

        if (!TryGetCase(record, index, out _))
            return false;

        record.History.RemoveAt(index);

        Finalize(key, record);
        return true;
    }

    public bool TryChangeStatus(StationRecordKey key, MedicalStatus status)
    {
        if (!_records.TryGetRecord<MedicalRecord>(key, out var record))
            return false;

        record.Status = status;
        record.StatusManuallySet = true;

        Finalize(key, record, recalculate: false);
        return true;
    }

    private bool TryGetCase(MedicalRecord record, int index, out MedicalCase medicalCase)
    {
        if (index >= 0 && index < record.History.Count)
        {
            medicalCase = record.History[index];
            return true;
        }

        medicalCase = null!;
        return false;
    }

    private MedicalStatus RecalculateStatus(MedicalRecord record)
    {
        var needsTreatment = record.History.Exists(medicalCase => medicalCase.Open && medicalCase.NeedsContinuedTreatment);

        switch (record.Status)
        {
            case MedicalStatus.None or MedicalStatus.CompletedTreatment when needsTreatment && !record.StatusManuallySet:
                record.Status = MedicalStatus.OnTreatment;
                break;
            case MedicalStatus.OnTreatment when !needsTreatment && !record.StatusManuallySet:
                record.Status = record.History.Count > 0
                    ? MedicalStatus.CompletedTreatment
                    : MedicalStatus.None;
                break;
        }

        return record.Status;
    }

    private void Finalize(StationRecordKey key, MedicalRecord record, bool recalculate = true)
    {
        if (recalculate)
            RecalculateStatus(record);

        _records.Synchronize(key);

        if (_records.TryGetRecord<GeneralStationRecord>(key, out var general))
            SetMedicalIcons(general.Name, record);
    }
}
