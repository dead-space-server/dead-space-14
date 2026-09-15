using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.NicotineAddiction;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.NicotineAddiction;

public sealed class NicotineAddictionSystem : EntitySystem
{
    private const string NicotineReagentId = "Nicotine";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Cigarettes add less nicotine than one metabolism tick removes, so it must be detected every frame.
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NicotineAddictionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (HasNicotine(uid, comp))
            {
                comp.LastNicotineInBloodTime = now;
                if (comp.DeprivationPopupShown || comp.DeprivationShakeActive)
                {
                    comp.DeprivationPopupShown = false;
                    comp.DeprivationShakeActive = false;
                    comp.DeprivationPopupShownAt = TimeSpan.Zero;
                    Dirty(uid, comp);
                }
                continue;
            }

            if (comp.LastNicotineInBloodTime == TimeSpan.Zero)
            {
                comp.LastNicotineInBloodTime = now;
                continue;
            }

            var dt = now - comp.LastNicotineInBloodTime;

            if (dt >= comp.DeprivationPopupDelay && !comp.DeprivationPopupShown)
            {
                _popup.PopupEntity(
                    Loc.GetString("nicotine-addiction-deprivation-popup"),
                    uid,
                    uid,
                    PopupType.SmallCaution);
                comp.DeprivationPopupShown = true;
                comp.DeprivationPopupShownAt = now;
            }

            if (comp.DeprivationPopupShown
                && !comp.DeprivationShakeActive
                && now >= comp.DeprivationPopupShownAt + comp.PopupToShakeDelay)
            {
                comp.DeprivationShakeActive = true;
                Dirty(uid, comp);
            }
        }
    }

    private bool HasNicotine(EntityUid uid, NicotineAddictionComponent comp)
    {
        if (!_solutionContainer.TryGetSolution(uid, "bloodstream", out var solution))
            return false;

        var q = solution.Value.Comp.Solution.GetReagentQuantity(new ReagentId(NicotineReagentId, null)).Float();
        return q >= comp.RequiredNicotineLevel;
    }
}
