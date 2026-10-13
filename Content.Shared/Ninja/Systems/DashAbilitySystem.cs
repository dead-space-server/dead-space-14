using Content.Shared.Actions;
using Content.Shared.Charges.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Robust.Shared.Random;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// Handles dashing logic including charge consumption and checking attempt events.
/// </summary>
//DS14-start
public abstract class SharedDashAbilitySystem : EntitySystem
//DS14-end
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedChargesSystem _sharedCharges = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    //DS14-start
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    private static readonly ProtoId<TagPrototype> CorruptTeleportTag = "CorruptTeleportItem";
    //DS14-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DashAbilityComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<DashAbilityComponent, DashEvent>(OnDash);
        SubscribeLocalEvent<DashAbilityComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DashAbilityComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.DashActionEntity, comp.DashAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<DashAbilityComponent> ent, ref GetItemActionsEvent args)
    {
        if (CheckDash(ent, args.User))
            args.AddAction(ent.Comp.DashActionEntity);
    }

    /// <summary>
    /// Handle charges and teleport to a visible location.
    /// </summary>
    private void OnDash(Entity<DashAbilityComponent> ent, ref DashEvent args)
    {
        var (uid, comp) = ent;
        var user = args.Performer;
        if (!CheckDash(uid, user))
            return;

        if (!_hands.IsHolding(user, uid, out var _))
        {
            _popup.PopupClient(Loc.GetString("dash-ability-not-held", ("item", uid)), user, user);
            return;
        }

        var origin = _transform.GetMapCoordinates(user);
        var target = _transform.ToMapCoordinates(args.Target);
        if (!_examine.InRangeUnOccluded(origin, target, SharedInteractionSystem.MaxRaycastRange, null))
        {
            // can only dash if the destination is visible on screen
            _popup.PopupClient(Loc.GetString("dash-ability-cant-see", ("item", uid)), user, user);
            return;
        }

        if (!_sharedCharges.TryUseCharge(uid))
        {
            _popup.PopupClient(Loc.GetString("dash-ability-no-charges", ("item", uid)), user, user);
            return;
        }

        // Check if the user is BEING pulled, and escape if so
        if (TryComp<PullableComponent>(user, out var pull) && _pullingSystem.IsPulled(user, pull))
            _pullingSystem.TryStopPull(user, pull);

        //DS14-start
        EntityUid? pulledUid = null;
        if (TryComp<PullerComponent>(user, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
        {
            if (ent.Comp.TeleportPulledEntity)
                pulledUid = puller.Pulling;
            else
                _pullingSystem.TryStopPull(puller.Pulling.Value, pullable);
        }

        bool corrupted = false;
        if (ent.Comp.CorruptByBluespaceItems)
        {
            var items = _inventory.GetHandOrInventoryEntities(user);
            foreach (var item in items)
            {
                if (_tagSystem.HasTag(item, CorruptTeleportTag))
                {
                    corrupted = true;
                    break;
                }
            }
        }

        var offset = _random.NextVector2(ent.Comp.CorruptMinDistance, ent.Comp.CorruptMaxDistance);
        var targetFinal = corrupted ? args.Target.Offset(offset) : args.Target;
        DoTeleport(user, targetFinal, ent.Comp.BeamProto, pulledUid);
        args.Handled = true;
        //DS14-end
    }

    protected virtual void DoTeleport(EntityUid user, EntityCoordinates target, string? beam = null, EntityUid? pulled = null) { } //DS14

    public bool CheckDash(EntityUid uid, EntityUid user)
    {
        var ev = new CheckDashEvent(user);
        RaiseLocalEvent(uid, ref ev);
        return !ev.Cancelled;
    }
}

/// <summary>
/// Raised on the item before adding the dash action and when using the action.
/// </summary>
[ByRefEvent]
public record struct CheckDashEvent(EntityUid User, bool Cancelled = false);