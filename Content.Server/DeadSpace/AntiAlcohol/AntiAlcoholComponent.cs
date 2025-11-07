using System;
using Content.Shared.Medical;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.AntiAlcohol
{
    public sealed class AntiAlcoholSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
        [Dependency] private readonly VomitSystem _vomit = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedContainerSystem _containers = default!;

        public override void Update(float frameTime)
        {
            var now = _timing.CurTime;

            var q = EntityQueryEnumerator<AntiAlcoholWatcherComponent>();
            while (q.MoveNext(out var implantUid, out var comp))
            {
                if (now < comp.NextAllowedVomitAt)
                    continue;

                if (!_containers.TryGetContainingContainer(implantUid, out var container))
                    continue;

                var host = container.Owner;

                if (!TryComp<BloodstreamComponent>(host, out var blood))
                    continue;

                var chemName = blood.ChemicalSolutionName;

                Entity<SolutionComponent>? solEnt = null;
                if (!_solutions.ResolveSolution(host, chemName, ref solEnt, out var solution))
                    continue;

                var ethanolQty = solution.GetReagentQuantity(new ReagentId("Ethanol", null));
                if (ethanolQty.Float() < comp.Threshold)
                    continue;

                if (comp.Probability < 1f && _random.NextFloat() > comp.Probability)
                    continue;

                _vomit.Vomit(host);

                comp.NextAllowedVomitAt = now + TimeSpan.FromSeconds(comp.CooldownSeconds);
            }
        }
    }
}