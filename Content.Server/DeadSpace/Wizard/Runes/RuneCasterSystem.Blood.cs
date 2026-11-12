// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private const float BloodCheckInterval = 0.5f;
    private const float BloodSearchRange = 0.5f;

    private float _bloodCheck;

    private void UpdateBlood(float frameTime)
    {
        _bloodCheck -= frameTime;
        if (_bloodCheck > 0f)
            return;

        _bloodCheck = BloodCheckInterval;
        var query = EntityQueryEnumerator<RuneCasterComponent>();
        while (query.MoveNext(out var uid, out var caster))
        {
            var inBlood = FindBlood(uid, caster) != null;
            if (inBlood == caster.InBlood)
                continue;

            caster.InBlood = inBlood;
            Dirty(uid, caster);
            _popup.PopupEntity(Loc.GetString(inBlood ? "rune-blood-awaken" : "rune-blood-fade"), uid, uid, inBlood ? PopupType.Medium : PopupType.Small);
        }
    }

    private Entity<SolutionComponent>? FindBlood(EntityUid uid, RuneCasterComponent caster)
    {
        foreach (var puddle in _lookup.GetEntitiesInRange<PuddleComponent>(Transform(uid).Coordinates, BloodSearchRange))
        {
            if (!_solutions.ResolveSolution(puddle.Owner, puddle.Comp.SolutionName, ref puddle.Comp.Solution, out var solution))
                continue;

            var blood = FixedPoint2.Zero;
            foreach (var reagent in caster.BloodReagents)
                blood += solution.GetTotalPrototypeQuantity(reagent);

            if (blood >= FixedPoint2.New(1))
                return puddle.Comp.Solution;
        }

        return null;
    }

    private void SpendBlood(EntityUid uid, RuneCasterComponent caster, RuneComposition composition)
    {
        if (composition.Element.ID != caster.BloodRune.Id && composition.Second?.ID != caster.BloodRune.Id)
            return;

        if (FindBlood(uid, caster) is not { } puddle)
            return;

        var left = FixedPoint2.New(caster.BloodCost);
        foreach (var reagent in caster.BloodReagents)
        {
            if (left <= FixedPoint2.Zero)
                break;

            left -= _solutions.RemoveReagent(puddle, reagent, left);
        }

        _bloodCheck = 0f;
    }
}
