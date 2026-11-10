// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Husbandry.Walking;
using Content.Shared.DeadSpace.Husbandry.Walking;
using Content.Shared.DeadSpace.Husbandry.Wander;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Husbandry.Wander;

/// <summary>
/// Takes animals for a stroll now and then, see <see cref="AnimalWanderComponent"/>. A stroll is a few legs: each one
/// is a route of one waypoint for the <see cref="AnimalWalkerComponent"/> of the animal, to a place whose way is free.
/// Between legs the animal stops for a moment, the next leg turns a bit from the last. A stuck animal or one the brain
/// takes over ends the stroll.
/// </summary>
public sealed class AnimalWanderSystem : EntitySystem
{
    /// <summary>
    /// How much room is kept between the end of a leg and what stops it.
    /// </summary>
    private const float Margin = 0.2f;

    [Dependency] private readonly AnimalWalkerSystem _walker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// Whether the animal goes for a stroll: rolls the chance, looks for room and starts the first leg.
    /// </summary>
    /// <returns>How long it should stand around afterwards, in seconds: the usual time, or a short one if there
    /// was no room and it should look again soon.</returns>
    public float StartOrStand(Entity<AnimalWanderComponent> ent)
    {
        var wander = ent.Comp;
        if (IsWalking(ent) || !_random.Prob(wander.WalkChance))
            return Roll(wander.StandTime);

        if (!TryComp<AnimalWalkerComponent>(ent, out var walker) ||
            !TryFindPlace(ent, walker, null, wander.MinClearProbes, out var place, out var heading))
        {
            return Roll(wander.RetryTime);
        }

        wander.WalkStart = _timing.CurTime;
        wander.LegsLeft = _random.Next(wander.Legs.X, Math.Max(wander.Legs.X, wander.Legs.Y) + 1);
        BeginLeg(ent, walker, place, heading);
        return Roll(wander.StandTime);
    }

    /// <summary>
    /// Whether a stroll is going on, walking or stopping between two legs.
    /// </summary>
    public bool IsWalking(Entity<AnimalWanderComponent> ent)
    {
        return ent.Comp.LegsLeft > 0;
    }

    /// <summary>
    /// Ends the stroll, if there is one.
    /// </summary>
    public void StopWalk(Entity<AnimalWanderComponent> ent)
    {
        ent.Comp.LegsLeft = 0;
        ent.Comp.PauseUntil = null;

        if (TryComp<AnimalWalkerComponent>(ent, out var walker))
            _walker.Stop((ent, walker));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AnimalWanderComponent, AnimalWalkerComponent>();
        while (query.MoveNext(out var uid, out var wander, out var walker))
        {
            if (wander.LegsLeft <= 0)
                continue;

            var ent = (uid, wander);
            if (now - wander.WalkStart > wander.MaxWalkTime)
            {
                StopWalk(ent);
                continue;
            }

            if (wander.PauseUntil is { } until)
            {
                if (now >= until)
                    NextLeg(ent, walker);

                continue;
            }

            switch (walker.Status)
            {
                case WalkStatus.Arrived:
                    EndLeg(ent, walker);
                    break;
                case WalkStatus.Walking:
                    break;
                // Stuck, dragged away or taken over: the stroll is over.
                default:
                    StopWalk(ent);
                    break;
            }
        }
    }

    /// <summary>
    /// The leg is over: the animal stops for a moment and then goes on, or the stroll is over.
    /// </summary>
    private void EndLeg(Entity<AnimalWanderComponent> ent, AnimalWalkerComponent walker)
    {
        var wander = ent.Comp;
        wander.LegsLeft--;

        if (wander.LegsLeft <= 0)
        {
            StopWalk(ent);
            return;
        }

        _walker.Stop((ent, walker));
        wander.PauseUntil = _timing.CurTime + TimeSpan.FromSeconds(Roll(wander.LegPause));
    }

    private void NextLeg(Entity<AnimalWanderComponent> ent, AnimalWalkerComponent walker)
    {
        ent.Comp.PauseUntil = null;

        // The next leg only needs one good place, the room was looked at when the stroll began.
        if (!TryFindPlace(ent, walker, ent.Comp.Heading, 1, out var place, out var heading))
        {
            StopWalk(ent);
            return;
        }

        BeginLeg(ent, walker, place, heading);
    }

    private void BeginLeg(Entity<AnimalWanderComponent> ent, AnimalWalkerComponent walker, EntityCoordinates place, Vector2 heading)
    {
        ent.Comp.Heading = heading;

        if (!_walker.StartRoute((ent, walker), new[] { place }, ent.Comp.ArriveDistance))
            StopWalk(ent);
    }

    /// <summary>
    /// Looks in a few random directions, any or, with a heading, turned away from it by at most the turn angle. A
    /// direction is good if there is room for a leg of at least the shortest length: the free strip as wide as the
    /// animal is long enough and the ground on the way is solid. The leg goes as far as it is meant to, or as far as
    /// the room allows. If enough directions are good there is room to walk, and one of them is where the animal goes.
    /// </summary>
    private bool TryFindPlace(Entity<AnimalWanderComponent> ent, AnimalWalkerComponent walker, Vector2? heading, int minClear, out EntityCoordinates place, out Vector2 direction)
    {
        place = default;
        direction = default;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var wander = ent.Comp;
        var origin = _transform.GetWorldPosition(xform);
        var mapId = xform.MapID;
        var mask = _walker.GetHardMask(ent);

        var good = new List<(Vector2 Position, Vector2 Direction)>();
        for (var i = 0; i < wander.Probes; i++)
        {
            var angle = heading is { } last
                ? last.ToAngle() + Angle.FromDegrees(_random.NextFloat(-wander.TurnAngle, wander.TurnAngle))
                : _random.NextAngle();
            var dir = angle.ToVec();
            var wanted = _random.NextFloat(wander.MinDistance, wander.MaxDistance);

            // The free length counts from the middle of the animal, the leg has to stop before its body hits something.
            var room = _walker.FreeLength(ent, mapId, origin, dir, wanted + walker.Clearance, walker.Clearance, mask) - walker.Clearance - Margin;
            var distance = Math.Min(wanted, room);

            if (distance >= wander.MinLegDistance &&
                _walker.IsSolidGround((gridUid, grid), origin, dir, distance) &&
                _walker.IsTargetAllowed(ent, new MapCoordinates(origin + dir * distance, mapId)))
            {
                good.Add((origin + dir * distance, dir));
            }
        }

        if (good.Count < minClear || good.Count == 0)
            return false;

        var chosen = _random.Pick(good);
        direction = chosen.Direction;

        // In the coordinates of the grid, so the place stays where it is if the grid moves.
        place = new EntityCoordinates(gridUid, Vector2.Transform(chosen.Position, _transform.GetInvWorldMatrix(gridUid)));
        return true;
    }

    private float Roll(Vector2 range)
    {
        return _random.NextFloat(range.X, Math.Max(range.X, range.Y));
    }
}
