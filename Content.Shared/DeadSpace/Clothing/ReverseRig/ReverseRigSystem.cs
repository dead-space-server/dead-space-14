// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.Clothing.ReverseRig;

public sealed class ReverseRigSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly Dictionary<EntityUid, DeferredReverseRigRollbackEvent> _pendingRollbacks = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReverseRigComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ReverseRigComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IsEquippingAttemptEvent>(OnEquippingAttempt);
        SubscribeLocalEvent<ReverseRigComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<ReverseRigComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<ReverseRigComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<DeferredReverseRigRollbackEvent>(OnDeferredRollback);

        SubscribeLocalEvent<ReverseRigBackpackComponent, BeingUnequippedAttemptEvent>(OnBackpackUnequipAttempt);
        SubscribeLocalEvent<ReverseRigBackpackComponent, ComponentRemove>(OnBackpackComponentRemove);
    }

    private void OnComponentInit(Entity<ReverseRigComponent> ent, ref ComponentInit args)
    {
        var (uid, component) = ent;
        component.BackpackContainer = _container.EnsureContainer<ContainerSlot>(uid, component.BackpackContainerId);
    }

    private void OnMapInit(Entity<ReverseRigComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsServer)
            return;

        var (uid, component) = ent;
        var container = component.BackpackContainer;
        if (container == null)
            return;

        // A backpack already exists (e.g. the suit was mapped in with one).
        if (container.ContainedEntity is { } existing)
        {
            var existingAttached = EnsureComp<ReverseRigBackpackComponent>(existing);
            existingAttached.AttachedUid = uid;
            Dirty(existing, existingAttached);

            if (component.BackpackUid != existing)
            {
                component.BackpackUid = existing;
                Dirty(uid, component);
            }
            return;
        }

        var xform = Transform(uid);
        var backpack = Spawn(component.BackpackPrototype, xform.Coordinates);
        component.BackpackUid = backpack;
        var attached = EnsureComp<ReverseRigBackpackComponent>(backpack);
        attached.AttachedUid = uid;
        Dirty(backpack, attached);

        _container.Insert(backpack, container, containerXform: xform);
        Dirty(uid, component);
    }

    private void OnEquippingAttempt(IsEquippingAttemptEvent args)
    {
        if (!TryComp<ReverseRigComponent>(args.Equipment, out var component) ||
            (args.SlotFlags & component.RequiredFlags) != component.RequiredFlags ||
            !_inventory.TryGetSlotEntity(args.EquipTarget, component.Slot, out var existing) ||
            existing == component.BackpackUid ||
            _inventory.CanUnequip(args.EquipTarget, args.EquipTarget, component.Slot, out var reason))
        {
            return;
        }

        args.Reason = reason;
        args.Cancel();
    }

    private void OnGotEquipped(Entity<ReverseRigComponent> ent, ref GotEquippedEvent args)
    {
        if (!_net.IsServer)
            return;

        var (uid, component) = ent;
        if ((args.SlotFlags & component.RequiredFlags) != component.RequiredFlags)
            return;

        if (component.BackpackUid is not { } backpack || Deleted(backpack))
            return;

        var wearer = args.Equipee;
        _pendingRollbacks.Remove(uid);

        // Whatever previously occupied the slot falls off the wearer, unless it is our own backpack.
        EntityUid? displaced = null;
        if (_inventory.TryGetSlotEntity(wearer, component.Slot, out var existing) && existing != backpack)
        {
            if (!_inventory.TryUnequip(wearer, component.Slot, triggerHandContact: true))
            {
                ScheduleRollback(uid, wearer, args.Slot, null);
                return;
            }

            displaced = existing;
        }

        // The attached backpack is not accessible while nested inside the equipped suit. Move it out of the
        // private container first so normal inventory access and equip checks can be used for the public slot.
        if (component.BackpackContainer == null ||
            !_container.Remove(backpack, component.BackpackContainer) ||
            !_inventory.TryEquip(wearer, wearer, backpack, component.Slot, triggerHandContact: true))
        {
            if (component.BackpackContainer != null && !_container.IsEntityInContainer(backpack))
                _container.Insert(backpack, component.BackpackContainer);

            ScheduleRollback(uid, wearer, args.Slot, displaced);
        }
    }

    private void OnGotUnequipped(Entity<ReverseRigComponent> ent, ref GotUnequippedEvent args)
    {
        if (!_net.IsServer)
            return;

        var (uid, component) = ent;
        if ((args.SlotFlags & component.RequiredFlags) != component.RequiredFlags)
            return;

        _pendingRollbacks.Remove(uid);

        if (component.BackpackUid is not { } backpack || Deleted(backpack))
            return;

        var wearer = args.Equipee;

        // The backpack comes off together with the suit.
        if (_inventory.TryGetSlotEntity(wearer, component.Slot, out var existing) && existing == backpack)
            _inventory.TryUnequip(wearer, component.Slot, force: true, triggerHandContact: true);

        if (component.BackpackContainer != null)
            _container.Insert(backpack, component.BackpackContainer);
    }

    private void OnComponentRemove(Entity<ReverseRigComponent> ent, ref ComponentRemove args)
    {
        _pendingRollbacks.Remove(ent.Owner);

        if (!_net.IsServer)
            return;

        if (ent.Comp.BackpackUid is { } backpack && !Deleted(backpack))
            QueueDel(backpack);
    }

    private void OnBackpackUnequipAttempt(Entity<ReverseRigBackpackComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        // The backpack is permanently attached to the suit and can not be removed manually.
        args.Cancel();
    }

    private void OnBackpackComponentRemove(Entity<ReverseRigBackpackComponent> ent, ref ComponentRemove args)
    {
        if (!_net.IsServer)
            return;

        // The backpack was removed or destroyed - clear the suit's reference.
        if (ent.Comp.AttachedUid is { } suit && TryComp<ReverseRigComponent>(suit, out var rig))
        {
            rig.BackpackUid = null;
            Dirty(suit, rig);
        }
    }

    private void ScheduleRollback(EntityUid rig, EntityUid wearer, string rigSlot, EntityUid? displaced)
    {
        var rollback = new DeferredReverseRigRollbackEvent(rig, wearer, rigSlot, displaced);
        _pendingRollbacks[rig] = rollback;
        QueueLocalEvent(rollback);
    }

    private void OnDeferredRollback(DeferredReverseRigRollbackEvent args)
    {
        if (!_pendingRollbacks.TryGetValue(args.Rig, out var active) || !ReferenceEquals(active, args))
            return;

        _pendingRollbacks.Remove(args.Rig);

        if (TerminatingOrDeleted(args.Rig) ||
            TerminatingOrDeleted(args.Wearer) ||
            !TryComp<ReverseRigComponent>(args.Rig, out var component) ||
            !_inventory.TryGetSlotEntity(args.Wearer, args.RigSlot, out var equipped) ||
            equipped != args.Rig ||
            !_inventory.TryUnequip(args.Wearer,
                args.RigSlot,
                silent: true,
                force: true,
                predicted: true))
        {
            return;
        }

        if (args.Displaced is not { } displaced ||
            TerminatingOrDeleted(displaced) ||
            _inventory.TryGetSlotEntity(args.Wearer, component.Slot, out _))
        {
            return;
        }

        // This item already passed normal unequip checks before the auxiliary backpack failed to equip.
        // Force is only used to restore the exact pre-operation inventory state.
        _inventory.TryEquip(args.Wearer,
            args.Wearer,
            displaced,
            component.Slot,
            silent: true,
            force: true,
            predicted: true);
    }

    private sealed class DeferredReverseRigRollbackEvent(
        EntityUid rig,
        EntityUid wearer,
        string rigSlot,
        EntityUid? displaced) : EntityEventArgs
    {
        public readonly EntityUid Rig = rig;
        public readonly EntityUid Wearer = wearer;
        public readonly string RigSlot = rigSlot;
        public readonly EntityUid? Displaced = displaced;
    }
}
