using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Surgery.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

public readonly record struct SurgeryAttempt(
    SurgeryPrototype Surgery,
    SurgeryStepPrototype Step,
    SurgeryStepVariant Variant,
    int StepIndex);
