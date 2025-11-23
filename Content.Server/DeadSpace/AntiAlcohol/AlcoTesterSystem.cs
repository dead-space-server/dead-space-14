// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System;
using Content.Server.DeadSpace.AntiAlcohol;
using Content.Shared.DeadSpace.AntiAlcohol;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.AntiAlcohol;

public sealed partial class AlcoTesterSystem : EntitySystem
{
    private static readonly ReagentId EthanolReagentId = new("Ethanol", null);

    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AlcoTesterComponent, AfterInteractEvent>(OnAfterInteract);

        SubscribeLocalEvent<AlcoTesterComponent, AlcoScanDoAfterEvent>(OnScanComplete);
    }

    private void OnAfterInteract(Entity<AlcoTesterComponent> uid, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not EntityUid target || !args.CanReach)
            return;

        if (!HasComp<BloodstreamComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("alco-tester-no-biological-sample"), uid, args.User);
            return;
        }

        var delay = TimeSpan.FromSeconds(uid.Comp.ScanDelaySec);

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, delay, new AlcoScanDoAfterEvent(), uid.Owner)
        {
            Target = target,
            Used = uid.Owner,
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
        }
    }

    private void OnScanComplete(Entity<AlcoTesterComponent> uid, ref AlcoScanDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Target is not EntityUid target)
            return;

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            _popup.PopupEntity(Loc.GetString("alco-tester-no-blood-sample"), uid, ev.User);
            return;
        }

        Entity<SolutionComponent>? solEnt = null;
        if (!_solutions.ResolveSolution(target, bloodstream.ChemicalSolutionName,
            ref solEnt, out var solution))
        {
            _popup.PopupEntity(Loc.GetString("alco-tester-chemical-solution-not-found"), uid, ev.User);
            return;
        }

        var ethanol = solution.GetReagentQuantity(EthanolReagentId).Float();

        var verdictKey = ethanol switch
        {
            < 0.01f => "alcohol-verdict-sober",
            < 0.10f => "alcohol-verdict-light",
            < 0.30f => "alcohol-verdict-medium",
            _ => "alcohol-verdict-strong"
        };
        var verdict = Loc.GetString(verdictKey);

        var ethanolStr = ethanol.ToString("0.###");
        _popup.PopupEntity(Loc.GetString("alco-tester-result", ("ethanol", ethanolStr), ("verdict", verdict)), uid, ev.User);
        ev.Handled = true;
    }
}
