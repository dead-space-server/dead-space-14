// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.MedicalRecords;

/// <summary>
/// Patient status, tracked by the medical records console and rendered on
/// <c>MedicalRecordComponent</c>. Deliberately separate from <c>SecurityStatus</c>: a medical status
/// <summary>
/// never reaches the criminal records console and never raises a wanted flag.
/// </summary>
/// </summary>
[Serializable, NetSerializable]
public enum MedicalStatus : byte
{
    None = 0,

    OnTreatment,

    PsychUnstable,

    CompletedTreatment,
}

[Serializable, NetSerializable]
public enum MedicalCaseKind : byte
{
    Deviation = 0,

    Illness,
}
