// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Access;
using Content.Shared.DeadSpace.MedicalRecords.Systems;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.MedicalRecords.Components;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedMedicalRecordsConsoleSystem))]
public sealed partial class MedicalRecordsConsoleComponent : Component
{
    [DataField]
    public uint? ActiveKey;

    [DataField]
    public StationRecordsFilter? Filter;

    [DataField]
    public MedicalStatus FilterStatus;

    [DataField]
    public ProtoId<AccessLevelPrototype> DeleteAccess = "ChiefMedicalOfficer";

    [DataField]
    public List<ProtoId<JobPrototype>> ExcludedJobs = new()
    {
        "Dismissed",
        "Visitor",
        "Magistrat",
    };

    [DataField]
    public uint MaxStringLength = 256;

    [DataField]
    public int MaxSpecialists = 8;

    [DataField]
    public int MaxCases = 64;

    [DataField]
    public TimeSpan ActionDelay = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextActionTime = TimeSpan.Zero;

    [DataField]
    public ProtoId<PaperworkFormPrototype> ConclusionForm = "MedicalConclusion";

    [DataField]
    public SoundSpecifier PrintSound = new SoundCollectionSpecifier("PrinterPrint");

    [DataField]
    public TimeSpan PrintDelay = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPrintTime = TimeSpan.Zero;
}
