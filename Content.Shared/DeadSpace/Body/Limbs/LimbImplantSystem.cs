// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Implants;
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
        if (!IsImplantContainer(containerId) || !HasComp<LimbImplantsComponent>(args.Container.Owner))
            return;

        ent.Comp.Host = args.Container.Owner;
        Dirty(ent);

        EntityManager.AddComponents(args.Container.Owner, ent.Comp.ImplantComponents);
        if (ent.Comp.ImplantAction != null)
            _actions.AddAction(args.Container.Owner, ref ent.Comp.Action, ent.Comp.ImplantAction, ent.Owner);

        var implanted = new ImplantImplantedEvent(ent.Owner, args.Container.Owner);
        RaiseLocalEvent(ent.Owner, ref implanted);
    }

    private void OnRemove(Entity<LimbImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        var containerId = args.Container.ID;
        if (!IsImplantContainer(containerId) || !HasComp<LimbImplantsComponent>(args.Container.Owner))
            return;

        if (ent.Comp.Host == null || TerminatingOrDeleted(ent.Comp.Host.Value))
        {
            ent.Comp.Host = null;
            ent.Comp.Action = null;
            Dirty(ent);
            return;
        }

        var host = ent.Comp.Host.Value;
        var removed = new ImplantRemovedEvent(ent.Owner, host);
        RaiseLocalEvent(ent.Owner, ref removed);
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

    public bool TryPlace(EntityUid body, EntityUid item)
    {
        if (!TryComp<LimbImplantComponent>(item, out var implant))
            return false;

        EnsureSlots(body);
        foreach (var slot in implant.Slots)
        {
            if (TryInsert(body, slot, item))
                return true;
        }

        return false;
    }

    public void Keep(EntityUid body, EntityUid part)
    {
        KeepTree(body, part);
    }

    public void Restore(EntityUid body, EntityUid part)
    {
        EnsureSlots(body);
        RestoreTree(body, part);
    }

    public void DropLost(EntityUid body, LimbZone zone)
    {
        foreach (var def in Layout(body))
        {
            var slotZone = LimbImplantSlots.ZoneOf(def.Id);
            if (slotZone == null || !LostWith(zone, slotZone.Value))
                continue;

            while (Occupied(body, def.Id))
            {
                if (!TryExtract(body, def.Id, out _))
                    break;
            }
        }
    }

    private void KeepTree(EntityUid body, EntityUid part)
    {
        KeepPart(body, part);
        foreach (var child in PartsInside(part))
            KeepTree(body, child);
    }

    private void RestoreTree(EntityUid body, EntityUid part)
    {
        RestorePart(body, part);
        foreach (var child in PartsInside(part))
            RestoreTree(body, child);
    }

    private void KeepPart(EntityUid body, EntityUid part)
    {
        if (ZoneOf(part) is not { } zone)
            return;

        foreach (var def in Layout(body))
        {
            if (LimbImplantSlots.ZoneOf(def.Id) != zone)
                continue;

            var id = LimbImplantSlots.ContainerId(def.Id);
            if (!_containers.TryGetContainer(body, id, out var from))
                continue;

            var held = _containers.EnsureContainer<Container>(part, id);
            while (from.ContainedEntities.Count > 0)
            {
                var item = from.ContainedEntities[^1];
                if (!_containers.Remove(item, from))
                    break;

                if (!_containers.Insert(item, held))
                {
                    Drop(body, item);
                    break;
                }
            }
        }
    }

    private void RestorePart(EntityUid body, EntityUid part)
    {
        if (ZoneOf(part) is not { } zone)
            return;

        foreach (var def in Layout(body))
        {
            if (LimbImplantSlots.ZoneOf(def.Id) != zone)
                continue;

            if (!_containers.TryGetContainer(part, LimbImplantSlots.ContainerId(def.Id), out var held))
                continue;

            var taken = new List<EntityUid>(held.ContainedEntities);
            foreach (var item in taken)
            {
                if (!_containers.Remove(item, held))
                    continue;

                if (!TryInsert(body, def.Id, item))
                    Drop(body, item);
            }
        }
    }

    private List<EntityUid> PartsInside(EntityUid part)
    {
        var children = new List<EntityUid>();
        if (!TryComp<ContainerManagerComponent>(part, out var manager))
            return children;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                if (HasComp<BodyPartComponent>(child))
                    children.Add(child);
            }
        }

        return children;
    }

    private void Drop(EntityUid body, EntityUid item)
    {
        _transform.SetCoordinates(item, Transform(body).Coordinates.Offset(new Vector2(0.45f, 0.15f)));
    }

    private LimbZone? ZoneOf(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var piece))
            return null;

        return piece.PartType switch
        {
            BodyPartType.Head => LimbZone.Head,
            BodyPartType.Torso => LimbZone.Torso,
            BodyPartType.Arm => Side(piece.Symmetry, LimbZone.ArmLeft, LimbZone.ArmRight),
            BodyPartType.Hand => Side(piece.Symmetry, LimbZone.HandLeft, LimbZone.HandRight),
            BodyPartType.Leg => Side(piece.Symmetry, LimbZone.LegLeft, LimbZone.LegRight),
            BodyPartType.Foot => Side(piece.Symmetry, LimbZone.FootLeft, LimbZone.FootRight),
            _ => null,
        };
    }

    private static LimbZone? Side(BodyPartSymmetry symmetry, LimbZone left, LimbZone right)
    {
        return symmetry switch
        {
            BodyPartSymmetry.Left => left,
            BodyPartSymmetry.Right => right,
            _ => null,
        };
    }

    private static bool LostWith(LimbZone severed, LimbZone slotZone)
    {
        if (slotZone == severed)
            return true;

        return LimbWoundRules.TryCarried(severed, out var child) && slotZone == child;
    }

    public bool TryInsert(EntityUid body, string slot, EntityUid item)
    {
        EnsureSlots(body);
        if (!Fits(body, slot, item))
            return false;

        if (!_containers.TryGetContainer(body, LimbImplantSlots.ContainerId(slot), out var pocket))
            return false;

        if (_containers.TryGetContainingContainer((item, null, null), out var hand) && hand != pocket
            && !_containers.Remove(item, hand, force: true))
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
