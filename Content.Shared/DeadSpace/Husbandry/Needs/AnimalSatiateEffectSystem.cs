// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Body;

namespace Content.Shared.DeadSpace.Husbandry.Needs;

/// <summary>
/// The same effect that feeds and waters a human (<see cref="Satiate"/>, in the metabolism of the reagent), for an
/// animal of the husbandry module: the animal has <see cref="AnimalNeedsComponent"/> instead of the vanilla
/// satiation, so the effect raises its satiety or hydration. The reagents get to the effect by the usual way: the food
/// and the drink go to the stomach, the metabolizer of the stomach works them off bit by bit and applies their effects,
/// the harmful ones included. Nothing here is special for animals except where the points go.
/// </summary>
public sealed class AnimalSatiateEffectSystem : EntityEffectSystem<AnimalNeedsComponent, Satiate>
{
    [Dependency] private readonly SharedAnimalNeedsSystem _needs = default!;

    protected override void Effect(Entity<AnimalNeedsComponent> entity, ref EntityEffectEvent<Satiate> args)
    {
        var amount = args.Effect.Factor * args.Scale;

        // The names of the vanilla satiation types.
        switch (args.Effect.SatiationType.Id)
        {
            case "Hunger":
                _needs.ModifySatiety(entity, amount);
                break;
            case "Thirst":
                _needs.ModifyHydration(entity, amount);
                break;
        }
    }
}
