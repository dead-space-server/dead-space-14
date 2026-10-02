// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.MedicalRecords.Systems;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.MedicalRecords.Components;

/// <summary>
/// Holds the medical status icons currently shown for a mob. Added/removed by
/// <see cref="SharedMedicalRecordsSystem"/>, never present when the patient has no medical status.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMedicalRecordsSystem))]
public sealed partial class MedicalRecordComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<MedicalStatusIconPrototype>> Icons = new();
}
