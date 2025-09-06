// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Content.Shared.DeadSpace.Asthma.Components;
using Content.Shared.DeadSpace.Asthma;
using Content.Shared.Interaction.Events;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Server.Inventory;
using Content.Shared.Nutrition.EntitySystems;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Body.Components;
using Content.Server.Popups;

namespace Content.Server.DeadSpace.Asthma;

public sealed class InhalerSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly AsthmaSystem _asthma = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InhalerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<InhalerComponent, UseInhalerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<InhalerComponent, UseInHandEvent>(OnUseInHand, before: [typeof(ServerInventorySystem)], after: [typeof(OpenableSystem)]);
    }

    private void OnUseInHand(EntityUid uid, InhalerComponent component, UseInHandEvent args)
    {
        if (!TryUseInhaler(uid, args.User, args.User, component))
            _popup.PopupEntity("Не удаётся применить ингалятор.", args.User, args.User);
    }

    private void OnAfterInteract(EntityUid uid, InhalerComponent component, AfterInteractEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (!TryUseInhaler(uid, target, args.User, component))
            _popup.PopupEntity("Не удаётся применить ингалятор.", args.User, args.User);

    }

    private bool TryUseInhaler(EntityUid uid, EntityUid target, EntityUid user, InhalerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!CanBeUsed(uid, target, user, component))
            return false;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(component.UseDuration), new UseInhalerDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 1f
        });

        return true;
    }

    private bool CanBeUsed(EntityUid uid, EntityUid target, EntityUid user, InhalerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!TryComp<BodyComponent>(target, out var body))
            return false;

        if (!_body.TryGetBodyOrganEntityComps<LungComponent>((target, body), out _))
            return false;

        if (!_ingestion.HasMouthAvailable(target, user))
            return false;

        if (!_solutionContainer.TryGetSolution(uid, component.SolutionName, out _, out var solution))
            return false;

        if (solution.Volume <= 0)
            return false;

        return true;
    }

    private void OnDoAfter(EntityUid uid, InhalerComponent component, UseInhalerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;

        if (!CanBeUsed(uid, target, args.User, component))
            return;

        if (component.Sound != null)
            _audio.PlayPvs(component.Sound, uid);

        if (!TryComp<SolutionContainerManagerComponent>(uid, out var container))
            return;

        if (!_solutionContainer.TryGetSolution(uid, component.SolutionName, out var sol, out var solution))
            return;

        bool treatAstma = false;

        foreach (var reagent in solution.Contents)
        {
            if (component.AsthmaMedicineWhitelist.Contains(reagent.Reagent.Prototype))
            {
                treatAstma = true;
                break;
            }
        }

        var drained = _solutionContainer.SplitSolution(sol.Value, component.Quantity);

        if (treatAstma)
            _asthma.ResetAsthma(target);

        if (!_solutionContainer.TryGetInjectableSolution(target, out var injectable, out _))
            return;

        _solutionContainer.TryTransferSolution(injectable.Value, drained, component.Quantity);

        args.Handled = true;
    }
}
