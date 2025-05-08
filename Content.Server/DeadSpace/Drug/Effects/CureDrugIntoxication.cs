// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Drug.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Drug.Effects;

public sealed partial class CureDrugIntoxication : EntityEffect
{
    [DataField]
    public float HealStrenght = -1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-cure-drug-intoxication", ("chance", Probability));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entityManager = args.EntityManager;

        entityManager.EnsureComponent<DrugAddicationComponent>(args.TargetEntity);

        args.EntityManager.System<DrugAddicationSystem>().AddTimeLastAppointment(
            args.TargetEntity,
            HealStrenght
            );
    }
}

