using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.MentalIllness.ReagentEffects;

public sealed partial class CauseMentalIllness : EventEntityEffect<CauseMentalIllness>
{
    [DataField]
    public MentalIllnessType Illnesses = new();

    [DataField]
    public float Severity = 0.01f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cause-mentalIllness", ("chance", Probability));
}

