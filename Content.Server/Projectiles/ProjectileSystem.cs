using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Player;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using System.Numerics; //DS-14
using Content.Server.DeadSpace.Physics; //DS-14
using Robust.Shared.Map; //DS-14
using Robust.Shared.Physics; //DS-14
using Robust.Shared.Physics.Components; //DS-14
using Robust.Shared.Physics.Dynamics; //DS-14
using Robust.Shared.Physics.Systems; //DS-14

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private readonly SharedPhysicsSystem _physics = default!; //DS-14

    //DS-14 start
    private const float SweepMinDistance = 0.25f;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _sweptHits = new();
    private readonly List<EntityUid> _deadSweeps = new();
    //DS-14 end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
        UpdatesAfter.Add(typeof(SharedPhysicsSystem)); //DS-14
    }

    //DS-14 start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent, FixturesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var projectile, out var body, out var fixtures, out var xform))
        {
            if (projectile.ProjectileSpent || projectile is { Weapon: null, OnlyCollideWhenShot: true })
                continue;

            if (SweepPath.TryGet(xform, body, frameTime, SweepMinDistance, _transform, out var mapId, out var from, out var to))
                Sweep(uid, projectile, body, fixtures, mapId, from, to);
        }

        foreach (var uid in _sweptHits.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _deadSweeps.Add(uid);
        }

        foreach (var uid in _deadSweeps)
        {
            _sweptHits.Remove(uid);
        }

        _deadSweeps.Clear();
    }

    private void Sweep(
        EntityUid uid,
        ProjectileComponent projectile,
        PhysicsComponent body,
        FixturesComponent fixtures,
        MapId mapId,
        Vector2 from,
        Vector2 to)
    {
        var delta = to - from;
        var distance = delta.Length();
        if (distance < SweepMinDistance || !fixtures.Fixtures.TryGetValue(ProjectileFixture, out var ourFixture))
            return;

        var ray = new CollisionRay(from, delta / distance, ourFixture.CollisionMask);
        foreach (var hit in _physics.IntersectRay(mapId, ray, distance, uid, false))
        {
            if (!CanSweepHit(uid, body, ourFixture, hit.HitEntity))
                continue;

            if (_sweptHits.TryGetValue(uid, out var hits) && hits.Contains(hit.HitEntity))
                continue;

            _transform.SetWorldPosition(uid, hit.HitPos);
            HitTarget(uid, projectile, hit.HitEntity, body.LinearVelocity);

            if (projectile.ProjectileSpent || TerminatingOrDeleted(uid))
                break;
        }
    }

    private bool CanSweepHit(EntityUid uid, PhysicsComponent body, Fixture ourFixture, EntityUid target)
    {
        if (target == uid ||
            HasComp<ProjectileComponent>(target) ||
            !TryComp<PhysicsComponent>(target, out var otherBody) ||
            !otherBody.CanCollide ||
            !TryComp<FixturesComponent>(target, out var otherFixtures))
        {
            return false;
        }

        Fixture? otherFixture = null;
        foreach (var fixture in otherFixtures.Fixtures.Values)
        {
            if (fixture.Hard && (fixture.CollisionLayer & ourFixture.CollisionMask) != 0)
            {
                otherFixture = fixture;
                break;
            }
        }

        if (otherFixture == null)
            return false;

        var ourEv = new PreventCollideEvent(uid, target, body, otherBody, ourFixture, otherFixture);
        RaiseLocalEvent(uid, ref ourEv);
        if (ourEv.Cancelled)
            return false;

        var otherEv = new PreventCollideEvent(target, uid, otherBody, body, otherFixture, ourFixture);
        RaiseLocalEvent(target, ref otherEv);
        return !otherEv.Cancelled;
    }
    //DS-14 end

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
            || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        //DS-14 start
        HitTarget(uid, component, args.OtherEntity, args.OurBody.LinearVelocity);
    }

    private void HitTarget(EntityUid uid, ProjectileComponent component, EntityUid target, Vector2 velocity)
    {
        if (!_sweptHits.TryGetValue(uid, out var hits))
        {
            hits = new HashSet<EntityUid>();
            _sweptHits[uid] = hits;
        }

        if (!hits.Add(target))
            return;
        //DS-14 end

        // DS14-start
        // Filter.Pvs ignores session view subscriptions used by remote eyes.
        var effectOrigin = _transform.GetMapCoordinates(target);
        var effectFilter = Filter.Empty().AddPlayersByPvs(effectOrigin, entManager: EntityManager)
            .AddPlayersByViewSubscriptions(effectOrigin, entityManager: EntityManager);
        // DS14-end

        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            return;
        }

        var damageEv = new BeforeProjectileHitEvent(component.Damage, target, component.Shooter);
        RaiseLocalEvent(uid, ref damageEv);
        var ev = new ProjectileHitEvent(damageEv.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var otherName = ToPrettyString(target);
        var damageRequired = _destructibleSystem.DestroyedAt(target);
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired -= damageableComponent.TotalDamage;
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }
        var deleted = Deleted(target);

        if (_damageableSystem.TryChangeDamage((target, damageableComponent), ev.Damage, out var damage, component.IgnoreResistances, origin: component.Shooter) && Exists(component.Shooter))
        {
            if (!deleted)
            {
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, effectFilter);
            }

            _adminLogger.Add(LogType.BulletHit,
                LogImpact.Medium,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {otherName:target} and dealt {damage:damage} damage");

            component.ProjectileSpent = !TryPenetrate((uid, component), damage, damageRequired);
        }
        else
        {
            component.ProjectileSpent = true;
        }

        if (!deleted)
        {
            _guns.PlayImpactSound(target, damage, component.SoundHit, component.ForceSound);

            if (!velocity.IsLengthZero()) //DS-14
                _sharedCameraRecoil.KickCamera(target, velocity.Normalized()); //DS-14
        }

        if (component.DeleteOnCollide && component.ProjectileSpent)
            QueueDel(uid);

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)), effectFilter);
        }
    }

    private bool TryPenetrate(Entity<ProjectileComponent> projectile, DamageSpecifier damage, FixedPoint2 damageRequired)
    {
        // If penetration is to be considered, we need to do some checks to see if the projectile should stop.
        if (projectile.Comp.PenetrationThreshold == 0)
            return false;

        // If a damage type is required, stop the bullet if the hit entity doesn't have that type.
        if (projectile.Comp.PenetrationDamageTypeRequirement != null)
        {
            foreach (var requiredDamageType in projectile.Comp.PenetrationDamageTypeRequirement)
            {
                if (damage.DamageDict.Keys.Contains(requiredDamageType))
                    continue;

                return false;
            }
        }

        // If the object won't be destroyed, it "tanks" the penetration hit.
        if (damage.GetTotal() < damageRequired)
        {
            return false;
        }

        if (!projectile.Comp.ProjectileSpent)
        {
            projectile.Comp.PenetrationAmount += damageRequired;
            // The projectile has dealt enough damage to be spent.
            if (projectile.Comp.PenetrationAmount >= projectile.Comp.PenetrationThreshold)
            {
                return false;
            }
        }

        return true;
    }
}
