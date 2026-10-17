// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.DeadSpace.Weapons.Ranged.Upgrades;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Spawners;

namespace Content.Server.DeadSpace.Weapons.Ranged;

public sealed class ExplosivePkaUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly Robust.Shared.Audio.Systems.SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExplosivePkaUpgradeComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<ExplosivePkaProjectileComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<ExplosivePkaProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnGunShot(Entity<ExplosivePkaUpgradeComponent> ent, ref GunShotEvent args)
    {
        // Only on lavaland: anywhere else the shot is an ordinary kinetic bolt.
        if (!HasComp<LavalandMapComponent>(Transform(args.User).MapUid))
            return;

        foreach (var (ammo, _) in args.Ammo)
        {
            if (ammo is not { } projectile ||
                !TryComp<ProjectileComponent>(projectile, out var projectileComp))
                continue;

            projectileComp.Damage += ent.Comp.BulletDamage;
            var explosive = EnsureComp<ExplosivePkaProjectileComponent>(projectile);
            explosive.Radius = ent.Comp.Radius;
            explosive.HumanDamage = ent.Comp.HumanDamage;
            explosive.CreatureDamage = ent.Comp.CreatureDamage;
            explosive.BossDamage = ent.Comp.BossDamage;
            explosive.ExplosionIntensity = ent.Comp.ExplosionIntensity;
            explosive.ExplosionSlope = ent.Comp.ExplosionSlope;
            explosive.ExplosionMaxIntensity = ent.Comp.ExplosionMaxIntensity;

            var origin = _transform.GetMapCoordinates(projectile);
            var requestedTarget = _transform.ToMapCoordinates(args.Target);
            var offset = requestedTarget.Position - origin.Position;
            var distance = MathF.Min(offset.Length(), ent.Comp.MaxRange);
            var direction = offset.LengthSquared() > 0f
                ? offset.Normalized()
                : Transform(args.User).LocalRotation.ToWorldVec();
            explosive.DetonationTarget = new MapCoordinates(
                origin.Position + direction * distance,
                origin.MapId);

            if (TryComp<PhysicsComponent>(projectile, out var projectilePhysics))
                _physics.SetLinearVelocity(
                    projectile,
                    direction * projectilePhysics.LinearVelocity.Length(),
                    body: projectilePhysics);

            if (TryComp<TimedDespawnComponent>(projectile, out var timed) &&
                TryComp<PhysicsComponent>(projectile, out var physics) &&
                physics.LinearVelocity.Length() > 0.01f)
                timed.Lifetime = distance / physics.LinearVelocity.Length() + 1f;
        }
    }

    /// <summary>
    /// Flies over creatures to the cursor, but walls and rock stop it: it blows up where it hits them.
    /// </summary>
    private void OnPreventCollide(Entity<ExplosivePkaProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OtherBody.BodyType == BodyType.Static &&
            args.OtherFixture.Hard &&
            (args.OtherFixture.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
        {
            return;
        }

        args.Cancelled = true;
    }

    private void OnProjectileHit(Entity<ExplosivePkaProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (TerminatingOrDeleted(ent) || ent.Comp.DetonationTarget == null)
            return;

        Detonate(ent, _transform.GetMapCoordinates(ent), args.Shooter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ExplosivePkaProjectileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var explosive, out var physics))
        {
            if (explosive.DetonationTarget is not { } target)
                continue;

            var current = _transform.GetMapCoordinates(uid);
            if (current.MapId != target.MapId)
                continue;

            var reachDistance = physics.LinearVelocity.Length() * frameTime + 0.1f;
            if ((target.Position - current.Position).Length() > reachDistance)
                continue;

            Detonate((uid, explosive), target, Comp<ProjectileComponent>(uid).Shooter);
        }
    }

    private void Detonate(
        Entity<ExplosivePkaProjectileComponent> ent,
        MapCoordinates center,
        EntityUid? shooter)
    {
        // Once only: hitting a wall and reaching the cursor can happen on the same tick.
        ent.Comp.DetonationTarget = null;

        // DS14: the outpost is safe from these blasts - its walls, doors and machines are not even scratched.
        // A wide margin: the blast spreads a few tiles past its visual radius.
        var nearOutpost = IsNearOutpost(center, ent.Comp.Radius + 6f);

        foreach (var victim in _lookup.GetEntitiesInRange(center, ent.Comp.Radius))
        {
            if (victim == ent.Owner)
                continue;

            if (!HasComp<MobStateComponent>(victim) && HasComp<LavalandOutpostComponent>(Transform(victim).GridUid))
                continue;

            var damage = TryDamageFor(victim, ent.Comp, out var specializedDamage)
                ? specializedDamage
                : ent.Comp.CreatureDamage;
            _damage.TryChangeDamage(victim, damage, true, origin: shooter);
        }

        // By the outpost only the blow itself above: no explosion that would tear at its walls.
        if (nearOutpost)
        {
            _audio.PlayPvs(OutpostBlastSound, _transform.ToCoordinates(center));
            QueueDel(ent.Owner);
            return;
        }

        _explosion.QueueExplosion(
            center,
            "LavalandPkaVisual",
            ent.Comp.ExplosionIntensity,
            ent.Comp.ExplosionSlope,
            ent.Comp.ExplosionMaxIntensity,
            shooter,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);

        QueueDel(ent.Owner);
    }

    private static readonly SoundPathSpecifier OutpostBlastSound = new("/Audio/Effects/explosion_small1.ogg");

    /// <summary>The outpost grid is within this distance of the blast.</summary>
    private bool IsNearOutpost(MapCoordinates center, float range)
    {
        var box = Box2.CenteredAround(center.Position, new System.Numerics.Vector2(range * 2f));
        foreach (var grid in _mapManager.FindGridsIntersecting(center.MapId, box))
        {
            if (HasComp<LavalandOutpostComponent>(grid.Owner))
                return true;
        }

        return false;
    }

    private bool TryDamageFor(
        EntityUid target,
        ExplosivePkaProjectileComponent component,
        out global::Content.Shared.Damage.DamageSpecifier damage)
    {
        // Bosses of every kind; the Legion's hands pass their damage on to the Legion, so they count as it.
        if (HasComp<LavalandBossComponent>(target) || HasComp<LavalandEliteComponent>(target) ||
            HasComp<LavalandMadMinerComponent>(target) || HasComp<LavalandLegionHandComponent>(target))
        {
            damage = component.BossDamage;
            return true;
        }

        if (HasComp<LavalandFaunaComponent>(target) || HasComp<NecromorfComponent>(target))
        {
            damage = component.CreatureDamage;
            return true;
        }

        if (HasComp<HumanoidAppearanceComponent>(target))
        {
            damage = component.HumanDamage;
            return true;
        }

        damage = default!;
        return false;
    }
}
