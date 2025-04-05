using Content.Server.Cuffs;
using Content.Server.DeadSpace.Armutant.Abilities.Components.FistAbilities;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs.Components;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Network;

namespace Content.Server.DeadSpace.Armutant.Abilities.Fist;

public sealed class FistMendSelfSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly CuffableSystem _cuffable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FistMendSelfComponent, FistMendSelfToggleEvent>(OnShrapnelActive);
        SubscribeLocalEvent<FistMendSelfComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<FistMendSelfComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, FistMendSelfComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionFistMendSelfEntity, component.ActionToggleFistMendSelf, uid);
    }

    private void OnComponentShutdown(EntityUid uid, FistMendSelfComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionFistMendSelfEntity);
    }

    private void OnShrapnelActive(EntityUid uid, FistMendSelfComponent component, FistMendSelfToggleEvent args)
    {
        if (TryComp<CuffableComponent>(uid, out var cuffs) && cuffs.Container.ContainedEntities.Count > 0)
        {
            _cuffable.Uncuff(uid, cuffs.LastAddedCuffs, cuffs.LastAddedCuffs);
            if (_net.IsServer && component.SelfEffect is not null)
            {
                var effect = Spawn(component.SelfEffect, Transform(uid).Coordinates);
                _transformSystem.SetParent(effect, uid);
            }
            args.Handled = true;
            return;
        }

        var reagents = new List<(string, FixedPoint2)>()
        {
            ("Shrapnel", 10f)
        };

        if (TryInjectReagents(uid, reagents))
        {
            _popup.PopupEntity(Loc.GetString("mendself-activated"), uid, uid);
        }
        else
            return;

        args.Handled = true;
    }

    public bool TryInjectReagents(EntityUid uid, List<(string, FixedPoint2)> reagents)
    {
        var solution = new Shared.Chemistry.Components.Solution();
        foreach (var reagent in reagents)
        {
            solution.AddReagent(reagent.Item1, reagent.Item2);
        }

        if (!_solution.TryGetInjectableSolution(uid, out var targetSolution, out var _))
            return false;

        if (!_solution.TryAddSolution(targetSolution.Value, solution))
            return false;

        return true;
    }
}
