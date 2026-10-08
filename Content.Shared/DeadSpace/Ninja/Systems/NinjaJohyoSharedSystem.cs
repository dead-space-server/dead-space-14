// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;
using Robust.Shared.Physics;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedChainGunSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaJohyoComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<NinjaJohyoProjectileComponent, ProjectileEmbedEvent>(OnEmbed);
    }

    private void OnShot(Entity<NinjaJohyoComponent> ent, ref GunShotEvent args)
    {
        foreach (var (uid, _) in args.Ammo)
        {
            if (uid == null || !TryComp<NinjaJohyoProjectileComponent>(uid, out var proj))
                continue;

            proj.Shooter = args.User;
            proj.PullAcceleration = ent.Comp.PullAcceleration;
            proj.MaxPullSpeed = ent.Comp.MaxPullSpeed;
            proj.ArrivalDistance = ent.Comp.ArrivalDistance;
            proj.MaxDistance = ent.Comp.MaxDistance;
        }

        SayPhrase(args.User, ent.Comp);
    }

    private void OnEmbed(Entity<NinjaJohyoProjectileComponent> ent, ref ProjectileEmbedEvent args)
    {
        if (ent.Comp.Shooter == null || !HasComp<PhysicsComponent>(args.Embedded))
        {
            QueueDel(ent);
            return;
        }

        if (args.Weapon == null || !TryComp<NinjaJohyoComponent>(args.Weapon, out var chainGun))
        {
            QueueDel(ent);
            return;
        }

        ent.Comp.Target = args.Embedded;
        ent.Comp.Pulling = true;

        var visuals = EnsureComp<JointVisualsComponent>(ent);
        visuals.Sprite = chainGun.ChainSprite;
        visuals.Target = ent.Comp.Shooter;
        Dirty(ent, visuals);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<NinjaJohyoProjectileComponent>();
        while (query.MoveNext(out var uid, out var proj))
        {
            if (proj.Shooter == null)
                continue;

            var targetPos = _transform.GetWorldPosition(uid);
            var shooterPos = _transform.GetWorldPosition(proj.Shooter.Value);
            var diff = shooterPos - targetPos;
            var distance = diff.Length();

            if (distance >= proj.MaxDistance)
            {
                QueueDel(uid);
                continue;
            }

            if (!proj.Pulling || proj.Target == null)
                continue;

            if (!Exists(proj.Target.Value) || !Exists(proj.Shooter.Value))
            {
                QueueDel(uid);
                continue;
            }

            if (!TryComp<PhysicsComponent>(proj.Target.Value, out var physics))
            {
                QueueDel(uid);
                continue;
            }

            if (physics.BodyType == BodyType.Static)
            {
                QueueDel(uid);
                continue;
            }

            if (distance <= proj.ArrivalDistance)
            {
                QueueDel(uid);
                continue;
            }

            var direction = Vector2.Normalize(diff);
            var currentSpeed = Vector2.Dot(physics.LinearVelocity, direction);

            if (currentSpeed < proj.MaxPullSpeed)
            {
                var impulse = direction * proj.PullAcceleration * frameTime * physics.Mass;
                _physics.ApplyLinearImpulse(proj.Target.Value, impulse, body: physics);
                _physics.WakeBody(proj.Target.Value, body: physics);
            }
        }
    }

    protected virtual void SayPhrase(EntityUid user, NinjaJohyoComponent comp) { }
}