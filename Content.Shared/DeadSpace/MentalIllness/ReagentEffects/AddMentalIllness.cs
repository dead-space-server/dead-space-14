using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.MentalIllness.ReagentEffects;

public sealed partial class AddMentalIllness : EventEntityEffect<AddMentalIllness>
{
    [DataField]
    public List<MentalIllnessType> Illnesses = new();

    [DataField]
    public float Severity = 0.01f;

    [DataField]
    public float AddTickIntervals = 0f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cure-mentalIllness", ("chance", Probability));
}

