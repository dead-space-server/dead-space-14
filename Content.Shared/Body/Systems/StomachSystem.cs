using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

<<<<<<< ours
namespace Content.Shared.Body.Systems
||||||| base
namespace Content.Shared.Body.Systems;

public sealed class StomachSystem : EntitySystem
=======
namespace Content.Shared.Body.Systems;

public sealed partial class StomachSystem : EntitySystem
>>>>>>> theirs
{
<<<<<<< ours
    public sealed class StomachSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
||||||| base
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
=======
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
>>>>>>> theirs

        public const string DefaultSolutionName = "stomach";

<<<<<<< ours
        public override void Initialize()
        {
            SubscribeLocalEvent<StomachComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<StomachComponent, EntityUnpausedEvent>(OnUnpaused);
            SubscribeLocalEvent<StomachComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
            SubscribeLocalEvent<StomachComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
        }
||||||| base
    public bool CanTransferSolution(
        EntityUid uid,
        Solution solution,
        StomachComponent? stomach = null,
        SolutionContainerManagerComponent? solutions = null)
    {
        return Resolve(uid, ref stomach, ref solutions, logMissing: false)
            && _solutionContainerSystem.ResolveSolution((uid, solutions), DefaultSolutionName, ref stomach.Solution, out var stomachSolution)
            // TODO: For now no partial transfers. Potentially change by design
            && stomachSolution.CanAddSolution(solution);
    }
=======
    public bool CanTransferSolution(Entity<StomachComponent?, SolutionManagerComponent?> entity, Solution solution)
    {
        return Resolve(entity, ref entity.Comp1, logMissing: false)
            && _solutionContainerSystem.ResolveSolution((entity, entity.Comp2), DefaultSolutionName, ref entity.Comp1.Solution, out var stomachSolution)
            // TODO: For now no partial transfers. Potentially change by design
            && stomachSolution.CanAddSolution(solution);
    }
>>>>>>> theirs

<<<<<<< ours
        private void OnMapInit(Entity<StomachComponent> ent, ref MapInitEvent args)
        {
            ent.Comp.NextUpdate = _gameTiming.CurTime + ent.Comp.AdjustedUpdateInterval;
        }

        private void OnUnpaused(Entity<StomachComponent> ent, ref EntityUnpausedEvent args)
        {
            ent.Comp.NextUpdate += args.PausedTime;
        }

        private void OnEntRemoved(Entity<StomachComponent> ent, ref EntRemovedFromContainerMessage args)
        {
            // Make sure the removed entity was our contained solution
            if (ent.Comp.Solution is not { } solution || args.Entity != solution.Owner)
                return;

            // Cleared our cached reference to the solution entity
            ent.Comp.Solution = null;
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<StomachComponent, OrganComponent, SolutionContainerManagerComponent>();
            while (query.MoveNext(out var uid, out var stomach, out var organ, out var sol))
            {
                if (_gameTiming.CurTime < stomach.NextUpdate)
                    continue;

                stomach.NextUpdate += stomach.AdjustedUpdateInterval;

                // DS14-start
                if (stomach.ReagentDeltas.Count == 0)
                    continue;
                // DS14-end

                // Get our solutions
                if (!_solutionContainerSystem.ResolveSolution((uid, sol), DefaultSolutionName, ref stomach.Solution, out var stomachSolution))
                    continue;

                if (organ.Body is not { } body || !_solutionContainerSystem.TryGetSolution(body, stomach.BodySolutionName, out var bodySolution))
                    continue;

                var transferSolution = new Solution();

                // DS14-start
                var retainedCount = 0;
                for (var i = 0; i < stomach.ReagentDeltas.Count; i++)
                {
                    var delta = stomach.ReagentDeltas[i];
                    delta.Increment(stomach.AdjustedUpdateInterval);
                    if (delta.Lifetime > stomach.DigestionDelay)
                    {
                        if (stomachSolution.TryGetReagent(delta.ReagentQuantity.Reagent, out var reagent))
                        {
                            if (reagent.Quantity > delta.ReagentQuantity.Quantity)
                                reagent = new(reagent.Reagent, delta.ReagentQuantity.Quantity);

                            stomachSolution.RemoveReagent(reagent);
                            transferSolution.AddReagent(reagent);
                        }

                        continue;
                    }

                    stomach.ReagentDeltas[retainedCount++] = delta;
                }

                if (retainedCount < stomach.ReagentDeltas.Count)
                    stomach.ReagentDeltas.RemoveRange(retainedCount, stomach.ReagentDeltas.Count - retainedCount);
                // DS14-end

                _solutionContainerSystem.UpdateChemicals(stomach.Solution.Value);

                // Transfer everything to the body solution!
                _solutionContainerSystem.TryAddSolution(bodySolution.Value, transferSolution);
            }
        }

        private void OnApplyMetabolicMultiplier(Entity<StomachComponent> ent, ref ApplyMetabolicMultiplierEvent args)
        {
            ent.Comp.UpdateIntervalMultiplier = args.Multiplier;
        }

        public bool CanTransferSolution(
            EntityUid uid,
            Solution solution,
            StomachComponent? stomach = null,
            SolutionContainerManagerComponent? solutions = null)
        {
            return Resolve(uid, ref stomach, ref solutions, logMissing: false)
                && _solutionContainerSystem.ResolveSolution((uid, solutions), DefaultSolutionName, ref stomach.Solution, out var stomachSolution)
                // TODO: For now no partial transfers. Potentially change by design
                && stomachSolution.CanAddSolution(solution);
        }
||||||| base
    public bool TryTransferSolution(
        EntityUid uid,
        Solution solution,
        StomachComponent? stomach = null,
        SolutionContainerManagerComponent? solutions = null)
    {
        if (!Resolve(uid, ref stomach, ref solutions, logMissing: false)
            || !_solutionContainerSystem.ResolveSolution((uid, solutions), DefaultSolutionName, ref stomach.Solution)
            || !CanTransferSolution(uid, solution, stomach, solutions))
        {
            return false;
        }

        _solutionContainerSystem.TryAddSolution(stomach.Solution.Value, solution);
=======
    public bool TryTransferSolution(Entity<StomachComponent?, SolutionManagerComponent?> entity, Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false)
            || !_solutionContainerSystem.ResolveSolution((entity, entity.Comp2), DefaultSolutionName, ref entity.Comp1.Solution)
            || !CanTransferSolution(entity, solution))
            return false;
>>>>>>> theirs

<<<<<<< ours
        public bool TryTransferSolution(
            EntityUid uid,
            Solution solution,
            StomachComponent? stomach = null,
            SolutionContainerManagerComponent? solutions = null)
        {
            if (!Resolve(uid, ref stomach, ref solutions, logMissing: false)
                || !_solutionContainerSystem.ResolveSolution((uid, solutions), DefaultSolutionName, ref stomach.Solution)
                || !CanTransferSolution(uid, solution, stomach, solutions))
            {
                return false;
            }

            _solutionContainerSystem.TryAddSolution(stomach.Solution.Value, solution);
            // Add each reagent to ReagentDeltas. Used to track how long each reagent has been in the stomach
            foreach (var reagent in solution.Contents)
            {
                stomach.ReagentDeltas.Add(new StomachComponent.ReagentDelta(reagent));
            }

            return true;
        }
||||||| base
        return true;
=======
        _solutionContainerSystem.TryAddSolution(entity.Comp1.Solution.Value, solution);
        return true;
>>>>>>> theirs
    }
}
