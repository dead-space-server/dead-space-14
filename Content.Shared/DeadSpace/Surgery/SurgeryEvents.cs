using Content.Shared.DeadSpace.Surgery.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[ByRefEvent]
public record struct SurgeryStartedEvent(SurgeryContext Context);

[ByRefEvent]
public readonly record struct SurgeryStepCompletedEvent(SurgeryContext Context);

[ByRefEvent]
public readonly record struct SurgeryCompletedEvent(SurgeryContext Context);

[ByRefEvent]
public readonly record struct SurgeryComplicationTriggeredEvent(
    SurgeryContext Context,
    SurgeryComplicationPrototype Complication,
    float Chance);

[ByRefEvent]
public record struct SurgeryComplicationRollEvent(
    SurgeryContext Context,
    SurgeryComplicationPrototype Complication)
{
    public float Chance;
}
