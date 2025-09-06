using Content.Shared.EntityEffects;
using Content.Shared.DeadSpace.MentalIllness;
using Content.Server.DeadSpace.MentalIllness.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.MentalIllness.ReagentEffects;

public sealed partial class AddMentalIllness : EntityEffect
{
    [DataField]
    public List<MentalIllnessType> Illnesses = new();

    [DataField]
    public float Severity = 0.01f;

    [DataField]
    public float AddTickIntervals = 0f;
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-cure-mentalIllness", ("chance", Probability));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entityManager = args.EntityManager;
        var target = args.TargetEntity;

        if (!entityManager.TryGetComponent<MentalIllnessComponent>(target, out var component))
            return;

        foreach (var illness in Illnesses)
        {
            if (!component.ActiveIllnesses.Contains(illness))
                continue;

            var system = args.EntityManager.EntitySysManager.GetEntitySystem<MentalIllnessSystem>();

            system.AddIllnessSeverity(target, illness, Severity, true, component);
            system.AddIllnessTickIntervals(target, illness, AddTickIntervals, component);
        }
    }
}

