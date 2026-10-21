// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Steers <see cref="LavalandHomingProjectileComponent"/> projectiles (rude buster) at creatures ahead.</summary>
public sealed class LavalandHomingProjectileSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _candidates = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandHomingProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var homing, out var physics, out var xform))
        {
            var velocity = physics.LinearVelocity;
            var speed = velocity.Length();
            if (speed < 0.01f)
                continue;

            var position = _transform.GetWorldPosition(xform);
            var direction = velocity / speed;
            if (homing.Target is not { } target || !IsValidTarget(uid, target))
                homing.Target = target = FindTarget(uid, homing, position, direction, xform) ?? EntityUid.Invalid;

            if (!target.IsValid())
                continue;

            var toTarget = _transform.GetWorldPosition(target) - position;
            if (toTarget.LengthSquared() < 0.0001f)
                continue;

            var current = MathF.Atan2(direction.Y, direction.X);
            var wanted = MathF.Atan2(toTarget.Y, toTarget.X);
            var delta = MathF.IEEERemainder(wanted - current, MathF.Tau);
            var maxTurn = MathHelper.DegreesToRadians(homing.TurnRate) * frameTime;
            var angle = current + Math.Clamp(delta, -maxTurn, maxTurn);

            var newVelocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            _physics.SetLinearVelocity(uid, newVelocity, body: physics);
        }
    }

    private bool IsValidTarget(EntityUid projectile, EntityUid target)
    {
        return !TerminatingOrDeleted(target) &&
               _mobState.IsAlive(target) &&
               (!TryComp<ProjectileComponent>(projectile, out var proj) || proj.Shooter != target);
    }

    private EntityUid? FindTarget(EntityUid projectile, LavalandHomingProjectileComponent homing, Vector2 position,
        Vector2 direction, TransformComponent xform)
    {
        _candidates.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, homing.Range, _candidates);
        var cosCone = MathF.Cos(MathHelper.DegreesToRadians(homing.ConeAngle));
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var candidate in _candidates)
        {
            if (!IsValidTarget(projectile, candidate))
                continue;

            var offset = _transform.GetWorldPosition(candidate) - position;
            var distance = offset.Length();
            if (distance < 0.01f || Vector2.Dot(offset / distance, direction) < cosCone || distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }
}
