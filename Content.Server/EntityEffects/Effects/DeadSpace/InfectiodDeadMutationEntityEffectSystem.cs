// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Necromorphs.InfectionDead;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;

namespace Content.Server.EntityEffects.Effects.DeadSpace;

/// <summary>
/// Mutates the necromorph virus on this entity.
/// </summary>
public sealed partial class InfectiodDeadMutationEntityEffectSystem : EntityEffectSystem<NecromorfComponent, InfectiodDeadMutation>
{
    [Dependency] private NecromorfSystem _necromorf = default!;

    protected override void Effect(Entity<NecromorfComponent> entity, ref EntityEffectEvent<InfectiodDeadMutation> args)
    {
        _necromorf.MutateVirus(entity, args.Effect.MutationStrength, args.Effect.IsStableMutation);
    }
}
