// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Physics;

namespace Content.Shared.DeadSpace.Abilities.Systems;

public sealed partial class SharedRollingStoneSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RollingStoneActionEvent>(OnAction);
        SubscribeLocalEvent<ActiveRollingStoneComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<ActiveRollingStoneComponent, ProjectileReflectAttemptEvent>(OnProjectileReflect); 
    }

    private void OnProjectileReflect(Entity<ActiveRollingStoneComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        args.Cancelled = false;
    }
    private void OnAction(RollingStoneActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var xform = Transform(performer);
        var direction = xform.WorldRotation.ToWorldVec();

        var active = EnsureComp<ActiveRollingStoneComponent>(performer);
        var reflect = EnsureComp<ReflectComponent>(performer);
        reflect.Reflects = ReflectType.NonEnergy | ReflectType.Energy;
        reflect.ReflectProb = 1f;
        active.EndTime = _timing.CurTime + TimeSpan.FromSeconds(args.Duration);
        active.Direction = direction;
        active.Speed = args.Speed;
        active.Damage = args.Damage;
        active.HitSound = args.HitSound;

        if (TryComp<InputMoverComponent>(performer, out var mover))
        {
            active.OldCanMove = mover.CanMove;
            mover.CanMove = false;
        }

        if (TryComp<PhysicsComponent>(performer, out var physics))
        {
            _physics.SetLinearVelocity(performer, direction * args.Speed, body: physics);
        }

        _popup.PopupPredicted(Loc.GetString("rolling-stone-start-popup"), performer, performer);
        args.Handled = true;
    }

    private void OnCollide(Entity<ActiveRollingStoneComponent> ent, ref StartCollideEvent args)
    {
        if (args.OtherEntity == ent.Owner)
            return;
    
        var uid = ent.Owner;
        var ourPos = _transform.GetWorldPosition(uid);
        var otherPos = _transform.GetWorldPosition(args.OtherEntity);
        var diff = ourPos - otherPos;
    
        if (diff.LengthSquared() < 0.001f)
            diff = Vector2.UnitY;
    
        var normal = diff.Normalized();
        var velocity = _physics.GetMapLinearVelocity(uid);
        var reflected = velocity - 2 * Vector2.Dot(velocity, normal) * normal;
    
        if (reflected.LengthSquared() < 0.01f)
            reflected = normal;
    
        var impactDirection = ent.Comp.Direction;
    
        var newDir = reflected.Normalized();
        _physics.SetLinearVelocity(uid, newDir * ent.Comp.Speed);
        ent.Comp.Direction = newDir;
    
        if (HasComp<ProjectileComponent>(args.OtherEntity) &&
            TryComp<PhysicsComponent>(args.OtherEntity, out var projPhysics))
        {
            _physics.SetBodyType(args.OtherEntity, BodyType.Dynamic, body: projPhysics);
            _physics.SetLinearVelocity(args.OtherEntity, impactDirection * ent.Comp.Speed, body: projPhysics);

            if (TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            {
                projectile.Shooter = uid;
                projectile.Weapon = uid;
            }
            return;
        }
    
        if (TryComp<DamageableComponent>(args.OtherEntity, out _))
        {
            var isMapGeometry = HasComp<MapGridComponent>(args.OtherEntity) ||
                                HasComp<MapComponent>(args.OtherEntity);

            if (!isMapGeometry)
            {
                if (!ent.Comp.DamagedThisTick.Contains(args.OtherEntity))
                {
                    ent.Comp.DamagedThisTick.Add(args.OtherEntity);
                    _damageable.TryChangeDamage(args.OtherEntity, ent.Comp.Damage);
                }
            }
        }
    
        if (ent.Comp.HitSound != null)
            _audio.PlayPredicted(ent.Comp.HitSound, uid, uid);
    
        _popup.PopupPredicted(Loc.GetString("rolling-stone-hit-popup"), uid, uid, PopupType.SmallCaution);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveRollingStoneComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var active, out var physics))
        {
            active.DamagedThisTick.Clear();

            if (_timing.CurTime > active.EndTime)
            {
                if (TryComp<InputMoverComponent>(uid, out var mover))
                    mover.CanMove = active.OldCanMove;

                RemCompDeferred<ActiveRollingStoneComponent>(uid);
                RemCompDeferred<ReflectComponent>(uid);
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
                continue;
            }

            var currentVel = physics.LinearVelocity;
            if (currentVel.LengthSquared() < 0.1f)
            {
                _physics.SetLinearVelocity(uid, active.Direction * active.Speed, body: physics);
            }
            else
            {
                _physics.SetLinearVelocity(uid, currentVel.Normalized() * active.Speed, body: physics);
            }
        }
    }
}