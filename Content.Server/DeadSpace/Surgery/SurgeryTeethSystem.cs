// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed partial class SurgeryTeethSystem : EntitySystem
{
    private const string Pocket = "surgery-teeth";
    private const string CapId = "Teeth";
    private static readonly EntProtoId BiteAction = "ActionSurgeryBitePill";

    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private StomachSystem _stomach = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryTeethComponent, SurgeryBitePillEvent>(OnBite);
        SubscribeLocalEvent<SurgeryTeethComponent, ComponentShutdown>(OnShutdown);
    }

    public bool CanLodge(EntityUid body, EntityUid held)
    {
        if (!HasComp<PillComponent>(held) || Blocked(body))
            return false;

        if (!_containers.TryGetContainer(body, Pocket, out var pocket))
            return true;

        return pocket.ContainedEntities.Count < Max(CompOrNull<SurgeryTeethComponent>(body));
    }

    public bool TryLodge(EntityUid body, EntityUid? held)
    {
        if (held is not { } pill || !HasComp<PillComponent>(pill) || Blocked(body))
            return false;

        var teeth = EnsureComp<SurgeryTeethComponent>(body);
        var pocket = _containers.EnsureContainer<Container>(body, Pocket);
        if (pocket.ContainedEntities.Count >= Max(teeth))
            return false;

        if (!_containers.Insert(pill, pocket))
            return false;

        if (teeth.Bite == null || !Exists(teeth.Bite))
            teeth.Bite = _actions.AddAction(body, BiteAction);

        return true;
    }

    private void OnBite(Entity<SurgeryTeethComponent> ent, ref SurgeryBitePillEvent args)
    {
        if (args.Handled)
            return;

        var pocket = _containers.EnsureContainer<Container>(ent, Pocket);
        if (pocket.ContainedEntities.Count == 0)
        {
            Clear(ent);
            return;
        }

        var pill = pocket.ContainedEntities[0];
        _containers.Remove(pill, pocket);
        Swallow(ent, pill);
        QueueDel(pill);
        args.Handled = true;
        if (pocket.ContainedEntities.Count == 0)
            Clear(ent);
    }

    private void OnShutdown(Entity<SurgeryTeethComponent> ent, ref ComponentShutdown args)
    {
        Clear(ent);
    }

    private void Swallow(EntityUid body, EntityUid pill)
    {
        if (!TryComp<EdibleComponent>(pill, out var edible)
            || !_solutions.TryGetSolution(pill, edible.Solution, out var solution)
            || solution.Value.Comp.Solution.Volume <= 0)
            return;

        var split = _solutions.SplitSolution(solution.Value, solution.Value.Comp.Solution.Volume);
        _reactive.DoEntityReaction(body, split, ReactionMethod.Ingestion);
        if (_body.TryGetBodyOrganEntityComps<StomachComponent>(body, out var stomachs) && stomachs.Count > 0)
            _stomach.TryTransferSolution(stomachs[0], split);
    }

    private void Clear(Entity<SurgeryTeethComponent> ent)
    {
        if (ent.Comp.Bite is not { } action)
            return;

        _actions.RemoveAction(ent.Owner, action);
        ent.Comp.Bite = null;
    }

    private int Max(SurgeryTeethComponent? teeth)
    {
        if (_prototype.TryIndex<SurgeryTeethPrototype>(CapId, out var cap) && cap.Max > 0)
            return cap.Max;

        return 3;
    }

    private bool Blocked(EntityUid body)
    {
        if (TryComp<LimbBodyComponent>(body, out var limbs) && (limbs.Plant || limbs.Soft))
            return true;

        return TryComp<HumanoidAppearanceComponent>(body, out var look)
               && SurgerySpeciesPrototype.IsMachine(_prototype, look.Species.Id);
    }
}
