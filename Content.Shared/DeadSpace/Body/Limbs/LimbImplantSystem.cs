// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed class LimbImplantSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbImplantComponent, EntGotInsertedIntoContainerMessage>(OnInsert);
        SubscribeLocalEvent<LimbImplantComponent, EntGotRemovedFromContainerMessage>(OnRemove);
    }

    public void EnsureSlots(EntityUid body)
    {
        EnsureComp<LimbImplantsComponent>(body);
        foreach (var slot in Layout(body))
            _containers.EnsureContainer<Container>(body, LimbImplantSlots.ContainerId(slot.Id));
    }

    private void OnInsert(Entity<LimbImplantComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        var containerId = args.Container.ID;
        if (!IsImplantContainer(containerId))
            return;

        ent.Comp.Host = args.Container.Owner;
        Dirty(ent);

        EntityManager.AddComponents(args.Container.Owner, ent.Comp.ImplantComponents);
        if (ent.Comp.ImplantAction != null)
            _actions.AddAction(args.Container.Owner, ref ent.Comp.Action, ent.Comp.ImplantAction, ent.Owner);
    }

    private void OnRemove(Entity<LimbImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        var containerId = args.Container.ID;
        if (!IsImplantContainer(containerId))
            return;

        if (ent.Comp.Host == null || TerminatingOrDeleted(ent.Comp.Host.Value))
        {
            ent.Comp.Host = null;
            ent.Comp.Action = null;
            Dirty(ent);
            return;
        }

        var host = ent.Comp.Host.Value;
        EntityManager.RemoveComponents(host, ent.Comp.ImplantComponents);
        if (ent.Comp.Toggled)
        {
            EntityManager.RemoveComponents(host, ent.Comp.ToggleComponents);
            ent.Comp.Toggled = false;
        }

        _actions.RemoveAction(host, ent.Comp.Action);
        ent.Comp.Action = null;
        ent.Comp.Host = null;
        Dirty(ent);
    }

    public IReadOnlyList<LimbImplantSlotDef> Layout(EntityUid body)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var look))
            return Array.Empty<LimbImplantSlotDef>();

        if (_prototypes.TryIndex<LimbSpeciesPrototype>(look.Species, out var species)
            && (species.OwnImplants || species.Implants.Count > 0))
            return species.Implants;

        return _prototypes.TryIndex(HumanSpecies, out var human)
            ? human.Implants
            : Array.Empty<LimbImplantSlotDef>();
    }

    private static readonly ProtoId<LimbSpeciesPrototype> HumanSpecies = "Human";

    private static bool IsImplantContainer(string containerId)
    {
        return containerId.StartsWith(LimbImplantSlots.Prefix, StringComparison.Ordinal);
    }

    public bool HasSlot(EntityUid body, string slot)
    {
        foreach (var def in Layout(body))
        {
            if (def.Id == slot)
                return true;
        }

        return false;
    }

    public int Capacity(EntityUid body, string slot)
    {
        foreach (var def in Layout(body))
        {
            if (def.Id == slot)
                return Math.Max(0, def.Capacity);
        }

        return 0;
    }

    public int Used(EntityUid body, string slot)
    {
        if (!_containers.TryGetContainer(body, LimbImplantSlots.ContainerId(slot), out var pocket))
            return 0;

        var used = 0;
        foreach (var item in pocket.ContainedEntities)
        {
            if (TryComp<LimbImplantComponent>(item, out var implant))
                used += Math.Max(1, implant.Size);
            else
                used += 1;
        }

        return used;
    }

    public bool Free(EntityUid body, string slot, int size = 1)
    {
        return HasSlot(body, slot) && Used(body, slot) + Math.Max(1, size) <= Capacity(body, slot);
    }

    public bool Occupied(EntityUid body, string slot)
    {
        return Used(body, slot) > 0;
    }

    public bool Fits(EntityUid body, string slot, EntityUid item)
    {
        return TryComp<LimbImplantComponent>(item, out var implant)
               && implant.Slots.Contains(slot)
               && Free(body, slot, implant.Size);
    }

    public bool TryInsert(EntityUid body, string slot, EntityUid item)
    {
        if (!Fits(body, slot, item))
            return false;

        if (!_containers.TryGetContainer(body, LimbImplantSlots.ContainerId(slot), out var pocket))
            return false;

        return _containers.Insert(item, pocket);
    }

    public bool TryExtract(EntityUid body, string slot, out EntityUid item)
    {
        item = default;
        if (!_containers.TryGetContainer(body, LimbImplantSlots.ContainerId(slot), out var pocket)
            || pocket.ContainedEntities.Count == 0)
            return false;

        item = pocket.ContainedEntities[^1];
        if (!_containers.Remove(item, pocket))
            return false;

        _transform.SetCoordinates(item, Transform(body).Coordinates.Offset(new Vector2(0.45f, 0.15f)));
        return true;
    }
}
