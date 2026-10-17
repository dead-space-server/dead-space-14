using Content.Shared.DeadSpace.Surgery.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

public readonly record struct SurgeryContext(
    EntityUid Patient,
    EntityUid? Surgeon = null,
    EntityUid? Tool = null,
    EntityUid? OperatingTable = null,
    SurgeryPrototype? Surgery = null,
    SurgeryStepPrototype? Step = null,
    int StepIndex = 0)
{
    public EntityUid? GetEntity(SurgeryEntityTarget target)
    {
        return target switch
        {
            SurgeryEntityTarget.Surgeon => Surgeon,
            SurgeryEntityTarget.Tool => Tool,
            SurgeryEntityTarget.OperatingTable => OperatingTable,
            _ => Patient,
        };
    }

    public SurgeryContext WithStep(SurgeryStepPrototype? step, int stepIndex = 0)
    {
        return this with { Step = step, StepIndex = stepIndex };
    }
}

public enum SurgeryEntityTarget : byte
{
    Patient,

    Surgeon,

    Tool,

    OperatingTable,
}
