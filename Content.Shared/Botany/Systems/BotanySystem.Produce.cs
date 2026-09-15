using JetBrains.Annotations;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Shared.Botany.Systems;

public sealed partial class BotanySystem
{
    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;
    // DS14-end

    private void OnProduceExamined(Entity<ProduceComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetPlantComponent<PlantComponent>(ent.Comp.PlantData, ent.Comp.PlantProtoId, out var plant))
            return;

        using (args.PushGroup(nameof(ProduceComponent)))
        {
            foreach (var m in plant.Mutations)
            {
                // Don't show mutations that have no effect on produce (sentience)
                if (!m.AppliesToProduce)
                    continue;

                if (m.Description != null)
                    args.PushMarkup(Loc.GetString(m.Description));
            }
        }
    }

    private void ProduceGrown(Entity<ProduceComponent> ent)
    {
        if (!TryGetPlantComponent<PlantComponent>(ent.Comp.PlantData, ent.Comp.PlantProtoId, out var plant)
            || !TryGetPlantComponent<PlantChemicalsComponent>(ent.Comp.PlantData, ent.Comp.PlantProtoId, out var chems))
            return;

        foreach (var mutation in plant.Mutations)
        {
            if (mutation.AppliesToProduce)
                _entityEffects.TryApplyEffect(ent.Owner, mutation.Effect);
        }

        // DS14-start - current engine returns the solution itself and annotates it through the boolean result.
        if (!_solutionContainer.EnsureSolution(ent.Owner, ent.Comp.TargetSolution, out var solution))
            return;
        solution.RemoveAllSolution();
        // DS14-end

        foreach (var (chem, quantity) in chems.Chemicals)
        {
            var amount = quantity.Min;
            if (quantity.PotencyDivisor > 0 && plant.Potency > 0)
                amount += plant.Potency / quantity.PotencyDivisor;
            amount = FixedPoint2.Clamp(amount, quantity.Min, quantity.Max);
            // DS14-start - current engine returns the solution itself.
            solution.MaxVolume += amount;
            solution.AddReagent(chem, amount);
            // DS14-end
        }
    }

    /// <summary>
    /// Spawns a produce item from a plant and produces the produce.
    /// </summary>
    [PublicAPI]
    public void SpawnProduce(Entity<PlantComponent?, PlantDataComponent?> ent, EntityCoordinates position, int amount) // DS14
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, false))
            return;

        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));
        // DS14-start - keep one random sequence so a multi-yield harvest does not clone one identical roll.
        for (var i = 0; i < amount; i++)
        {
            var product = random.Pick(ent.Comp2.ProductPrototypes);
            var entity = PredictedSpawnAtPosition(product, position);
            _randomHelper.RandomOffset(entity, 0.25f, random);

            var produce = EnsureComp<ProduceComponent>(entity);
            produce.PlantProtoId = MetaData(ent.Owner).EntityPrototype!.ID;
            produce.PlantData = ClonePlantSnapshotData(ent.Owner, parent: entity);
            Dirty(entity, produce);
            ProduceGrown((entity, produce));
            _appearance.SetData(entity, ProduceVisuals.Potency, ent.Comp1.Potency);
        }
        // DS14-end
    }
}
