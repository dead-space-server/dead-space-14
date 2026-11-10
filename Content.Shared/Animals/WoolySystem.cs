using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Animals;

/// <summary>
///     Gives ability to produce fiber reagents;
///     produces endlessly if the owner has no SatiationComponent.
/// </summary>
public sealed class WoolySystem : EntitySystem
{
    [Dependency] private readonly SatiationSystem _satiation = default!;
    // DS14-start: animals of the husbandry module pay with their AnimalNeeds
    [Dependency] private readonly SharedAnimalNeedsSystem _animalNeeds = default!;
    [Dependency] private readonly INetManager _net = default!;
    // DS14-end
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WoolyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WoolyComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void OnMapInit(EntityUid uid, WoolyComponent component, MapInitEvent args)
    {
        component.NextGrowth = _timing.CurTime + component.GrowthDelay;
    }

    private void OnEntRemoved(Entity<WoolyComponent> entity, ref EntRemovedFromContainerMessage args)
    {
        // Make sure the removed entity was our contained solution
        if (entity.Comp.Solution == null || args.Entity != entity.Comp.Solution.Value.Owner)
            return;

        // Clear our cached reference to the solution entity
        entity.Comp.Solution = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WoolyComponent>();
        while (query.MoveNext(out var uid, out var wooly))
        {
            if (_timing.CurTime < wooly.NextGrowth)
                continue;

            wooly.NextGrowth += wooly.GrowthDelay;

            if (_mobState.IsDead(uid))
                continue;

            if (!_solutionContainer.ResolveSolution(uid, wooly.SolutionName, ref wooly.Solution, out var solution))
                continue;

            if (solution.AvailableVolume == 0)
                continue;

            // DS14-start
            if (TryComp<AnimalNeedsComponent>(uid, out var animalNeeds))
            {
                if (_net.IsClient || !_animalNeeds.TryConsumeSatiety((uid, animalNeeds), wooly.HungerUsage, animalNeeds.Satiety.SeekBelow))
                    continue;

                _solutionContainer.TryAddReagent(wooly.Solution.Value, wooly.ReagentId, wooly.Quantity, out _);
                continue;
            }
            // DS14-end

            // Actually there is food digestion so no problem with instant reagent generation "OnFeed"
            if (TryComp<SatiationComponent>(uid, out var satiation))
            {
                // Is there enough nutrition to produce reagent?
                if (_satiation.IsValueInRange((uid, satiation), SatiationSystem.Hunger, above: wooly.MinHungerThreshold, hypotheticalValueDelta: -wooly.HungerUsage))
                {
                    continue;
                }

                _satiation.ModifyValue((uid, satiation), SatiationSystem.Hunger, -wooly.HungerUsage);
            }

            _solutionContainer.TryAddReagent(wooly.Solution.Value, wooly.ReagentId, wooly.Quantity, out _);
        }
    }
}
