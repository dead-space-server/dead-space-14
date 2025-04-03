using Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAblities;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Examine;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Armutant.Abilities.Blade;

public sealed class ClawsDashSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BladeDashArmutantComponent, BladeDashActionEvent>(OnDash);
        SubscribeLocalEvent<BladeDashArmutantComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BladeDashArmutantComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, BladeDashArmutantComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionDashEntity, component.ActionToggleDashBlade, uid);
    }

    private void OnComponentShutdown(EntityUid uid, BladeDashArmutantComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionDashEntity);
    }

    private void OnDash(Entity<BladeDashArmutantComponent> ent, ref BladeDashActionEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var (uid, comp) = ent;
        var user = args.Performer;

        var origin = _transform.GetMapCoordinates(user);
        var target = args.Target.ToMap(EntityManager, _transform);
        if (!_examine.InRangeUnOccluded(origin, target, comp.MaxDashRange, null))
        {
            _popup.PopupClient(Loc.GetString("claws-dash-ability-cant-see", ("item", uid)), user, user);
            return;
        }

        HandlePullingInteractions(user);

        var userTransform = Transform(user);
        _transform.SetCoordinates(user, userTransform, args.Target);
        _transform.AttachToGridOrMap(user, userTransform);

        ApplyDashEffects(user, target, comp);

        args.Handled = true;
    }

    private void HandlePullingInteractions(EntityUid user)
    {
        if (TryComp<PullableComponent>(user, out var pullable) && _pullingSystem.IsPulled(user, pullable))
            _pullingSystem.TryStopPull(user, pullable);

        if (TryComp<PullerComponent>(user, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullableTarget))
            _pullingSystem.TryStopPull(puller.Pulling.Value, pullableTarget);
    }

    private void ApplyDashEffects(EntityUid user, MapCoordinates target, BladeDashArmutantComponent comp)
    {
        foreach (var entity in _lookup.GetEntitiesInRange(user, comp.CollisionRadius))
        {
            if (entity == user || !TryComp<PhysicsComponent>(entity, out var physics))
                continue;

            if (TryComp<DamageableComponent>(entity, out var damageable))
            {
                _damageable.TryChangeDamage(entity, comp.DashDamage, true);

                if (comp.DashSound != null)
                    _audio.PlayPvs(comp.DashSound, entity);
            }

            var dashDirection = (_transform.GetWorldPosition(entity) - _transform.GetWorldPosition(user)).Normalized();
            _physics.SetLinearVelocity(entity, dashDirection * comp.KnockbackForce, body: physics);

            var effect = Spawn(comp.SelfEffect, Transform(user).Coordinates);
            _transformSystem.SetParent(effect, user);
        }
    }
}



