// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Necromorphs.InfectionDead;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;

namespace Content.Server.EntityEffects.Effects.DeadSpace;

/// <summary>
/// Cures the necromorph infection on this entity.
/// </summary>
public sealed partial class CureInfectionDeadEntityEffectSystem : EntityEffectSystem<InfectionDeadComponent, CureInfectionDead>
{
    [Dependency] private InfectionDeadSystem _infectionDead = default!;

    protected override void Effect(Entity<InfectionDeadComponent> entity, ref EntityEffectEvent<CureInfectionDead> args)
    {
        _infectionDead.TryCure(entity);
    }
}
