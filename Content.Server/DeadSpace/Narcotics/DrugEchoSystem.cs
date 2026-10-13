// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Narcotics;
using Content.Shared.FixedPoint;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Narcotics;

/// <summary>
/// Attaches a networked <see cref="DrugEchoComponent"/> to entities whose bloodstream
/// contains any reagent from the "Narcotics" group, so the affected client can apply
/// the drug echo audio effect while high.
/// </summary>
public sealed partial class DrugEchoSystem : EntitySystem
{
    private const string NarcoticsGroup = "Narcotics";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodstreamComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var bloodstream, out _))
        {
            var isDrugged = HasNarcotic(uid, bloodstream);
            if (isDrugged == HasComp<DrugEchoComponent>(uid))
                continue;

            if (isDrugged)
                AddComp<DrugEchoComponent>(uid);
            else
                RemComp<DrugEchoComponent>(uid);
        }
    }

    private bool HasNarcotic(EntityUid uid, BloodstreamComponent bloodstream)
    {
        Entity<SolutionComponent>? solutionEntity = null;
        if (!_solutions.ResolveSolution(uid, bloodstream.BloodSolutionName, ref solutionEntity, out var solution))
            return false;

        foreach (var reagent in solution.Contents)
        {
            if (reagent.Quantity == FixedPoint2.Zero)
                continue;

            var proto = _prototypes.Index<ReagentPrototype>(reagent.Reagent.Prototype);
            if (proto.Group == NarcoticsGroup)
                return true;
        }

        return false;
    }
}