// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.Psychiatry;

public enum PsychiatryIllnessKind : byte
{
    Schizophrenia = 0,
    Cyberpsychosis = 1,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SchizophreniaComponent : Component
{
    [DataField, AutoNetworkedField]
    public PsychiatryIllnessKind Kind = PsychiatryIllnessKind.Schizophrenia;

    [DataField, AutoNetworkedField]
    public SchizophreniaStage Stage = SchizophreniaStage.Latent;

    [DataField, AutoNetworkedField]
    public float StageHealth = 1f;

    [DataField, AutoNetworkedField]
    public int Seed;

    [DataField, AutoNetworkedField]
    public bool PillForced;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextAutoEscalate;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextWhisper;

    [DataField]
    public int CourseNeeded;

    [DataField]
    public int CourseTaken;

    [DataField]
    public float CourseMetabolized;

    [DataField]
    public bool CourseSpoiled;

    [DataField]
    public TimeSpan NextUnrealSound;
}

[RegisterComponent]
public sealed partial class SchizophreniaOnsetTrackerComponent : Component
{
    public TimeSpan NextAllowedOnset;

    public TimeSpan NextAllowedGasOnset;

    public TimeSpan NextAsphyxiationRoll;

    public TimeSpan NextRadiationRoll;

    public TimeSpan NextShockRoll;

    public HashSet<string> RolledMedicines = new();
}
