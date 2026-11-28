// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Physics.Components;

namespace Content.Server.DeadSpace.Weapons.Ranged;

public sealed partial class ProjectileBurstSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _bursting = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileBurstComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ProjectileBurstComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        ent.Comp.OriginMap = xform.MapID;
        ent.Comp.Origin = _transform.GetWorldPosition(xform);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _bursting.Clear();
        var query = EntityQueryEnumerator<ProjectileBurstComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var burst, out var xform))
        {
            // A slug that already hit something this tick is spent: it must not also burst on the same target.
            if (TerminatingOrDeleted(uid) || CompOrNull<ProjectileComponent>(uid)?.ProjectileSpent == true)
                continue;

            if (xform.MapID != burst.OriginMap ||
                (_transform.GetWorldPosition(xform) - burst.Origin).LengthSquared() < burst.Distance * burst.Distance)
            {
                continue;
            }

            _bursting.Add(uid);
        }

        foreach (var uid in _bursting)
        {
            Burst(uid, Comp<ProjectileBurstComponent>(uid));
        }
    }

    private void Burst(EntityUid uid, ProjectileBurstComponent burst)
    {
        var velocity = TryComp<PhysicsComponent>(uid, out var physics) ? physics.LinearVelocity : default;
        var heading = velocity.LengthSquared() > 0.0001f
            ? velocity.ToWorldAngle()
            : _transform.GetWorldRotation(uid);

        var projectile = CompOrNull<ProjectileComponent>(uid);
        // The slug is gone the moment it bursts: it must not also collide later this tick before deletion.
        if (projectile != null)
            projectile.ProjectileSpent = true;
        RemComp<ProjectileBurstComponent>(uid);

        var gun = projectile?.Weapon ?? projectile?.Shooter ?? uid;
        var from = Transform(uid).Coordinates;

        for (var i = 0; i < burst.Count; i++)
        {
            var share = burst.Count == 1 ? 0.5 : i / (double) (burst.Count - 1);
            var angle = heading - burst.Spread / 2 + burst.Spread * share;
            var pellet = SpawnAtPosition(burst.Pellet, from);
            var trace = new HitscanTraceEvent
            {
                FromCoordinates = from,
                ShotDirection = angle.ToWorldVec(),
                Gun = gun,
                Shooter = projectile?.Shooter,
            };

            RaiseLocalEvent(pellet, ref trace);
            Del(pellet);
        }

        QueueDel(uid);
    }
}
