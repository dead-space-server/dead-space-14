using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Player;
using Content.Shared.DeadSpace.Wizard.Runes; //DS-14
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing; //DS-14
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanBasicRaycastSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ISharedAdminLogManager _log = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!; //DS-14

    private EntityQuery<HitscanBasicVisualsComponent> _visualsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _visualsQuery = GetEntityQuery<HitscanBasicVisualsComponent>();

        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
    }

    private void OnHitscanFired(Entity<HitscanBasicRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var mapCords = _transform.ToMapCoordinates(args.FromCoordinates);
        var ray = new CollisionRay(mapCords.Position, args.ShotDirection, (int) ent.Comp.CollisionMask);
        var rayCastResults = _physics.IntersectRay(mapCords.MapId, ray, ent.Comp.MaxDistance, shooter, false);

        var target = args.Target;
        // If you are in a container, use the raycast result
        // Otherwise:
        //  1.) Hit the first entity that you targeted.
        //  2.) Hit the first entity that doesn't require you to aim at it specifically to be hit.
        var ignored = args.IgnoredEntities;
        var result = _container.IsEntityOrParentInContainer(shooter)
            ? rayCastResults.FirstOrNull(hit => ignored?.Contains(hit.HitEntity) != true)
            : rayCastResults.FirstOrNull(hit =>
                ignored?.Contains(hit.HitEntity) != true &&
                (hit.HitEntity == target || CompOrNull<RequireProjectileTargetComponent>(hit.HitEntity)?.Active != true));

        var distanceTried = result?.Distance ?? ent.Comp.MaxDistance;
        var hold = 0f; //DS-14
        var stopInside = TryStopHitscan(ent, mapCords, ref args, ref result, ref distanceTried, out hold); //DS-14

        // DS14-start: aggregate the visual trace and render it after reflection handling.
        var isRoot = false;
        if (args.OutputTrace == null)
        {
            args.OutputTrace = [];
            isRoot = true;
        }

        if (GenerateTraceStep(args.FromCoordinates, distanceTried, args.ShotDirection.ToAngle(), result?.HitEntity) is { } trace)
        {
            trace.HoldSeconds = hold;
            args.OutputTrace.Add(trace);
        }

        if (stopInside)
        {
            if (isRoot)
                FireEffects(ent.Owner, args.OutputTrace);

            return;
        }
        // DS14-end

        // Do visuals without an event. They should always happen and putting it on the attempt event is weird!
        // If more stuff gets added here, it should probably be turned into an event.
        // DS14: visuals are fired after the hit attempt so reflected traces can be rendered together.

        // Admin logging
        if (result?.HitEntity != null && !HasComp<HitscanBasicDamageComponent>(ent.Owner)) // DS14
        {
            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(result.Value.HitEntity):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        var data = new HitscanRaycastFiredData
        {
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Hitscan = ent.Owner,
            Shooter = args.Shooter,
            Target = target,
            HitEntity = result?.HitEntity,
            OutputTrace = args.OutputTrace,
            IgnoredEntities = ignored,
            HitPosition = result is { } hit ? new MapCoordinates(hit.HitPos, mapCords.MapId) : null,
        };

        var attemptEvent = new AttemptHitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref attemptEvent);

        if (attemptEvent.Cancelled)
        {
            if (isRoot)
                FireEffects(ent.Owner, args.OutputTrace);

            return;
        }

        var hitEvent = new HitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref hitEvent);

        if (data.HitEntity != null)
        {
            var strikeEvent = new HitscanRaycastStrikeEvent { Data = data };
            RaiseLocalEvent(data.HitEntity.Value, ref strikeEvent);
        }

        if (isRoot)
            FireEffects(ent.Owner, args.OutputTrace);
    }

    // DS14-start: hitscan trace visuals.
    private HitscanTrace? GenerateTraceStep(EntityCoordinates fromCoordinates, float distance, Angle shotAngle, EntityUid? entity = null)
    {
        var fromXform = Transform(fromCoordinates.EntityId);

        // We'll get the effects relative to the grid / map of the firer
        // Look you could probably optimise this a bit with redundant transforms at this point.

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = _transform.GetWorldPositionRotationInvMatrix(gridXform);
            var map = _transform.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            shotAngle -= gridRot;
        }
        else
        {
            var mapCoords = _transform.ToMapCoordinates(fromCoordinates);
            var mapEnt = Transform(fromCoordinates.EntityId).MapUid;
            if (mapEnt == null)
                return null;

            fromCoordinates = new EntityCoordinates(mapEnt.Value, mapCoords.Position);
        }

        var shotVec = shotAngle.ToVec().Normalized();

        return new HitscanTrace
        {
            Angle = shotAngle,
            Distance = distance,
            MuzzleCoordinates = distance > 1f ? GetNetCoordinates(fromCoordinates.Offset(shotVec / 2f)) : null,
            TravelCoordinates = distance > 1f ? GetNetCoordinates(fromCoordinates.Offset(shotVec * (distance + 0.5f) / 2f)) : null,
            ImpactCoordinates = GetNetCoordinates(fromCoordinates.Offset(shotVec * distance)),
            ImpactedEnt = GetNetEntity(entity),
        };
    }

    private void FireEffects(EntityUid hitscanUid, List<HitscanTrace> traces)
    {
        if (traces.Count == 0 || !_visualsQuery.TryComp(hitscanUid, out var vizComp))
            return;

        var filter = Filter.Empty();
        foreach (var trace in traces)
        {
            var coords = GetCoordinates(trace.MuzzleCoordinates ?? trace.ImpactCoordinates);
            if (!coords.IsValid(EntityManager))
                continue;

            // DS14-start
            // Filter.Pvs ignores session view subscriptions used by remote eyes.
            var mapCoords = _transform.ToMapCoordinates(coords);
            filter.Merge(Filter.Empty().AddPlayersByPvs(mapCoords, entManager: EntityManager)
                .AddPlayersByViewSubscriptions(mapCoords, entityManager: EntityManager));
            // DS14-end
        }

        if (filter.Count == 0)
            return;

        if (vizComp.Bullet == null)
        {
            var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier Sprite, float Distance)>();

            foreach (var trace in traces)
            {
                if (trace.Distance >= 1f)
                {
                    if (vizComp.MuzzleFlash != null && trace.MuzzleCoordinates is { } muzzleCoordinates)
                        sprites.Add((muzzleCoordinates, trace.Angle, vizComp.MuzzleFlash, 1f));

                    if (vizComp.TravelFlash != null && trace.TravelCoordinates is { } travelCoordinates)
                        sprites.Add((travelCoordinates, trace.Angle, vizComp.TravelFlash, trace.Distance - 1.5f));
                }

                if (vizComp.ImpactFlash != null)
                    sprites.Add((trace.ImpactCoordinates, trace.Angle.FlipPositive(), vizComp.ImpactFlash, 1f));
            }

            if (sprites.Count == 0)
                return;

            RaiseNetworkEvent(new SharedGunSystem.HitscanEvent
            {
                Sprites = sprites,
            }, filter);

            return;
        }

        RaiseNetworkEvent(new SharedGunSystem.HitscanEvent
        {
            Traces = traces,
            MuzzleFlash = vizComp.MuzzleFlash,
            TravelFlash = vizComp.TravelFlash,
            ImpactFlash = vizComp.ImpactFlash,
            Bullet = vizComp.Bullet,
            BulletLight = GetLightVisual(hitscanUid),
            Speed = vizComp.Speed,
        }, filter);
    }

    private HitscanLightVisual? GetLightVisual(EntityUid hitscanUid)
    {
        if (!_lights.TryGetLight(hitscanUid, out var light) || !light.Enabled)
            return null;

        return new HitscanLightVisual
        {
            Color = light.Color,
            Radius = light.Radius,
            Energy = light.Energy,
            Softness = light.Softness,
            Falloff = light.Falloff,
            CurveFactor = light.CurveFactor,
            CastShadows = light.CastShadows,
            Offset = light.Offset,
        };
    }

    private bool TryStopHitscan(
        Entity<HitscanBasicRaycastComponent> ent,
        MapCoordinates from,
        ref HitscanTraceEvent args,
        ref RayCastResults? result,
        ref float distanceTried,
        out float hold)
    {
        hold = 0f;
        var direction = args.ShotDirection;
        var length = direction.Length();
        if (length < 0.001f)
            return false;

        direction /= length;
        RuneTimeStopCircleComponent? nearest = null;
        var enter = float.MaxValue;
        var inside = false;

        var query = EntityQueryEnumerator<RuneTimeStopCircleComponent>();
        while (query.MoveNext(out var circle))
        {
            if (circle.MapId != from.MapId || _timing.CurTime >= circle.End)
                continue;

            if ((from.Position - circle.Origin).LengthSquared() <= circle.Radius * circle.Radius)
            {
                nearest = circle;
                inside = true;
                break;
            }

            if (!TryEnterCircle(from.Position, direction, circle.Origin, circle.Radius, out var distance))
                continue;

            if (distance >= distanceTried || distance >= enter)
                continue;

            nearest = circle;
            enter = distance;
        }

        if (nearest == null)
            return false;

        var proto = MetaData(ent.Owner).EntityPrototype?.ID;
        if (string.IsNullOrEmpty(proto))
            return false;

        if (inside)
        {
            distanceTried = 0f;
            result = null;
            hold = (float) (nearest.End - _timing.CurTime).TotalSeconds;
            nearest.Held.Add(new RuneHeldHitscan
            {
                Proto = proto,
                From = from,
                Direction = direction,
                MaxDistance = ent.Comp.MaxDistance,
                Gun = args.Gun,
                Shooter = args.Shooter,
                Target = args.Target,
                Ignored = args.IgnoredEntities is { } ignored ? new List<EntityUid>(ignored) : null,
            });
            return true;
        }

        distanceTried = enter;
        result = null;
        var speed = 315f;
        if (_visualsQuery.TryComp(ent.Owner, out var visuals))
            speed = MathF.Max(visuals.Speed, 1f);

        var flight = enter * 5f / speed;
        hold = MathF.Max(0f, (float) (nearest.End - _timing.CurTime).TotalSeconds - flight);
        var left = ent.Comp.MaxDistance - enter;
        if (left > 0.05f)
        {
            nearest.Held.Add(new RuneHeldHitscan
            {
                Proto = proto,
                From = new MapCoordinates(from.Position + direction * enter, from.MapId),
                Direction = direction,
                MaxDistance = left,
                Gun = args.Gun,
                Shooter = args.Shooter,
                Target = args.Target,
                Ignored = args.IgnoredEntities is { } ignored ? new List<EntityUid>(ignored) : null,
            });
        }

        return false;
    }

    private static bool TryEnterCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, out float distance)
    {
        var from = origin - center;
        var b = 2f * Vector2.Dot(from, direction);
        var c = from.LengthSquared() - radius * radius;
        var disc = b * b - 4f * c;
        distance = 0f;
        if (disc < 0f)
            return false;

        var near = (-b - MathF.Sqrt(disc)) * 0.5f;
        if (near < 0f)
            return false;

        distance = near;
        return true;
    }
    // DS14-end
}
