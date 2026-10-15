// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Beam;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandLegionSentinelSystem : EntitySystem
{
    private static readonly Vector2[] Cardinals = [Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY];

    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandLegionSentinelComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sentinel, out var xform))
        {
            if (sentinel.NextStage == TimeSpan.Zero)
            {
                sentinel.NextStage = now + sentinel.AimDelay;
                continue;
            }

            if (now < sentinel.NextStage)
                continue;

            switch (sentinel.Stage)
            {
                case 0:
                    Aim(uid, sentinel, xform);
                    sentinel.Stage = 1;
                    sentinel.NextStage = now + sentinel.ShotDelay;
                    break;
                case 1:
                    Fire(uid, sentinel, xform);
                    sentinel.Stage = 2;
                    sentinel.NextStage = now + sentinel.LingerTime;
                    break;
                default:
                    QueueDel(uid);
                    break;
            }
        }
    }

    private void Aim(EntityUid uid, LavalandLegionSentinelComponent sentinel, TransformComponent xform)
    {
        var position = _transform.GetWorldPosition(xform);
        Vector2? best = null;
        var bestDistance = sentinel.VisionRange * sentinel.VisionRange;

        _mobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, sentinel.VisionRange, _mobs);
        foreach (var mob in _mobs)
        {
            if (!IsTarget(mob))
                continue;

            var offset = _transform.GetWorldPosition(mob) - position;
            var distance = offset.LengthSquared();
            if (distance > bestDistance || distance < 0.01f)
                continue;

            bestDistance = distance;
            best = offset;
        }

        sentinel.Direction = best is { } direction ? Vector2.Normalize(direction) : _random.Pick(Cardinals);
        DrawBeam(uid, sentinel, xform, sentinel.TracerPrototype);
        if (sentinel.AimSound != null)
            _audio.PlayPvs(sentinel.AimSound, uid);
    }

    private void Fire(EntityUid uid, LavalandLegionSentinelComponent sentinel, TransformComponent xform)
    {
        var start = _transform.GetWorldPosition(xform);
        var end = start + sentinel.Direction * sentinel.BeamRange;

        _mobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, sentinel.BeamRange + sentinel.BeamWidth, _mobs);
        foreach (var mob in _mobs)
        {
            if (!IsTarget(mob) ||
                DistanceToSegment(_transform.GetWorldPosition(mob), start, end) > sentinel.BeamWidth)
            {
                continue;
            }

            // The pulse pierces everything on its line.
            _damageable.TryChangeDamage(mob.Owner, sentinel.Damage, origin: uid);
        }

        DrawBeam(uid, sentinel, xform, sentinel.BeamPrototype);
        if (sentinel.FireSound != null)
            _audio.PlayPvs(sentinel.FireSound, uid);
    }

    private void DrawBeam(EntityUid uid, LavalandLegionSentinelComponent sentinel, TransformComponent xform, string prototype)
    {
        var endpoint = Spawn("LavalandLegionBeamEndpoint",
            xform.Coordinates.Offset(sentinel.Direction * sentinel.BeamRange));
        _beam.TryCreateBeam(uid, endpoint, prototype);
    }

    private bool IsTarget(Entity<MobStateComponent> mob)
    {
        // Lavaland creatures (Legion itself, its skulls, fauna) are spared, like the mining faction in tg.
        return !HasComp<LavalandFaunaComponent>(mob) &&
               !HasComp<LavalandLegionMegafaunaComponent>(mob) &&
               !HasComp<GhostComponent>(mob) &&
               _mobState.IsAlive(mob, mob.Comp);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared < 0.0001f)
            return Vector2.Distance(point, start);

        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, start + segment * t);
    }
}
