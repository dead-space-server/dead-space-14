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
            _popup.PopupEntity("Нет биологического образца для сканирования.", uid, args.User);
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
            _popup.PopupEntity("Начинается сканирование...", uid, args.User);
            args.Handled = true;
        }
    }

    private void OnScanComplete(Entity<AlcoTesterComponent> uid, ref AlcoScanDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Target is not EntityUid target)
            return;

        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            _popup.PopupEntity("Нет биологического образца (крови).", uid, ev.User);
            return;
        }

        Entity<SolutionComponent>? solEnt = null;
        if (!_solutions.ResolveSolution(target, bloodstream.ChemicalSolutionName,
            ref solEnt, out var solution))
        {
            _popup.PopupEntity("Химический раствор не найден.", uid, ev.User);
            return;
        }

        var ethanolProto = new ReagentId(uid.Comp.EthanolId, null);
        var ethanol = solution.GetReagentQuantity(ethanolProto).Float();

        var verdict = ethanol switch
        {
            < 0.01f => "Трезв(а).",
            < 0.10f => "Лёгкое опьянение.",
            < 0.30f => "Среднее опьянение.",
            _ => "Сильное опьянение!"
        };

        var message = $"Этанол: {ethanol:0.###} ед. | {verdict}";
        _popup.PopupEntity(message, uid, ev.User);
        ev.Handled = true;
    }
}
