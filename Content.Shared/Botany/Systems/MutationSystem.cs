using JetBrains.Annotations;
using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Handles plant mutations, including random mutation effects, crossbreeding, and
/// inheritance of plant properties and traits from pollen.
/// </summary>
public sealed partial class PlantMutationSystem : EntitySystem
{
    private static readonly ProtoId<RandomPlantMutationListPrototype> RandomPlantMutations = "RandomPlantMutations";
    private RandomPlantMutationListPrototype _randomMutations = default!;

    // DS14-start
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly PlantSystem _plant = default!;
    [Dependency] private readonly PlantTraySystem _plantTray = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;

    private EntityQuery<PlantChemicalsComponent> _chemicalsQuery;
    private EntityQuery<PlantComponent> _plantQuery;
    // DS14-end

    public override void Initialize()
    {
        // DS14-start: initialize EntityQuery explicitly on the current engine.
        base.Initialize();
        _randomMutations = _prototypeManager.Index(RandomPlantMutations);
        _chemicalsQuery = GetEntityQuery<PlantChemicalsComponent>();
        _plantQuery = GetEntityQuery<PlantComponent>();
        // DS14-end
    }

    /// <summary>
    /// For each random mutation, see if it occurs on this plant this check.
    /// </summary>
    [PublicAPI]
    public void CheckRandomMutations(Entity<PlantComponent?> ent, float severity)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var mutation in _randomMutations.Mutations)
        {
            if (Random(Math.Min(mutation.BaseOdds * severity, 1.0f))) // DS14
            {
                if (mutation.AppliesToPlant)
                    _entityEffects.TryApplyEffect(ent, mutation.Effect);

                // Stat adjustments do not persist by being an attached effect, they just change the stat.
                if (mutation.Persists && ent.Comp.Mutations.All(m => m.Name != mutation.Name))
                    ent.Comp.Mutations.Add(mutation);
            }
        }
    }

    /// <summary>
    /// Replaces the current plant species with a new one from prototype,
    /// preserving lifecycle state.
    /// </summary>
    [PublicAPI]
    public void SpeciesChange(Entity<PlantDataComponent?> oldPlant, EntProtoId newPlantProto)
    {
        if (!Resolve(oldPlant, ref oldPlant.Comp, false))
            return;

        if (oldPlant.Comp.MutationPrototypes.Count == 0)
            return;

        if (!_net.IsServer)
            return;

        // Clone state via snapshot and apply to new plant.
        var snapshot = _botany.ClonePlantSnapshotData(oldPlant.Owner, cloneLifecycle: true);
        if (snapshot == null)
            return;

        var newPlantUid = SpawnAtPosition(newPlantProto, Transform(oldPlant.Owner).Coordinates);
        _botany.ApplyPlantSnapshotData(snapshot, newPlantUid, cloneLifecycle: true);
        _botany.DeletePlantSnapshot(snapshot);

        ChemicalsSpeciesChange(newPlantUid, newPlantProto);

        if (_plant.TryGetTray(oldPlant.Owner, out var trayEnt))
            _plantTray.PlantingPlantInTray(trayEnt.AsNullable(), newPlantUid);
        else
            _plant.PlantingPlant(newPlantUid);

        _plant.ForceUpdate(newPlantUid);
        QueueDel(oldPlant);
    }

    private void ChemicalsSpeciesChange(EntityUid plantUid, EntProtoId plantProto)
    {
        if (!_botany.TryGetPlantComponent<PlantChemicalsComponent>(null, plantProto, out var newPlantChemicals)
            || !_chemicalsQuery.TryComp(plantUid, out var oldPlantChemicals)) // DS14
            return;

        var oldPlant = oldPlantChemicals.Chemicals;
        var newPlant = newPlantChemicals.Chemicals;

        // Adding the new chemicals from the new species.
        foreach (var otherChem in newPlant)
        {
            oldPlant.TryAdd(otherChem.Key, otherChem.Value);
        }

        // Removing the inherent chemicals from the old species. Leaving mutated/crossbred ones intact.
        foreach (var originalChem in oldPlant)
        {
            if (!newPlant.ContainsKey(originalChem.Key) && originalChem.Value.Inherent)
                oldPlant.Remove(originalChem.Key);
        }

        Dirty(plantUid, oldPlantChemicals);
    }

    /// <summary>
    /// Combines mutations from the pollen and target plants.
    /// </summary>
    [PublicAPI]
    public void CrossMutations(EntityUid pollenPlant, EntProtoId? pollenProtoId, EntityUid targetPlant)
    {
        if (!_botany.TryGetPlantComponent<PlantComponent>(pollenPlant, pollenProtoId, out var pollenCore) ||
            !_plantQuery.TryComp(targetPlant, out var targetCore)) // DS14
            return;

        // LINQ Explanation
        // For the list of mutation effects on both plants, use a 50% chance to pick each one.
        // Union all of the chosen mutations into one list, and pick ones with a Distinct (unique) name.
        targetCore.Mutations = targetCore.Mutations.Where(_ => Random(0.5f)).Union(pollenCore.Mutations.Where(_ => Random(0.5f))).DistinctBy(m => m.Name).ToList(); // DS14

        // Hybrids have a high chance of being seedless. Balances very
        // effective hybrid crossings.
        if (pollenProtoId != null
            && pollenProtoId != MetaData(targetPlant).EntityPrototype?.ID
            && Random(0.7f)) // DS14
        {
            EnsureComp<PlantTraitSeedlessComponent>(targetPlant);
        }
    }

    /// <summary>
    /// Combines chemical properties from the pollen and target plants.
    /// </summary>
    [PublicAPI]
    public void CrossChemicals(EntityUid uid, ref Dictionary<ProtoId<ReagentPrototype>, PlantChemQuantity> val, Dictionary<ProtoId<ReagentPrototype>, PlantChemQuantity> other)
    {
        // Go through chemicals from the pollen in swab
        foreach (var otherChem in other)
        {
            // if both have same chemical, randomly pick potency ratio from the two.
            if (val.TryGetValue(otherChem.Key, out var value))
            {
                val[otherChem.Key] = Random(0.5f) ? otherChem.Value : value; // DS14
            }
            // if target plant doesn't have this chemical, has 50% chance to add it.
            else
            {
                if (Random(0.5f)) // DS14
                {
                    var fixedChem = otherChem.Value;
                    fixedChem.Inherent = false;
                    val.Add(otherChem.Key, fixedChem);
                }
            }
        }

        // if the target plant has chemical that the pollen in swab does not, 50% chance to remove it.
        foreach (var thisChem in val)
        {
            if (!other.ContainsKey(thisChem.Key))
            {
                if (Random(0.5f)) // DS14
                {
                    if (val.Count > 1)
                    {
                        val.Remove(thisChem.Key);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Combines gas properties from the pollen and target plants.
    /// </summary>
    [PublicAPI]
    public void CrossGasses(EntityUid uid, ref Dictionary<Gas, float> val, Dictionary<Gas, float> other)
    {
        // Go through gasses from the pollen in swab
        foreach (var otherGas in other)
        {
            // if both have same gas, randomly pick ammount from the two.
            if (val.TryGetValue(otherGas.Key, out var value))
            {
                val[otherGas.Key] = Random(0.5f) ? otherGas.Value : value; // DS14
            }
            // if target plant doesn't have this gas, has 50% chance to add it.
            else
            {
                if (Random(0.5f)) // DS14
                {
                    val.Add(otherGas.Key, otherGas.Value);
                }
            }
        }
        // if the target plant has gas that the pollen in swab does not, 50% chance to remove it.
        foreach (var thisGas in val)
        {
            if (!other.ContainsKey(thisGas.Key))
            {
                if (Random(0.5f)) // DS14
                {
                    val.Remove(thisGas.Key);
                }
            }
        }
    }

    /// <summary>
    /// Selects a floating value from the plant or pollen.
    /// </summary>
    [PublicAPI]
    // DS14-start
    public void CrossFloat(ref float val, float other)
    {
        val = Random(0.5f) ? val : other;
    }
    // DS14-end

    /// <summary>
    /// Selects an integer value from the plant or pollen.
    /// </summary>
    [PublicAPI]
    // DS14-start
    public void CrossInt(ref int val, int other)
    {
        val = Random(0.5f) ? val : other;
    }
    // DS14-end

    /// <summary>
    /// Selects a Boolean value from the plant or pollen.
    /// </summary>
    [PublicAPI]
    // DS14-start
    public void CrossBool(ref bool val, bool other)
    {
        val = Random(0.5f) ? val : other;
    }
    // DS14-end

    /// <summary>
    /// Crosses plant trait components from pollen to the target plant.
    /// </summary>
    [PublicAPI]
    public void CrossTrait(EntityUid val, EntityUid pollenData)
    {
        foreach (var component in AllComps(pollenData))
        {
            if (component is not PlantTraitsComponent)
                continue;

            if (HasComp(val, component.GetType()))
                continue;

            if (Random(0.5f)) // DS14
                AddComp(val, _serialization.CreateCopy(component, notNullableOverride: true));
        }
    }

    // DS14-start - plant mutations are authoritative and each check needs an independent roll.
    private bool Random(float p)
    {
        if (!_net.IsServer)
            return false;

        return _random.Prob(p);
    }
    // DS14-end
}
