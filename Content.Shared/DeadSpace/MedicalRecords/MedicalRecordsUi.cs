// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.MedicalRecords;

[Serializable, NetSerializable]
public enum MedicalRecordsConsoleKey : byte
{
    Key
}

/// <summary>
/// Medical Records console state. Selecting and filtering reuse <see cref="SelectStationRecord"/>
/// and <see cref="SetStationRecordFilter"/>, the same messages the other records consoles use.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecordsConsoleState : BoundUserInterfaceState
{
    public uint? SelectedKey;

    public GeneralStationRecord? StationRecord;

    public MedicalRecord? MedicalRecord;

    public MedicalStatus FilterStatus;
    public readonly Dictionary<uint, string>? RecordListing;
    public readonly StationRecordsFilter? Filter;

    public bool CanEdit;

    public bool CanDelete;

    public bool CanPrint;

    public MedicalRecordsConsoleState(Dictionary<uint, string>? recordListing, StationRecordsFilter? filter)
    {
        RecordListing = recordListing;
        Filter = filter;
    }

    public MedicalRecordsConsoleState() : this(null, null)
    {
    }

    public bool IsEmpty() => SelectedKey == null && StationRecord == null && MedicalRecord == null && RecordListing == null;
}

/// <summary>
/// Sets the patient-status filter for the crew listing (mirrors
/// <c>PersonnelRecordSetStatusFilter</c>). <see cref="MedicalStatus.None"/> clears the filter.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecordSetStatusFilter : BoundUserInterfaceMessage
{
    public readonly MedicalStatus FilterStatus;

    public MedicalRecordSetStatusFilter(MedicalStatus filterStatus)
    {
        FilterStatus = filterStatus;
    }
}

/// <summary>
/// Sets the selected patient's status. The server records the choice as the doctor's own and lets
/// it stand, rather than second-guessing it against the history.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecordChangeStatus : BoundUserInterfaceMessage
{
    public readonly MedicalStatus Status;

    public MedicalRecordChangeStatus(MedicalStatus status)
    {
        Status = status;
    }
}

/// <summary>
/// Appends a new case to the selected patient's history. The server re-validates every field and
/// owns <see cref="MedicalCase.AddTime"/> and <see cref="MedicalCase.AuthorName"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecordAddCase : BoundUserInterfaceMessage
{
    public readonly string AdmissionState;
    public readonly string Diagnosis;
    public readonly string Treatment;
    public readonly bool NeedsContinuedTreatment;
    public readonly bool NeedsForcedTreatment;
    public readonly List<string> Specialists;
    public readonly string Recommendations;
    public readonly string DischargeState;

    public MedicalRecordAddCase(
        string admissionState,
        string diagnosis,
        string treatment,
        bool needsContinuedTreatment,
        bool needsForcedTreatment,
        List<string> specialists,
        string recommendations,
        string dischargeState)
    {
        AdmissionState = admissionState;
        Diagnosis = diagnosis;
        Treatment = treatment;
        NeedsContinuedTreatment = needsContinuedTreatment;
        NeedsForcedTreatment = needsForcedTreatment;
        Specialists = specialists;
        Recommendations = recommendations;
        DischargeState = dischargeState;
    }
}

/// <summary>
/// Overwrites the case at <see cref="Index"/>. Same field set as
/// <see cref="MedicalRecordAddCase"/>, minus kind and author, which an edit never rewrites.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecordEditCase : BoundUserInterfaceMessage
{
    public readonly int Index;
    public readonly string AdmissionState;
    public readonly string Diagnosis;
    public readonly string Treatment;
    public readonly bool NeedsContinuedTreatment;
    public readonly bool NeedsForcedTreatment;
    public readonly List<string> Specialists;
    public readonly string Recommendations;
    public readonly string DischargeState;
    public readonly bool Open;

    public MedicalRecordEditCase(
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
        Index = index;
        AdmissionState = admissionState;
        Diagnosis = diagnosis;
        Treatment = treatment;
        NeedsContinuedTreatment = needsContinuedTreatment;
        NeedsForcedTreatment = needsForcedTreatment;
        Specialists = specialists;
        Recommendations = recommendations;
        DischargeState = dischargeState;
        Open = open;
    }
}

/// <summary>
/// Removes the case at <see cref="Index"/>. Rejected unless the actor holds the head of Medical's
/// access level - see <see cref="MedicalRecordsConsoleState.CanDelete"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecordDeleteCase : BoundUserInterfaceMessage
{
    public readonly int Index;

    public MedicalRecordDeleteCase(int index)
    {
        Index = index;
    }
}

/// <summary>
/// Prints a health conclusion for a single case. Separate from
/// <see cref="MedicalRecordEditCase"/> so printing a closed case months later needs no edit rights.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecordPrintCase : BoundUserInterfaceMessage
{
    public readonly int Index;

    public MedicalRecordPrintCase(int index)
    {
        Index = index;
    }
}
