// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.MedicalRecords;

[Serializable, NetSerializable, DataRecord]
public sealed partial record MedicalRecord
{
    [DataField]
    public Sex Sex = Sex.Male;

    [DataField]
    public MedicalStatus Status = MedicalStatus.None;

    [DataField]
    public bool StatusManuallySet;

    [DataField]
    public List<MedicalCase> History = new();
}

[Serializable, NetSerializable, DataRecord]
public sealed partial record MedicalCase
{
    [DataField]
    public MedicalCaseKind Kind = MedicalCaseKind.Illness;

    [DataField]
    public TimeSpan AddTime = TimeSpan.Zero;

    [DataField]
    public string AdmissionState = string.Empty;

    [DataField]
    public string Diagnosis = string.Empty;

    [DataField]
    public string Treatment = string.Empty;

    [DataField]
    public bool NeedsContinuedTreatment;

    [DataField]
    public bool NeedsForcedTreatment;

    [DataField]
    public List<string> Specialists = new();

    [DataField]
    public string Recommendations = string.Empty;

    [DataField]
    public string DischargeState = string.Empty;

    [DataField]
    public string? AuthorName;

    [DataField]
    public bool Open = true;
}
