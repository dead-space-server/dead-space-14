using Content.Server.DeadSpace.Armutant.Abilities.Components.ShieldAbilities;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Armutant.Abilities.Shield;

public sealed class StunShieldSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private readonly HashSet<EntityUid> _receivers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShieldStunComponent, StunShieldToggleEvent>(OnStunShieldAction);
        SubscribeLocalEvent<ShieldStunComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShieldStunComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, ShieldStunComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionShieldStunEntity, component.ActionToggleShieldStunSpawn, uid);
    }

    private void OnComponentShutdown(EntityUid uid, ShieldStunComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionShieldStunEntity);
    }

    private void OnStunShieldAction(EntityUid uid, ShieldStunComponent component, StunShieldToggleEvent args)
    {
        var ev = new StunShieldAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        if (ev.Cancelled)
            return;

        if (!TryComp<TransformComponent>(uid, out var xform) || _mobState.IsDead(uid))
        {
            return;
        }

        args.Handled = true;

        _receivers.Clear();
        foreach (var entity in _entityLookup.GetEntitiesInRange(xform.Coordinates, component.Range))
        {
            if (entity == uid)
                continue;

            if (HasComp<ShieldStunComponent>(entity))
                continue;

            if (!HasComp<MobStateComponent>(entity))
                continue;

            _receivers.Add(entity);
        }

        if (_net.IsServer)
            _audio.PlayPvs(component.Sound, uid);

        foreach (var receiver in _receivers)
        {
            if (!TryComp<PhysicsComponent>(receiver, out var physics))
                continue;

            if (_mobState.IsDead(receiver))
                continue;

            _stun.TryParalyze(receiver, component.ParalyzeTime, true);

            var dashDirection = (_transform.GetWorldPosition(receiver) - _transform.GetWorldPosition(uid)).Normalized();
            _physics.SetLinearVelocity(receiver, dashDirection * component.KnockbackForce, body: physics);

            if (_net.IsServer && component.Effect is not null)
                SpawnAttachedTo(component.Effect, Transform(receiver).Coordinates);

            if (xform.Coordinates.TryDistance(EntityManager, Transform(receiver).Coordinates, out var distance) &&
                distance <= component.ShortRange)
            {
                if (!_standing.IsDown(receiver))
                    continue;

                var damage = _damageable.TryChangeDamage(receiver, component.Damage);
                if (damage?.GetTotal() > FixedPoint2.Zero)
                {
                    var filter = Filter.Pvs(receiver, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == uid);
                    _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { receiver }, filter);
                }
            }
        }

        if (_net.IsServer && component.SelfEffect is not null)
            SpawnAttachedTo(component.SelfEffect, Transform(uid).Coordinates);
    }

}
