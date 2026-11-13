// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.DeadSpace.Necromorphs.InfectionDead;
using Content.Shared.Body.Components;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Mobs.Components;

namespace Content.Server.EntityEffects.Effects.DeadSpace;

/// <summary>
/// Causes the necromorph infection on this entity.
/// Uses strain data from the effect, optionally overridden by strain data found in the bloodstream.
/// </summary>
public sealed partial class CauseInfectionDeadEntityEffectSystem : EntityEffectSystem<MobStateComponent, CauseInfectionDead>
{
    [Dependency] private InfectionDeadSystem _infectionDead = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<CauseInfectionDead> args)
    {
        if (!_infectionDead.IsInfectionPossible(entity))
            return;

        var component = new InfectionDeadComponent(args.Effect.StrainData);

        // Try to find InfectionDeadStrainData from the entity's bloodstream solution,
        // as it may carry strain-specific data as ReagentData.
        if (TryComp<BloodstreamComponent>(entity, out var bloodstream)
            && bloodstream.BloodSolution is { } bloodSolutionEntity)
        {
            foreach (var reagent in bloodSolutionEntity.Comp.Solution.Contents)
            {
                var dataList = reagent.Reagent.Data;
                if (dataList == null)
                    continue;

                var infectionData = dataList.OfType<InfectionDeadStrainData>().FirstOrDefault();
                if (infectionData != null)
                {
                    component.StrainData = infectionData;
                    break;
                }
            }
        }

        AddComp(entity, component);
    }
}
