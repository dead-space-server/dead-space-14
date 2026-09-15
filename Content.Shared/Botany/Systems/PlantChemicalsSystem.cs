using JetBrains.Annotations;
using System.Linq;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Timing;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Handles the chemicals of a plant.
/// </summary>
public sealed partial class PlantChemicalsSystem : EntitySystem
{
    // DS14-start: current engine uses explicit event subscriptions.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantChemicalsComponent, PlantCrossPollinateEvent>(OnCrossPollinate);
    }
    // DS14-end

    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly PlantMutationSystem _mutation = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    // DS14-end

    private void OnCrossPollinate(Entity<PlantChemicalsComponent> ent, ref PlantCrossPollinateEvent args)
    {
        if (!_botany.TryGetPlantComponent<PlantChemicalsComponent>(args.PollenData, args.PollenProtoId, out var pollenData))
            return;

        _mutation.CrossChemicals(ent, ref ent.Comp.Chemicals, pollenData.Chemicals);
        Dirty(ent);
    }

    /// <summary>
    /// Adds a random chemical to the plant chemicals.
    /// </summary>
    [PublicAPI]
    public void MutateRandomChemical(Entity<PlantChemicalsComponent?> ent, WeightedRandomFillSolutionPrototype randomChems)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));
        // DS14-start - weighted helpers only accept IRobustRandom on the current engine.
        var randomFill = SharedRandomExtensions.Pick(
            randomChems.Fills.ToDictionary(fill => fill, fill => fill.Weight),
            random);
        var chemicalId = randomFill.Reagents[(int)(random.NextSingle() * randomFill.Reagents.Count)];
        var quantity = randomFill.Quantity;
        var amount = FixedPoint2.Max(random.NextSingle() * quantity, FixedPoint2.Epsilon);
        // DS14-end
        var seedChemQuantity = new PlantChemQuantity();
        if (ent.Comp.Chemicals.TryGetValue(chemicalId, out var value))
        {
            seedChemQuantity.Min = value.Min;
            seedChemQuantity.Max = value.Max + amount;
        }
        else
        {
            //Set the minimum to a fifth of the quantity to give some level of bad luck protection
            seedChemQuantity.Min = FixedPoint2.Clamp(quantity / 5f, FixedPoint2.Epsilon, 1f);
            seedChemQuantity.Max = seedChemQuantity.Min + amount;
            seedChemQuantity.Inherent = false;
        }

        var potencyDivisor = 100f / seedChemQuantity.Max;
        seedChemQuantity.PotencyDivisor = (float)potencyDivisor;
        ent.Comp.Chemicals[chemicalId] = seedChemQuantity;
        Dirty(ent);
    }
}
