using Content.Shared.Actions;
using Content.Shared.Charges.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// Handles dashing logic including charge consumption and checking attempt events.
/// </summary>
public sealed class DashAbilitySystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedChargesSystem _sharedCharges = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    // DS14-start: semantic #45041 port for the legacy dash system on the current engine baseline.
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private readonly HashSet<Entity<PhysicsComponent>> _intersecting = new();
    // DS14-end

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

        // DS14-start: #45041 destination collision check adapted without the newer TeleportAction refactor.
        var targetRotation = _transform.GetWorldRotation(args.Target.EntityId);
        if (IsDestinationBlocked(user, target, targetRotation))
        {
            _popup.PopupClient(Loc.GetString("dash-ability-blocked"), user, user);
            return;
        }
        // DS14-end

        if (!_sharedCharges.TryUseCharge(uid))
        {
            _popup.PopupClient(Loc.GetString("dash-ability-no-charges", ("item", uid)), user, user);
            return;
        }

        // Check if the user is BEING pulled, and escape if so
        if (TryComp<PullableComponent>(user, out var pull) && _pullingSystem.IsPulled(user, pull))
            _pullingSystem.TryStopPull(user, pull);

        // Check if the user is pulling anything, and drop it if so
        if (TryComp<PullerComponent>(user, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
            _pullingSystem.TryStopPull(puller.Pulling.Value, pullable);

        var xform = Transform(user);
        _transform.SetCoordinates(user, xform, args.Target);
        _transform.AttachToGridOrMap(user, xform);
        args.Handled = true;
    }

    // DS14-start: #45041 collision helper adapted to the legacy DashAbility API.
    private bool IsDestinationBlocked(
        EntityUid user,
        MapCoordinates target,
        Angle rotation,
        FixturesComponent? fixtures = null,
        PhysicsComponent? physics = null)
    {
        if (!Resolve(user, ref fixtures, ref physics, false) ||
            !physics.CanCollide ||
            !physics.Hard)
        {
            return false;
        }

        var destinationTransform = new Transform(target.Position, rotation);

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            _intersecting.Clear();
            _lookup.GetEntitiesIntersecting(
                target.MapId,
                fixture.Shape,
                destinationTransform,
                _intersecting,
                LookupFlags.Dynamic | LookupFlags.Static);

            foreach (var other in _intersecting)
            {
                if (other.Owner == user)
                    continue;

                if (_physics.IsCurrentlyHardCollidable(
                        (other.Owner, null, other.Comp),
                        (user, fixtures, physics)))
                {
                    return true;
                }
            }
        }

        return false;
    }
    // DS14-end

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
