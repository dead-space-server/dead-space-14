using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Physics;
using Robust.Shared.Physics.Events;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.ShieldDash.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Standing;
using Content.Shared.Gravity;
using Content.Shared.Throwing;
using Content.Shared.Damage.Systems;
using System.Numerics;
using Robust.Shared.Random;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Items;

namespace Content.Shared.DeadSpace.ShieldDash;

public sealed partial class ShieldDashSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly FixtureSystem _fixtureSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShieldDashComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<ShieldDashComponent, HandSelectedEvent>(OnSelect);

        SubscribeLocalEvent<ShieldDashComponent, GotUnequippedHandEvent>(OnUnequip);
        SubscribeLocalEvent<ShieldDashComponent, DroppedEvent>(OnDrop);

        SubscribeLocalEvent<ShieldDashComponent, GetItemActionsEvent>(OnGetActions);

        SubscribeLocalEvent<ShieldDashComponent, ShieldDashEvent>(OnDash);

        SubscribeLocalEvent<ShieldDashUserComponent, StartCollideEvent>(OnUserCollide);
        SubscribeLocalEvent<ShieldDashUserComponent, LandEvent>(OnUserLand);
        SubscribeLocalEvent<ShieldDashUserComponent, StopThrowEvent>(OnUserStopThrow);
    }

    private void OnMapInit(EntityUid uid, ShieldDashComponent comp, MapInitEvent args)
    {
        _actionContainer.EnsureAction(uid, ref comp.DashActionEntity, comp.DashAction);
    }

    private void OnSelect(EntityUid uid, ShieldDashComponent comp, HandSelectedEvent args)
    {
        comp.User = args.User;

        if (TryComp<PhysicsComponent>(args.User, out var physics) && physics.BodyType != BodyType.Static)
        {
            var userComp = EnsureComp<ShieldDashUserComponent>(args.User);
            userComp.Shield = uid;
        }
    }

    private void OnUnequip(EntityUid uid, ShieldDashComponent comp, GotUnequippedHandEvent args)
    {
        RemoveShieldTracking(uid, comp, args.User);
    }

    private void OnDrop(EntityUid uid, ShieldDashComponent comp, DroppedEvent args)
    {
        RemoveShieldTracking(uid, comp, args.User);
    }

    private void RemoveShieldTracking(EntityUid uid, ShieldDashComponent comp, EntityUid user)
    {
        comp.User = null;

        if (!TryComp<ShieldDashUserComponent>(user, out var userComp) || userComp.Shield != uid)
            return;

        StopDash(user, userComp);

        if (TryFindHeldShield(user, uid, out var nextShield))
        {
            userComp.Shield = nextShield;

            if (TryComp<ShieldDashComponent>(nextShield, out var nextShieldComp))
                nextShieldComp.User = user;

            return;
        }

        RemComp<ShieldDashUserComponent>(user);
    }

    private bool TryFindHeldShield(EntityUid user, EntityUid except, out EntityUid shield)
    {
        shield = default;

        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (held == except || !HasComp<ShieldDashComponent>(held))
                continue;

            shield = held;
            return true;
        }

        return false;
    }

    private void OnGetActions(EntityUid uid, ShieldDashComponent comp, GetItemActionsEvent args)
    {
        if (!args.InHands)
            return;
        args.AddAction(ref comp.DashActionEntity, comp.DashAction);
    }

    private void OnDash(EntityUid uid, ShieldDashComponent comp, ShieldDashEvent args)
    {
        if (!TryComp<ShieldDashUserComponent>(args.Performer, out var user))
            return;

        if (!CanDash(uid, comp, args.Performer))
            return;

        for (var i = 0; i < comp.NeedFreeHands; i++)
        {
            _virtualItem.TrySpawnVirtualItemInHand(uid, args.Performer);
        }

        var xform = Transform(args.Performer);
        var throwing = xform.LocalRotation.ToWorldVec() * comp.DashLength;
        var direction = xform.Coordinates.Offset(throwing);

        _throwing.TryThrow(args.Performer, direction, comp.DashSpeed, animated: false, user: uid);

        _audio.PlayPvs(comp.DashSound, args.Performer);

        if (TryComp<PhysicsComponent>(args.Performer, out var physics))
        {
            _fixtureSystem.TryCreateFixture(args.Performer,
                comp.Shape,
                ShieldDashComponent.DashFixtureID,
                hard: true,
                collisionLayer: (int)CollisionGroup.MobLayer,
                body: physics);
        }

        args.Handled = true;
    }

    private void OnUserCollide(EntityUid uid, ShieldDashUserComponent comp, ref StartCollideEvent args)
    {
        if (args.OurFixtureId == ShieldDashComponent.DashFixtureID)
        {
            if (TryComp<ShieldDashComponent>(comp.Shield, out var shield))
            {
                if (HasComp<DamageableComponent>(args.OtherEntity))
                    _damageableSystem.TryChangeDamage(args.OtherEntity, shield.Damage, ignoreResistances: shield.IgnoreResistances, origin: uid);

                if (HasComp<DamageableComponent>(comp.Shield.Value))
                    _damageableSystem.TryChangeDamage(comp.Shield.Value, shield.DamageToShield, ignoreResistances: true, origin: comp.Shield);

                _stamina.TakeStaminaDamage(uid, shield.StaminaDamageToUser, source: comp.Shield);
                _stamina.TakeStaminaDamage(args.OtherEntity, shield.StaminaDamage, source: uid);

                if (_random.Prob(shield.DisarmChance) &&
                    (!_hands.TryGetActiveItem(args.OtherEntity, out var activeItem) ||
                     !HasComp<PreventForcedDropComponent>(activeItem.Value)))
                {
                    _hands.TryDrop(args.OtherEntity);
                }

                _audio.PlayPvs(shield.ImpactSound, uid);
            }

            StopDash(uid, comp);

            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        }
    }

    private void OnUserLand(EntityUid uid, ShieldDashUserComponent comp, ref LandEvent args)
    {
        StopDash(uid, comp);
    }

    private void OnUserStopThrow(EntityUid uid, ShieldDashUserComponent comp, ref StopThrowEvent args)
    {
        StopDash(uid, comp);
    }

    private bool CanDash(EntityUid shield, ShieldDashComponent comp, EntityUid user)
    {
        if (TryComp<PhysicsComponent>(user, out var physics) && physics.BodyType == BodyType.Static)
        {
            _popupSystem.PopupClient(Loc.GetString("shield-dash-cannot-dash"), user, user);
            return false;
        }

        if (!TryComp<HandsComponent>(user, out var hands))
        {
            _popupSystem.PopupClient(Loc.GetString("shield-dash-cannot-dash"), user, user);
            return false;
        }

        if (_hands.GetActiveItem(user) != shield)
        {
            _popupSystem.PopupClient(Loc.GetString("shield-dash-cannot-dash-not-in-hands"), user, user);
            return false;
        }

        if (_hands.CountFreeHands((user, hands)) < comp.NeedFreeHands)
        {
            _popupSystem.PopupClient(Loc.GetString("shield-dash-cannot-dash-not-enough-hands"), user, user);
            return false;
        }

        if (_gravity.IsWeightless(user) || _standing.IsDown(user))
        {
            _popupSystem.PopupClient(Loc.GetString("shield-dash-cannot-dash"), user, user);
            return false;
        }

        return true;
    }

    private void StopDash(EntityUid user, ShieldDashUserComponent comp)
    {
        if (comp.Shield != null)
            _virtualItem.DeleteInHandsMatching(user, comp.Shield.Value);

        if (TryComp<PhysicsComponent>(user, out var physics))
            _fixtureSystem.DestroyFixture(user, ShieldDashComponent.DashFixtureID, body: physics);
    }
}
