using Content.Shared.EntityEffects;
using Content.Shared.DeadSpace.MentalIllness;
using Content.Server.DeadSpace.MentalIllness.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MentalIllness.ReagentEffects;

public sealed partial class CauseMentalIllness : EntityEffect
{
    [DataField]
    public MentalIllnessType Illnesses = new();

    [DataField]
    public float Severity = 0.01f;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-cause-mentalIllness", ("chance", Probability));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entityManager = args.EntityManager;
        var target = args.TargetEntity;
        var system = args.EntityManager.EntitySysManager.GetEntitySystem<MentalIllnessSystem>();

        if (entityManager.TryGetComponent<MentalIllnessComponent>(target, out var component))
        {
            system.TryAddIllness(target, Illnesses, Severity, component);
        }
        else
        {
            var mentalIllness = entityManager.AddComponent<MentalIllnessComponent>(target);
            system.TryAddIllness(target, Illnesses, Severity, mentalIllness);
        }

    }
}

