using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Sound.Components;
using Content.Shared.Strip.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Strip;

public abstract class SharedStrippableSystem : EntitySystem
{
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    [Dependency] private readonly SharedCuffableSystem _cuffableSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    // DS14-start
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    // DS14-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrippableComponent, GetVerbsEvent<Verb>>(AddStripVerb);
        SubscribeLocalEvent<StrippableComponent, GetVerbsEvent<ExamineVerb>>(AddStripExamineVerb);

        // BUI
        SubscribeLocalEvent<StrippableComponent, StrippingSlotButtonPressed>(OnStripButtonPressed);

        // DoAfters
        SubscribeLocalEvent<HandsComponent, DoAfterAttemptEvent<StrippableDoAfterEvent>>(OnStrippableDoAfterRunning);
        SubscribeLocalEvent<HandsComponent, StrippableDoAfterEvent>(OnStrippableDoAfterFinished);

        SubscribeLocalEvent<StrippingComponent, CanDropTargetEvent>(OnCanDropOn);
        SubscribeLocalEvent<StrippableComponent, CanDropDraggedEvent>(OnCanDrop);
        SubscribeLocalEvent<StrippableComponent, DragDropDraggedEvent>(OnDragDrop);
        SubscribeLocalEvent<StrippableComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void AddStripVerb(EntityUid uid, StrippableComponent component, GetVerbsEvent<Verb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
            return;

        // DS14-start: don't offer to strip a target that can't be stripped (e.g. a stasis cocoon).
        if (args.Target is { Valid: true } target && TryComp<HandsComponent>(target, out var targetHands) && !targetHands.CanBeStripped)
            return;
        // DS14-end

        Verb verb = new()
        {
            Text = Loc.GetString("strip-verb-get-data-text"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Act = () => TryOpenStrippingUi(args.User, (uid, component), true),
        };

        args.Verbs.Add(verb);
    }

    private void AddStripExamineVerb(EntityUid uid, StrippableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
            return;

        // DS14-start: don't offer to strip a target that can't be stripped (e.g. a stasis cocoon).
        if (args.Target is { Valid: true } target && TryComp<HandsComponent>(target, out var targetHands) && !targetHands.CanBeStripped)
            return;
        // DS14-end

        ExamineVerb verb = new()
        {
            Text = Loc.GetString("strip-verb-get-data-text"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Act = () => TryOpenStrippingUi(args.User, (uid, component), true),
            Category = VerbCategory.Examine,
        };

        args.Verbs.Add(verb);
    }

    private void OnStripButtonPressed(Entity<StrippableComponent> strippable, ref StrippingSlotButtonPressed args)
    {
        if (args.Actor is not { Valid: true } user ||
            !TryComp<HandsComponent>(user, out var userHands))
            return;

        if (args.IsHand)
        {
            StripHand((user, userHands), (strippable.Owner, null), args.Slot, strippable);
            return;
        }

        if (!TryComp<InventoryComponent>(strippable, out var inventory))
            return;

        var hasEnt = _inventorySystem.TryGetSlotEntity(strippable, args.Slot, out var held, inventory);

        if (_handsSystem.GetActiveItem((user, userHands)) is { } activeItem && !hasEnt)
            StartStripInsertInventory((user, userHands), strippable.Owner, activeItem, args.Slot);
        else if (hasEnt)
            StartStripRemoveInventory(user, strippable.Owner, held!.Value, args.Slot);
    }

    private void StripHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        string handId,
        StrippableComponent? targetStrippable)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp) ||
            !Resolve(target, ref targetStrippable))
            return;

        var heldEntity = _handsSystem.GetHeldItem(target.Owner, handId);
        var activeItem = _handsSystem.GetActiveItem(user.AsNullable());

        if (!target.Comp.CanBeStripped &&
            !(heldEntity == null && activeItem != null && IsInteractiveGhost(user.Owner)))
            return;

        // Is the target a handcuff?
        if (TryComp<VirtualItemComponent>(heldEntity, out var virtualItem) &&
            _cuffableSystem.TryGetAllCuffs(target.Owner, out var cuffs) &&
            cuffs.Contains(virtualItem.BlockingEntity))
        {
            _cuffableSystem.TryUncuff(target.Owner, user, virtualItem.BlockingEntity);
            return;
        }

        // DS14-start
        if (activeItem is { } item && heldEntity == null)
            StartStripInsertHand(user, target, item, handId, targetStrippable);
        else if (heldEntity != null)
            StartStripRemoveHand(user, target, heldEntity.Value, handId, targetStrippable);
        // DS14-end
    }

    /// <summary>
    ///     Checks whether the item is in a user's active hand and whether it can be inserted into the inventory slot.
    /// </summary>
    private bool CanStripInsertInventory(
        Entity<HandsComponent?> user,
        EntityUid target,
        EntityUid held,
        string slot)
    {
        if (!Resolve(user, ref user.Comp))
            return false;

        // DS14-start: a stasis cocoon blocks inventory slots as well as hands.
        if (TryComp<HandsComponent>(target, out var targetHands) && !targetHands.CanBeStripped)
            return false;
        // DS14-end

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != held)
            return false;

        if (!_handsSystem.CanDropHeld(user, user.Comp.ActiveHandId!))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-drop"));
            return false;
        }

        var targetIdentity = Identity.Entity(target, EntityManager);

        if (_inventorySystem.TryGetSlotEntity(target, slot, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-item-slot-occupied", ("owner", targetIdentity)));
            return false;
        }

        if (!_inventorySystem.CanEquip(user, target, held, slot, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-equip-message", ("owner", targetIdentity)));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to insert the item in the user's active hand into the inventory slot.
    /// </summary>
    private void StartStripInsertInventory(
        Entity<HandsComponent?> user,
        EntityUid target,
        EntityUid held,
        string slot)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        if (!CanStripInsertInventory(user, target, held, slot))
            return;

        if (!_inventorySystem.TryGetSlot(target, slot, out var slotDef))
        {
            Log.Error($"{ToPrettyString(user)} attempted to place an item in a non-existent inventory slot ({slot}) on {ToPrettyString(target)}");
            return;
        }

        var (time, stealth) = GetStripTimeModifiers(user, target, held, slotDef.StripTime);

        if (!stealth)
        {
            _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner-insert",
                                                        ("user", Identity.Entity(user, EntityManager)),
                                                        ("item", _handsSystem.GetActiveItem((user, user.Comp))!.Value)),
                                                        target,
                                                        target,
                                                        PopupType.Large);
        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}place the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s {slot} slot");

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(true, true, slot), user, target, held)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    ///     Inserts the item in the user's active hand into the inventory slot.
    /// </summary>
    private void StripInsertInventory(
        Entity<HandsComponent?> user,
        EntityUid target,
        EntityUid held,
        string slot)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        if (!CanStripInsertInventory(user, target, held, slot))
            return;

        if (!_handsSystem.TryDrop(user))
            return;

        _inventorySystem.TryEquip(user, target, held, slot, triggerHandContact: true);
        _adminLogger.Add(LogType.Stripping, LogImpact.Medium, $"{ToPrettyString(user):actor} has placed the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s {slot} slot");
    }

    /// <summary>
    ///     Checks whether the item can be removed from the target's inventory.
    /// </summary>
    private bool CanStripRemoveInventory(
        EntityUid user,
        EntityUid target,
        EntityUid item,
        string slot)
    {
        // DS14-start: re-check during the strip do-after so entering a cocoon cancels it.
        if (TryComp<HandsComponent>(target, out var targetHands) && !targetHands.CanBeStripped)
            return false;
        // DS14-end

        if (!_inventorySystem.TryGetSlotEntity(target, slot, out var slotItem))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-item-slot-free-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        if (slotItem != item)
            return false;

        if (!_inventorySystem.CanUnequip(user, target, slot, out var reason))
        {
            _popupSystem.PopupCursor(Loc.GetString(reason));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to remove the item from the target's inventory and insert it in the user's active hand.
    /// </summary>
    private void StartStripRemoveInventory(
        EntityUid user,
        EntityUid target,
        EntityUid item,
        string slot)
    {
        if (!CanStripRemoveInventory(user, target, item, slot))
            return;

        if (!_inventorySystem.TryGetSlot(target, slot, out var slotDef))
        {
            Log.Error($"{ToPrettyString(user)} attempted to take an item from a non-existent inventory slot ({slot}) on {ToPrettyString(target)}");
            return;
        }

        var (time, stealth) = GetStripTimeModifiers(user, target, item, slotDef.StripTime);

        if (!stealth)
        {
            if (IsStripHidden(slotDef, user))
                _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner-hidden", ("slot", slot)), target, target, PopupType.Large);
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner",
                                                            ("user", Identity.Entity(user, EntityManager)),
                                                            ("item", item)),
                                                            target,
                                                            target,
                                                            PopupType.Large);

            }
        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}strip the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s {slot} slot");

        _interactionSystem.DoContactInteraction(user, item);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(false, true, slot), user, target, item)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = false, // Allow simultaneously removing multiple items.
            DuplicateCondition = DuplicateConditions.SameTool
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    ///     Removes the item from the target's inventory and inserts it in the user's active hand.
    /// </summary>
    private void StripRemoveInventory(
        EntityUid user,
        EntityUid target,
        EntityUid item,
        string slot,
        bool stealth)
    {
        if (!CanStripRemoveInventory(user, target, item, slot))
            return;

        if (!_inventorySystem.TryUnequip(user, target, slot, triggerHandContact: true))
            return;

        // DS14-start
        var suppressSounds = stealth && !HasComp<SuppressPickupDropSoundComponent>(item);
        if (suppressSounds)
            EnsureComp<SuppressPickupDropSoundComponent>(item);

        try
        {
            RaiseLocalEvent(item, new DroppedEvent(user), true); // Gas tank internals etc.
            _handsSystem.PickupOrDrop(user, item, animateUser: stealth, animate: !stealth);
        }
        finally
        {
            if (suppressSounds)
                RemCompDeferred<SuppressPickupDropSoundComponent>(item);
        }
        // DS14-end

        _adminLogger.Add(LogType.Stripping, LogImpact.High, $"{ToPrettyString(user):actor} has stripped the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s {slot} slot");
    }

    // DS14-start
    /// <summary>
    ///     Checks whether the item in the user's active hand can be inserted into one of the target's hands.
    /// </summary>
    private bool CanStripInsertHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid held,
        string handName,
        bool allowClientUnknownSession = false)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp))
            return false;

        if (!target.Comp.CanBeStripped && !IsInteractiveGhost(user.Owner))
            return false;

        if (!CanDirectInsertHand(user.Owner, target.Owner, allowClientUnknownSession))
            return false;

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != held)
            return false;

        if (!_handsSystem.TryGetHand(target, handName, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-item-slot-free-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        if (!_container.TryGetContainer(target, handName, out var handContainer) ||
            handContainer.Count != 0)
            return false;

        if (IsInteractiveGhost(user.Owner))
            return true;

        if (!_handsSystem.CanDropHeld(user, user.Comp.ActiveHandId!))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-drop"));
            return false;
        }

        if (!_handsSystem.CanPickupToHand(target, held, handName, checkActionBlocker: false, handsComp: target.Comp))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-equip-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to insert the item in the user's active hand into one of the target's hands.
    /// </summary>
    private void StartStripInsertHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid held,
        string handName,
        StrippableComponent? targetStrippable = null)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp) ||
            !Resolve(target, ref targetStrippable))
            return;

        if (!CanStripInsertHand(user, target, held, handName))
            return;

        if (IsInteractiveGhost(user.Owner))
        {
            StripInsertHand(user, target, held, handName, true);
            return;
        }

        var (time, stealth) = GetStripTimeModifiers(user, target, null, targetStrippable.HandStripDelay);

        if (!stealth)
        {
            _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner-insert",
                    ("user", Identity.Entity(user, EntityManager)),
                    ("item", held)),
                target,
                target,
                PopupType.Large);
        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}place the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s hands");

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(true, false, handName), user, target, held)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    ///     Places the item in the user's active hand into one of the target's hands.
    /// </summary>
    private void StripInsertHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid held,
        string handName,
        bool stealth)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp))
            return;

        if (!CanStripInsertHand(user, target, held, handName))
            return;

        var suppressSounds = stealth && !HasComp<SuppressPickupDropSoundComponent>(held);
        if (suppressSounds)
            EnsureComp<SuppressPickupDropSoundComponent>(held);

        try
        {
            if (!TryForceInsertItemIntoHand(user, target, held, handName))
                return;
        }
        finally
        {
            if (suppressSounds)
                RemCompDeferred<SuppressPickupDropSoundComponent>(held);
        }

        _adminLogger.Add(LogType.Stripping, LogImpact.Medium, $"{ToPrettyString(user):actor} has placed the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s hands");

        // Hand update will trigger strippable update.
    }
    // DS14-end

    /// <summary>
    ///     Checks whether the item is in the target's hand and whether it can be dropped.
    /// </summary>
    private bool CanStripRemoveHand(
        EntityUid user,
        Entity<HandsComponent?> target,
        EntityUid item,
        string handName)
    {
        if (!Resolve(target, ref target.Comp))
            return false;

        if (!target.Comp.CanBeStripped)
            return false;

        if (!_handsSystem.TryGetHand(target, handName, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-item-slot-free-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        if (!_handsSystem.TryGetHeldItem(target, handName, out var heldEntity))
            return false;

        if (HasComp<VirtualItemComponent>(heldEntity))
            return false;

        if (heldEntity != item)
            return false;

        if (!_handsSystem.CanDropHeld(target, handName, false))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-drop-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to remove the item from the target's hand and insert it in the user's active hand.
    /// </summary>
    private void StartStripRemoveHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid item,
        string handName,
        StrippableComponent? targetStrippable = null)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp) ||
            !Resolve(target, ref targetStrippable))
            return;

        if (!CanStripRemoveHand(user, target, item, handName))
            return;

        var (time, stealth) = GetStripTimeModifiers(user, target, null, targetStrippable.HandStripDelay);

        if (!stealth)
        {
            _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner",
                                                        ("user", Identity.Entity(user, EntityManager)),
                                                        ("item", item)),
                                                        target,
                                                        target);
        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}strip the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s hands");

        _interactionSystem.DoContactInteraction(user, item);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(false, false, handName), user, target, item)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = false, // Allow simultaneously removing multiple items.
            DuplicateCondition = DuplicateConditions.SameTool
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    ///     Takes the item from the target's hand and inserts it in the user's active hand.
    /// </summary>
    private void StripRemoveHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid item,
        string handName,
        bool stealth)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp))
            return;

        if (!CanStripRemoveHand(user, target, item, handName))
            return;

        // DS14-start
        var suppressSounds = stealth && !HasComp<SuppressPickupDropSoundComponent>(item);
        if (suppressSounds)
            EnsureComp<SuppressPickupDropSoundComponent>(item);

        try
        {
            _handsSystem.TryDrop(target, item, checkActionBlocker: false);
            _handsSystem.PickupOrDrop(user, item, animateUser: stealth, animate: !stealth, handsComp: user.Comp);
        }
        finally
        {
            if (suppressSounds)
                RemCompDeferred<SuppressPickupDropSoundComponent>(item);
        }
        // DS14-end

        _adminLogger.Add(LogType.Stripping, LogImpact.High, $"{ToPrettyString(user):actor} has stripped the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s hands");

        // Hand update will trigger strippable update.
    }

    private void OnStrippableDoAfterRunning(Entity<HandsComponent> entity, ref DoAfterAttemptEvent<StrippableDoAfterEvent> ev)
    {
        var args = ev.DoAfter.Args;

        DebugTools.Assert(entity.Owner == args.User);
        DebugTools.Assert(args.Target != null);
        DebugTools.Assert(args.Used != null);
        DebugTools.Assert(ev.Event.SlotOrHandName != null);

        if (ev.Event.InventoryOrHand)
        {
            if (ev.Event.InsertOrRemove && !CanStripInsertInventory((entity.Owner, entity.Comp), args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName) ||
                !ev.Event.InsertOrRemove && !CanStripRemoveInventory(entity.Owner, args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName))
            {
                ev.Cancel();
            }
        }
        else
        {
            // DS14-start
            if (ev.Event.InsertOrRemove
                    ? !CanStripInsertHand((entity.Owner, entity.Comp), args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName, true)
                    : !CanStripRemoveHand(entity.Owner, args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName))
            {
                ev.Cancel();
            }
            // DS14-end
        }
    }

    private void OnStrippableDoAfterFinished(Entity<HandsComponent> entity, ref StrippableDoAfterEvent ev)
    {
        if (ev.Cancelled)
            return;

        DebugTools.Assert(entity.Owner == ev.User);
        DebugTools.Assert(ev.Target != null);
        DebugTools.Assert(ev.Used != null);
        DebugTools.Assert(ev.SlotOrHandName != null);

        if (ev.InventoryOrHand)
        {
            if (ev.InsertOrRemove)
                StripInsertInventory((entity.Owner, entity.Comp), ev.Target.Value, ev.Used.Value, ev.SlotOrHandName);
            else
                StripRemoveInventory(entity.Owner, ev.Target.Value, ev.Used.Value, ev.SlotOrHandName, ev.Args.Hidden);
        }
        else
        {
            // DS14-start
            if (ev.InsertOrRemove)
                StripInsertHand((entity.Owner, entity.Comp), ev.Target.Value, ev.Used.Value, ev.SlotOrHandName, ev.Args.Hidden);
            else
                StripRemoveHand((entity.Owner, entity.Comp), ev.Target.Value, ev.Used.Value, ev.SlotOrHandName, ev.Args.Hidden);
            // DS14-end
        }
    }

    private void OnActivateInWorld(EntityUid uid, StrippableComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || args.Target == args.User)
            return;

        if (TryOpenStrippingUi(args.User, (uid, component)))
            args.Handled = true;
    }

    /// <summary>
    /// Modify the strip time via events. Raised directed at the item being stripped, the player stripping someone and the player being stripped.
    /// </summary>
    public (TimeSpan Time, bool Stealth) GetStripTimeModifiers(EntityUid user, EntityUid targetPlayer, EntityUid? targetItem, TimeSpan initialTime)
    {
        var itemEv = new BeforeItemStrippedEvent(initialTime, false);
        if (targetItem != null)
            RaiseLocalEvent(targetItem.Value, ref itemEv);
        var userEv = new BeforeStripEvent(itemEv.Time, itemEv.Stealth);
        RaiseLocalEvent(user, ref userEv);
        var targetEv = new BeforeGettingStrippedEvent(userEv.Time, userEv.Stealth);
        RaiseLocalEvent(targetPlayer, ref targetEv);
        return (targetEv.Time, targetEv.Stealth);
    }

    private void OnDragDrop(EntityUid uid, StrippableComponent component, ref DragDropDraggedEvent args)
    {
        // If the user drags a strippable thing onto themselves.
        if (args.Handled || args.Target != args.User)
            return;

        if (TryOpenStrippingUi(args.User, (uid, component)))
            args.Handled = true;
    }

    public bool TryOpenStrippingUi(EntityUid user, Entity<StrippableComponent> target, bool openInCombat = false)
    {
        if (!openInCombat && TryComp<CombatModeComponent>(user, out var mode) && mode.IsInCombatMode)
            return false;

        if (!HasComp<StrippingComponent>(user))
            return false;

        // DS14-start: don't let anyone open the stripping UI on a target that
        // can't be stripped (e.g. a changeling in a stasis cocoon).
        if (TryComp<HandsComponent>(target.Owner, out var targetHands) && !targetHands.CanBeStripped)
        {
            _popupSystem.PopupCursor(Loc.GetString("strip-cannot-strip"), user);
            return false;
        }
        // DS14-end

        _ui.OpenUi(target.Owner, StrippingUiKey.Key, user);
        return true;
    }

    private void OnCanDropOn(EntityUid uid, StrippingComponent component, ref CanDropTargetEvent args)
    {
        var val = uid == args.User &&
                  HasComp<StrippableComponent>(args.Dragged) &&
                  HasComp<HandsComponent>(args.User) &&
                  HasComp<StrippingComponent>(args.User);
        args.Handled |= val;
        args.CanDrop |= val;
    }

    private void OnCanDrop(EntityUid uid, StrippableComponent component, ref CanDropDraggedEvent args)
    {
        args.CanDrop |= args.Target == args.User &&
                        HasComp<StrippingComponent>(args.User) &&
                        HasComp<HandsComponent>(args.User);

        if (args.CanDrop)
            args.Handled = true;
    }

    public bool IsStripHidden(SlotDefinition definition, EntityUid? viewer)
    {
        if (!definition.StripHidden)
            return false;

        if (viewer == null)
            return true;

        return !HasComp<BypassInteractionChecksComponent>(viewer);
    }

    // DS14-start
    private bool CanDirectInsertHand(EntityUid user, EntityUid target, bool allowClientUnknownSession = false)
    {
        if (IsInteractiveGhost(user))
            return true;

        if (_mobState.IsIncapacitated(target))
            return true;

        if (!_netManager.IsServer)
            return allowClientUnknownSession;

        return !_playerManager.TryGetSessionByEntity(target, out var session) ||
               !IsActiveSession(session);
    }

    private bool TryForceInsertItemIntoHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid item,
        string targetHand)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp))
            return false;

        if (!_container.TryGetContainer(target, targetHand, out var handContainer) ||
            handContainer.Count != 0)
            return false;

        EntityUid? oldHandOwner = null;
        HandsComponent? oldHands = null;
        string? oldHandId = null;
        if (_container.TryGetContainingContainer((item, null, null), out var oldContainer) &&
            TryComp(oldContainer.Owner, out oldHands) &&
            _handsSystem.TryGetHand((oldContainer.Owner, oldHands), oldContainer.ID, out _))
        {
            oldHandOwner = oldContainer.Owner;
            oldHandId = oldContainer.ID;
        }

        if (!_container.TryRemoveFromContainer((item, null, null), force: true, out var wasInContainer) && wasInContainer)
            return false;

        if (oldHandOwner is { } oldOwner && oldHands != null && oldHandId != null)
        {
            Dirty(oldOwner, oldHands);

            if (oldHandId == oldHands.ActiveHandId)
                RaiseLocalEvent(item, new HandDeselectedEvent(oldOwner));
        }

        if (!_container.Insert(item, handContainer, force: true))
            return false;

        _interactionSystem.DoContactInteraction(target.Owner, item);
        Dirty(target.Owner, target.Comp);

        if (targetHand == target.Comp.ActiveHandId)
            RaiseLocalEvent(item, new HandSelectedEvent(target.Owner));

        return true;
    }

    private bool IsInteractiveGhost(EntityUid user)
    {
        return TryComp(user, out GhostComponent? ghost) && ghost.CanGhostInteract;
    }

    private static bool IsActiveSession(ICommonSession session)
    {
        return session.Status is SessionStatus.Connected or SessionStatus.InGame;
    }
    // DS14-end

}
