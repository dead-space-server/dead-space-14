using Content.Shared.Actions;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Strip;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Backmen.Blob.Components; //DS14

namespace Content.Shared.Clothing.EntitySystems;

public sealed class ToggleableClothingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStrippableSystem _strippable = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableClothingComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ToggleableClothingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleClothingEvent>(OnToggleClothing);
        SubscribeLocalEvent<ToggleableClothingComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ToggleableClothingComponent, ComponentRemove>(OnRemoveToggleable);
        SubscribeLocalEvent<ToggleableClothingComponent, GotUnequippedEvent>(OnToggleableUnequip);

        SubscribeLocalEvent<AttachedClothingComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<AttachedClothingComponent, GotUnequippedEvent>(OnAttachedUnequip);
        SubscribeLocalEvent<AttachedClothingComponent, ComponentRemove>(OnRemoveAttached);
        SubscribeLocalEvent<AttachedClothingComponent, BeingUnequippedAttemptEvent>(OnAttachedUnequipAttempt);

        SubscribeLocalEvent<ToggleableClothingComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(GetRelayedVerbs);
        SubscribeLocalEvent<ToggleableClothingComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AttachedClothingComponent, GetVerbsEvent<EquipmentVerb>>(OnGetAttachedStripVerbsEvent);
        SubscribeLocalEvent<ToggleableClothingComponent, ToggleClothingDoAfterEvent>(OnDoAfterComplete);
        SubscribeLocalEvent<ToggleableClothingComponent, SelfToggleClothingDoAfterEvent>(OnSelfDoAfterComplete); //DS14

    }

    private void GetRelayedVerbs(EntityUid uid, ToggleableClothingComponent component, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        OnGetVerbs(uid, component, args.Args);
    }

    private void OnGetVerbs(EntityUid uid, ToggleableClothingComponent component, GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || component.ClothingUid == null || component.Container == null)
            return;

        var text = component.VerbText ?? (component.ActionEntity == null ? null : Name(component.ActionEntity.Value));
        if (text == null)
            return;

        if (!_inventorySystem.InSlotWithFlags(uid, component.RequiredFlags))
            return;

        var wearer = Transform(uid).ParentUid;
        if (args.User != wearer && component.StripDelay == null)
            return;

        var verb = new EquipmentVerb()
        {
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Text = Loc.GetString(text),
        };

        if (args.User == wearer)
        {
            verb.EventTarget = uid;
            verb.ExecutionEventArgs = new ToggleClothingEvent() { Performer = args.User };
        }
        else
        {
            verb.Act = () => StartDoAfter(args.User, uid, Transform(uid).ParentUid, component);
        }

        args.Verbs.Add(verb);
    }

    private void StartDoAfter(EntityUid user, EntityUid item, EntityUid wearer, ToggleableClothingComponent component)
    {
        if (component.StripDelay == null)
            return;

        var (time, stealth) = _strippable.GetStripTimeModifiers(user, wearer, item, component.StripDelay.Value);

        var args = new DoAfterArgs(EntityManager, user, time, new ToggleClothingDoAfterEvent(), item, wearer, item)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            // This should just re-use the BUI range checks & cancel the do after if the BUI closes. But that is all
            // server-side at the moment.
            // TODO BUI REFACTOR.
            DistanceThreshold = 2,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        if (!stealth)
        {
            var popup = Loc.GetString("strippable-component-alert-owner-interact", ("user", Identity.Entity(user, EntityManager)), ("item", item));
            _popupSystem.PopupEntity(popup, wearer, wearer, PopupType.Large);
        }
    }

    private void OnGetAttachedStripVerbsEvent(EntityUid uid, AttachedClothingComponent component, GetVerbsEvent<EquipmentVerb> args)
    {
        // redirect to the attached entity.
        OnGetVerbs(component.AttachedUid, Comp<ToggleableClothingComponent>(component.AttachedUid), args);
    }

    private void OnDoAfterComplete(EntityUid uid, ToggleableClothingComponent component, ToggleClothingDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        ToggleClothing(args.User, uid, component);
    }

    private void OnInteractHand(EntityUid uid, AttachedClothingComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(component.AttachedUid, out ToggleableClothingComponent? toggleCom)
            || toggleCom.Container == null)
            return;

        if (!_inventorySystem.TryUnequip(Transform(uid).ParentUid, toggleCom.Slot, force: true))
            return;

        _containerSystem.Insert(uid, toggleCom.Container);
        args.Handled = true;
    }

    /// <summary>
    ///     Called when the suit is unequipped, to ensure that the helmet also gets unequipped.
    /// </summary>
    private void OnToggleableUnequip(EntityUid uid, ToggleableClothingComponent component, GotUnequippedEvent args)
    {
        // If it's a part of PVS departure then don't handle it.
        if (_timing.ApplyingState)
            return;

        // If the attached clothing is not currently in the container, this just assumes that it is currently equipped.
        // This should maybe double check that the entity currently in the slot is actually the attached clothing, but
        // if its not, then something else has gone wrong already...
        if (component.Container != null && component.Container.ContainedEntity == null && component.ClothingUid != null)
            _inventorySystem.TryUnequip(args.Equipee, component.Slot, force: true, triggerHandContact: true);

        RestoreStoredClothing(args.Equipee, uid, component); // DS14: Return displaced headwear when removing the suit.
    }

    private void OnRemoveToggleable(EntityUid uid, ToggleableClothingComponent component, ComponentRemove args)
    {
        // If the parent/owner component of the attached clothing is being removed (entity getting deleted?) we will
        // delete the attached entity. We do this regardless of whether or not the attached entity is currently
        // "outside" of the container or not. This means that if a hardsuit takes too much damage, the helmet will also
        // automatically be deleted.

        _actionsSystem.RemoveAction(component.ActionEntity);

        if (component.ClothingUid != null && !_netMan.IsClient)
            QueueDel(component.ClothingUid.Value);
    }

    private void OnAttachedUnequipAttempt(EntityUid uid, AttachedClothingComponent component, BeingUnequippedAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnRemoveAttached(EntityUid uid, AttachedClothingComponent component, ComponentRemove args)
    {
        // if the attached component is being removed (maybe entity is being deleted?) we will just remove the
        // toggleable clothing component. This means if you had a hard-suit helmet that took too much damage, you would
        // still be left with a suit that was simply missing a helmet. There is currently no way to fix a partially
        // broken suit like this.

        if (!TryComp(component.AttachedUid, out ToggleableClothingComponent? toggleComp))
            return;

        if (toggleComp.LifeStage > ComponentLifeStage.Running)
            return;

        // DS14-start
        if (!_netMan.IsClient)
            ReleaseStoredClothing(component.AttachedUid, uid, toggleComp);
        // DS14-end

        _actionsSystem.RemoveAction(toggleComp.ActionEntity);
        RemComp(component.AttachedUid, toggleComp);
    }

    // DS14-start
    private void ReleaseStoredClothing(EntityUid suit,
        EntityUid attachedClothing,
        ToggleableClothingComponent component)
    {
        if (!component.StoreExistingItem ||
            !_containerSystem.TryGetContainer(suit, ToggleableClothingComponent.DefaultStoredClothingContainerId,
                out var container) || container is not ContainerSlot { ContainedEntity: { } stored })
        {
            return;
        }

        var wearer = Transform(suit).ParentUid;
        if (HasComp<InventoryComponent>(wearer) &&
            _inventorySystem.TryGetSlotEntity(wearer, component.Slot, out var equipped) &&
            equipped == attachedClothing)
        {
            _inventorySystem.TryUnequip(wearer,
                component.Slot,
                force: true,
                triggerHandContact: true);
        }

        if (!_containerSystem.Remove(stored, container))
            return;

        if (HasComp<InventoryComponent>(wearer) &&
            !_inventorySystem.TryGetSlotEntity(wearer, component.Slot, out _) &&
            _inventorySystem.TryEquip(wearer, wearer, stored, component.Slot, triggerHandContact: true))
        {
            return;
        }

        _transformSystem.DropNextTo(stored, suit);
    }
    // DS14-end

    /// <summary>
    ///     Called if the helmet was unequipped, to ensure that it gets moved into the suit's container.
    /// </summary>
    private void OnAttachedUnequip(EntityUid uid, AttachedClothingComponent component, GotUnequippedEvent args)
    {
        // Let containers worry about it.
        if (_timing.ApplyingState)
            return;

        if (component.LifeStage > ComponentLifeStage.Running)
            return;

        if (!TryComp(component.AttachedUid, out ToggleableClothingComponent? toggleComp))
            return;

        if (LifeStage(component.AttachedUid) > EntityLifeStage.MapInitialized)
            return;

        // As unequipped gets called in the middle of container removal, we cannot call a container-insert without causing issues.
        // So we delay it and process it during a system update:
        if (toggleComp.ClothingUid != null && toggleComp.Container != null)
            _containerSystem.Insert(toggleComp.ClothingUid.Value, toggleComp.Container);
    }

    //DS14-start
    private void OnSelfDoAfterComplete(EntityUid uid, ToggleableClothingComponent component,
        SelfToggleClothingDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var parent = Transform(uid).ParentUid;
        if (parent != args.User)
            return;

        if (!_inventorySystem.TryGetSlotEntity(args.User, component.Slot, out _)
            && !_inventorySystem.InSlotWithFlags(uid, component.RequiredFlags))
            return;

        ToggleClothing(args.User, uid, component);
    }
    //DS14-end

    /// <summary>
    ///     Equip or unequip the toggleable clothing.
    /// </summary>
    private void OnToggleClothing(EntityUid uid, ToggleableClothingComponent component, ToggleClothingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        //DS14-start
        if (component.ToggleDelay <= TimeSpan.Zero)
        {
            ToggleClothing(args.Performer, uid, component);
            return;
        }
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.Performer,
            component.ToggleDelay,
            new SelfToggleClothingDoAfterEvent(),
            uid,
            args.Performer)
        {
            BreakOnMove = false,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        //DS14-end
    }

    private void ToggleClothing(EntityUid user, EntityUid target, ToggleableClothingComponent component)
    {
        if (component.Container == null || component.ClothingUid == null)
            return;

        var parent = Transform(target).ParentUid;
        //DS14-start
        if (_inventorySystem.TryGetSlotEntity(parent, component.Slot, out var currentHeadItem))
        {
            if (currentHeadItem != component.ClothingUid &&
                (HasComp<ToggleableClothingStorageBlockerComponent>(currentHeadItem) || HasComp<BlobPodComponent>(currentHeadItem)))
            {
                _popupSystem.PopupClient(Loc.GetString("toggleable-clothing-remove-first", ("entity", currentHeadItem)), user, user);
                return;
            }
        }
        //DS14-end
        if (component.Container.ContainedEntity == null)
        {
            _inventorySystem.TryUnequip(user, parent, component.Slot, force: true);
            RestoreStoredClothing(parent, target, component); // DS14: Re-equip the displaced item after retracting the helmet.
        }
        else if (_inventorySystem.TryGetSlotEntity(parent, component.Slot, out var existing))
        {
            // DS14-start
            if (!component.StoreExistingItem ||
                HasComp<AttachedClothingComponent>(existing) ||
                HasComp<ArmorComponent>(existing) ||
                HasComp<ToggleableClothingStorageBlockerComponent>(existing))
            {
                _popupSystem.PopupClient(Loc.GetString("toggleable-clothing-remove-first", ("entity", existing)),
                    user, user);
                return;
            }

            // DS14: Server-only clothing components can veto temporary storage without leaking into shared code.
            var storageAttempt = new ToggleableClothingStorageAttemptEvent();
            RaiseLocalEvent(existing.Value, storageAttempt);
            if (storageAttempt.Cancelled)
            {
                _popupSystem.PopupClient(Loc.GetString("toggleable-clothing-remove-first", ("entity", existing)),
                    user, user);
                return;
            }

            // Prediction rollback can replace the container; resolve it from the manager on every toggle.
            var storedContainer = _containerSystem.EnsureContainer<ContainerSlot>(target,
                ToggleableClothingComponent.DefaultStoredClothingContainerId);

            if (!_inventorySystem.TryUnequip(user, parent, component.Slot, force: true) ||
                !_containerSystem.Insert(existing.Value, storedContainer))
            {
                _popupSystem.PopupClient(Loc.GetString("toggleable-clothing-remove-first", ("entity", existing)),
                    user, user);
                return;
            }

            if (!_inventorySystem.TryEquip(user, parent, component.ClothingUid.Value, component.Slot,
                    triggerHandContact: true))
                RestoreStoredClothing(parent, target, component);
            // DS14-end
        }
        else
        {
            // DS14-start
            if (!_inventorySystem.TryEquip(user, parent, component.ClothingUid.Value, component.Slot, triggerHandContact: true))
                RestoreStoredClothing(parent, target, component);
            // DS14-end
        }
    }

    // DS14-start
    private void RestoreStoredClothing(EntityUid wearer, EntityUid suit, ToggleableClothingComponent component)
    {
        if (!component.StoreExistingItem ||
            !_containerSystem.TryGetContainer(suit, ToggleableClothingComponent.DefaultStoredClothingContainerId,
                out var container) || container is not ContainerSlot { ContainedEntity: { } stored } ||
            _inventorySystem.TryGetSlotEntity(wearer, component.Slot, out _))
            return;

        if (!_containerSystem.Remove(stored, container))
            return;
        if (_inventorySystem.TryEquip(wearer, wearer, stored, component.Slot, triggerHandContact: true))
            return;

        // Keep the item associated with the suit if inventory rules changed while the helmet was active.
        _containerSystem.Insert(stored, container);
    }
    // DS14-end

    private void OnGetActions(EntityUid uid, ToggleableClothingComponent component, GetItemActionsEvent args)
    {
        if (component.ClothingUid != null
            && component.ActionEntity != null
            && (args.SlotFlags & component.RequiredFlags) == component.RequiredFlags)
        {
            args.AddAction(component.ActionEntity.Value);
        }
    }

    private void OnInit(EntityUid uid, ToggleableClothingComponent component, ComponentInit args)
    {
        component.Container = _containerSystem.EnsureContainer<ContainerSlot>(uid, component.ContainerId);
    }

    /// <summary>
    ///     On map init, either spawn the appropriate entity into the suit slot, or if it already exists, perform some
    ///     sanity checks. Also updates the action icon to show the toggled-entity.
    /// </summary>
    private void OnMapInit(EntityUid uid, ToggleableClothingComponent component, MapInitEvent args)
    {
        if (component.Container!.ContainedEntity is { } ent)
        {
            DebugTools.Assert(component.ClothingUid == ent, "Unexpected entity present inside of a toggleable clothing container.");
            return;
        }

        if (component.ClothingUid != null && component.ActionEntity != null)
        {
            DebugTools.Assert(Exists(component.ClothingUid), "Toggleable clothing is missing expected entity.");
            DebugTools.Assert(TryComp(component.ClothingUid, out AttachedClothingComponent? comp), "Toggleable clothing is missing an attached component");
            DebugTools.Assert(comp?.AttachedUid == uid, "Toggleable clothing uid mismatch");
        }
        else
        {
            var xform = Transform(uid);
            component.ClothingUid = Spawn(component.ClothingPrototype, xform.Coordinates);
            var attachedClothing = EnsureComp<AttachedClothingComponent>(component.ClothingUid.Value);
            attachedClothing.AttachedUid = uid;
            Dirty(component.ClothingUid.Value, attachedClothing);
            _containerSystem.Insert(component.ClothingUid.Value, component.Container, containerXform: xform);
            Dirty(uid, component);
        }

        if (_actionContainer.EnsureAction(uid, ref component.ActionEntity, out var action, component.Action))
            _actionsSystem.SetEntityIcon((component.ActionEntity.Value, action), component.ClothingUid);
    }

    //DS14-start
    public void ForceRetractHelmet(EntityUid suitUid, ToggleableClothingComponent? component = null)
    {
        if (!Resolve(suitUid, ref component))
            return;

        if (component.Container?.ContainedEntity == null)
        {
            var wearer = Transform(suitUid).ParentUid;
            ToggleClothing(wearer, suitUid, component);
        }
    }
    //DS14-end
}

public sealed partial class ToggleClothingEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class ToggleClothingDoAfterEvent : SimpleDoAfterEvent
{
}

//DS14-start
[Serializable, NetSerializable]
public sealed partial class SelfToggleClothingDoAfterEvent : SimpleDoAfterEvent
{
}
//DS14-end

// DS14
public sealed class ToggleableClothingStorageAttemptEvent : CancellableEntityEventArgs;
