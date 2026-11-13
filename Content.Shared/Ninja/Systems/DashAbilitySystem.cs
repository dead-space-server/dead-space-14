using Content.Shared.Actions;
using Content.Shared.ActionBlocker; // DS14
using Content.Shared.Alert; // DS14
using Content.Shared.Charges.Systems;
using Content.Shared.Hands; // DS14
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Audio; // DS14
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Timing; // DS14
using Content.Shared.Random.Helpers; // DS14
using System.Numerics; // DS14
using System.Linq; // DS14
using Robust.Shared.Containers; // DS14

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// Handles dashing logic including charge consumption and checking attempt events.
/// </summary>
//DS14-start
public abstract partial class SharedDashAbilitySystem : EntitySystem
//DS14-end
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!; // DS14
    [Dependency] private SharedActionsSystem _actions = default!; // DS14
    [Dependency] private AlertsSystem _alerts = default!; // DS14
    [Dependency] private SharedChargesSystem _sharedCharges = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pullingSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!; // DS14
    //DS14-start
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private static readonly ProtoId<TagPrototype> CorruptTeleportTag = "CorruptTeleportItem";
    //DS14-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DashAbilityComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<DashAbilityComponent, DashEvent>(OnDash);
        SubscribeLocalEvent<DashAbilityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DashAbilityComponent, GotEquippedHandEvent>(OnEquippedHand); // DS14
        SubscribeLocalEvent<DashAbilityComponent, GotUnequippedHandEvent>(OnUnequippedHand); // DS14
        SubscribeLocalEvent<DashAbilityComponent, ExaminedEvent>(OnExamined); // DS14
    }

    //DS14-start
    private void OnEquippedHand(Entity<DashAbilityComponent> ent, ref GotEquippedHandEvent args)
    {
        if (CheckDash(ent, args.User))
            ShowAlert(args.User, ent);
    }

    private void OnUnequippedHand(Entity<DashAbilityComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (TryFindHeld(args.User, ent.Owner))
            return;

        _alerts.ClearAlert(args.User, ent.Comp.CooldownAlert);
    }

    private void ShowAlert(EntityUid user, Entity<DashAbilityComponent> ent)
    {
        var now = _timing.CurTime;
        if (ent.Comp.NextUse > now)
            _alerts.ShowAlert(user, ent.Comp.CooldownAlert, cooldown: (now, ent.Comp.NextUse), autoRemove: false);
        else
            _alerts.ShowAlert(user, ent.Comp.CooldownAlert, autoRemove: false);
    }

    private bool TryFindHeld(EntityUid user, EntityUid ignored)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held == ignored)
                continue;

            if (HasComp<DashAbilityComponent>(held))
                return true;
        }

        return false;
    }
    //DS14-end

    private void OnMapInit(Entity<DashAbilityComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        if (comp.DashAction != null)
            _actionContainer.EnsureAction(uid, ref comp.DashActionEntity, comp.DashAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<DashAbilityComponent> ent, ref GetItemActionsEvent args)
    {
        if (CheckDash(ent, args.User))
            args.AddAction(ent.Comp.DashActionEntity);
    }

    //DS14-start
    /// <summary>
    /// Handle charges and teleport to a visible location.
    /// </summary>
    private void OnDash(Entity<DashAbilityComponent> ent, ref DashEvent args)
    {
        if (TryDash(ent, args.Performer, args.Target))
            args.Handled = true;
    }

    public bool TryDash(Entity<DashAbilityComponent> ent, EntityUid user, EntityCoordinates target, bool predicted = true)
    {
        var (uid, comp) = ent;
        if (!CheckDash(uid, user))
            return false;

        if (!_actionBlocker.CanConsciouslyPerformAction(user) || !_actionBlocker.CanInteract(user, null))
            return false;

        if (_timing.CurTime < comp.NextUse)
            return false;

        if (!_hands.IsHolding(user, uid, out _))
        {
            DashPopup(predicted, "dash-ability-not-held", uid, user);
            return false;
        }

        var origin = _transform.GetMapCoordinates(user);
        var targetMap = _transform.ToMapCoordinates(target);
        if (!_examine.InRangeUnOccluded(origin, targetMap, SharedInteractionSystem.MaxRaycastRange, null))
        {
            // can only dash if the destination is visible on screen
            DashPopup(predicted, "dash-ability-cant-see", uid, user);
            return false;
        }

        if (!_sharedCharges.TryUseCharge(uid))
        {
            DashPopup(predicted, "dash-ability-no-charges", uid, user);
            return false;
        }

        // Check if the user is BEING pulled, and escape if so
        if (TryComp<PullableComponent>(user, out var pull) && _pullingSystem.IsPulled(user, pull))
            _pullingSystem.TryStopPull(user, pull);

        EntityUid? pulledUid = null;
        if (TryComp<PullerComponent>(user, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
        {
            if (comp.TeleportPulledEntity)
                pulledUid = puller.Pulling;
            else
                _pullingSystem.TryStopPull(puller.Pulling.Value, pullable);
        }

        bool corrupted = CheckCorruption(user, comp);

        var targetFinal = corrupted ? target.Offset(GetCorruptOffset(user, comp)) : target;
        DoTeleport(user, targetFinal, comp.BeamProto, pulledUid, comp.DashSound);

        var now = _timing.CurTime;
        comp.NextUse = now + comp.Cooldown;
        Dirty(uid, comp);
        _actions.SetCooldown(comp.DashActionEntity, comp.Cooldown);
        _alerts.ShowAlert(user, comp.CooldownAlert, cooldown: (now, comp.NextUse), autoRemove: false);
        return true;
    }
    //DS14-end

    //DS14-start
    private void DashPopup(bool predicted, string locKey, EntityUid item, EntityUid user)
    {
        var msg = Loc.GetString(locKey, ("item", item));
        if (predicted)
            _popup.PopupClient(msg, user, user);
        else
            _popup.PopupEntity(msg, user, user);
    }
    //DS14-end

    protected virtual void DoTeleport(EntityUid user, EntityCoordinates target, string? beam = null, EntityUid? pulled = null, SoundSpecifier? sound = null) { } //DS14

    public bool CheckDash(EntityUid uid, EntityUid user)
    {
        var ev = new CheckDashEvent(user);
        RaiseLocalEvent(uid, ref ev);
        return !ev.Cancelled;
    }

    //DS14-start
    private void OnExamined(Entity<DashAbilityComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        if (CheckCorruption(args.Examiner, ent.Comp) && CheckDash(ent, args.Examiner))
        {
            args.PushMarkup(Loc.GetString("dash-ability-corrupted"));
        }
    }

    private bool CheckCorruption(EntityUid user, DashAbilityComponent comp)
    {
        if (!comp.CorruptByBluespaceItems)
            return false;

        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            if (HasCorruptItem(item))
                return true;
        }

        return false;
    }

    private bool HasCorruptItem(EntityUid item, int depth = 0)
    {
        if (_tagSystem.HasTag(item, CorruptTeleportTag))
            return true;

        if (depth >= 3 || !HasComp<ContainerManagerComponent>(item))
            return false;

        foreach (var container in _container.GetAllContainers(item))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasCorruptItem(contained, depth + 1))
                    return true;
            }
        }

        return false;
    }

    private Vector2 GetCorruptOffset(EntityUid user, DashAbilityComponent comp)
    {
        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(user));

        var angle = random.NextDouble() * Math.Tau;
        var dist = comp.CorruptMinDistance + random.NextDouble() * (comp.CorruptMaxDistance - comp.CorruptMinDistance);

        return new Vector2((float) (Math.Cos(angle) * dist), (float) (Math.Sin(angle) * dist));
    }
    //DS14-end
}

/// <summary>
/// Raised on the item before adding the dash action and when using the action.
/// </summary>
[ByRefEvent]
public record struct CheckDashEvent(EntityUid User, bool Cancelled = false);