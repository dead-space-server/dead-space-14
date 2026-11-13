// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Husbandry.Walking;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Husbandry.Breeding;
using Content.Shared.DeadSpace.Husbandry.Core;
using Content.Shared.DeadSpace.Husbandry.Feeding;
using Content.Shared.DeadSpace.Husbandry.Sex;
using Content.Shared.DeadSpace.Husbandry.Walking;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.DeadSpace.Husbandry.Breeding;
using Content.Server.DeadSpace.Husbandry.Feeding;

namespace Content.Server.DeadSpace.Husbandry.Wander;

/// <summary>
/// Takes animals for a stroll now and then, see <see cref="AnimalWanderComponent"/>. A stroll is a few legs: each one
/// is a route of one waypoint for the <see cref="AnimalWalkerComponent"/> of the animal, to a place whose way is free.
/// Between legs the animal stops for a moment, the next leg turns a bit from the last. A stuck animal or one the brain
/// takes over ends the stroll.
/// </summary>
public sealed class AnimalWanderSystem : EntitySystem
{
    [Dependency] private readonly AnimalWalkerSystem _walker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
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

        if (!TryComp<AnimalWalkerComponent>(ent, out var walker))
            return Roll(wander.RetryTime);

        // What kind of stroll: round a player, after a mate, round a feeder, or the plain roam.
        PickMode(ent);

        EntityCoordinates place;
        Vector2 heading;
        var found = wander.Mode != WanderMode.Roam
            ? TryFindSocialPlace(ent, walker, out place, out heading)
            : TryFindPlace(ent, walker, null, wander.MinClearProbes, out place, out heading);

        // A social stroll with no way to the subject is a roam.
        if (!found && wander.Mode != WanderMode.Roam)
        {
            wander.Mode = WanderMode.Roam;
            wander.Subject = null;
            found = TryFindPlace(ent, walker, null, wander.MinClearProbes, out place, out heading);
        }

        if (!found)
            return Roll(wander.RetryTime);

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
        ent.Comp.Mode = WanderMode.Roam;
        ent.Comp.Subject = null;
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
        EntityCoordinates place;
        Vector2 heading;
        var found = ent.Comp.Mode != WanderMode.Roam
            ? TryFindSocialPlace(ent, walker, out place, out heading)
            : TryFindPlace(ent, walker, ent.Comp.Heading, 1, out place, out heading);

        if (!found)
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

        var good = _probes;
        good.Clear();
        for (var i = 0; i < wander.Probes; i++)
        {
            var angle = heading is { } last
                ? last.ToAngle() + Angle.FromDegrees(_random.NextFloat(-wander.TurnAngle, wander.TurnAngle))
                : _random.NextAngle();
            var dir = angle.ToVec();
            var wanted = _random.NextFloat(wander.MinDistance, wander.MaxDistance);

            // The free length counts from the middle of the animal, the leg has to stop before its body hits something.
            var room = _walker.FreeLength(ent, mapId, origin, dir, wanted + walker.Clearance, walker.Clearance, mask) - walker.Clearance - wander.LegMargin;
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

    #region Social strolls

    private readonly List<EntityUid> _players = new();
    private readonly HashSet<Entity<AnimalBreedingComponent>> _animals = new();
    private readonly HashSet<Entity<AnimalFeederComponent>> _feeders = new();
    private readonly HashSet<Entity<WaterSourceComponent>> _waters = new();
    private TimeSpan _nextPlayerScan;
    private readonly List<(Vector2 Position, Vector2 Direction)> _probes = new();

    /// <summary>
    /// Rolls what kind of stroll this is and who or what it is round, see <see cref="WanderMode"/>. The subject is
    /// looked for once per stroll, not per leg, so the cost is one small lookup every few seconds of an animal that
    /// the AI is awake for (so one that is near a player).
    /// </summary>
    private void PickMode(Entity<AnimalWanderComponent> ent)
    {
        var wander = ent.Comp;
        wander.Mode = WanderMode.Roam;
        wander.Subject = null;

        var roll = _random.NextFloat();

        if (roll < wander.OrbitChance)
        {
            if (FindNearestPlayer(ent) is { } player)
            {
                wander.Mode = WanderMode.OrbitPlayer;
                wander.Subject = player;
                wander.OrbitAngle = _random.NextFloat(0f, MathF.Tau);
            }

            return;
        }

        roll -= wander.OrbitChance;

        var male = TryComp<AnimalSexComponent>(ent, out var sex) && sex.Sex == AnimalSexKind.Male;
        if (male && roll < wander.FollowMateChance)
        {
            if (FindMate(ent) is { } mate)
            {
                wander.Mode = WanderMode.FollowMate;
                wander.Subject = mate;
            }

            return;
        }

        if (male)
            roll -= wander.FollowMateChance;

        if (roll < wander.HangoutChance)
        {
            if (FindHangout(ent) is { } place)
            {
                wander.Mode = WanderMode.Hangout;
                wander.Subject = place;
            }

            return;
        }

        roll -= wander.HangoutChance;

        // With the herd: after a random animal of the same kind that is near, so the animals of a kind keep together.
        if (roll < wander.HerdChance && FindHerdMember(ent) is { } member)
        {
            wander.Mode = WanderMode.FollowHerd;
            wander.Subject = member;
        }
    }

    private EntityUid? FindNearestPlayer(Entity<AnimalWanderComponent> ent)
    {
        var now = _timing.CurTime;
        if (now >= _nextPlayerScan)
        {
            _nextPlayerScan = now + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.HusbandryPlayerScanInterval));
            _players.Clear();

            var query = EntityQueryEnumerator<ActorComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (!HasComp<GhostComponent>(uid) && _mobState.IsAlive(uid))
                    _players.Add(uid);
            }
        }

        var origin = _transform.GetWorldPosition(ent);
        var mapId = Transform(ent).MapID;
        var range = ent.Comp.SocialRange * ent.Comp.SocialRange;
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        foreach (var player in _players)
        {
            if (TerminatingOrDeleted(player) || Transform(player).MapID != mapId)
                continue;

            var distance = (_transform.GetWorldPosition(player) - origin).LengthSquared();
            if (distance <= range && distance < bestDistance)
            {
                bestDistance = distance;
                best = player;
            }
        }

        return best;
    }

    /// <summary>
    /// The nearest living female of the same kind.
    /// </summary>
    private EntityUid? FindMate(Entity<AnimalWanderComponent> ent)
    {
        if (!TryComp<AnimalBreedingComponent>(ent, out var own))
            return null;

        _animals.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.SocialRange, _animals);

        var origin = _transform.GetWorldPosition(ent);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        foreach (var other in _animals)
        {
            if (other.Owner == ent.Owner ||
                other.Comp.Species != own.Species ||
                !TryComp<AnimalSexComponent>(other, out var sex) ||
                sex.Sex != AnimalSexKind.Female ||
                !_mobState.IsAlive(other))
                continue;

            var distance = (_transform.GetWorldPosition(other) - origin).LengthSquared();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = other;
            }
        }

        return best;
    }

    /// <summary>
    /// A living animal of the same kind that is near, any of them.
    /// </summary>
    private EntityUid? FindHerdMember(Entity<AnimalWanderComponent> ent)
    {
        if (!TryComp<AnimalBreedingComponent>(ent, out var own))
            return null;

        _animals.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.SocialRange, _animals);

        var count = 0;
        EntityUid? pick = null;
        foreach (var other in _animals)
        {
            if (other.Owner == ent.Owner || other.Comp.Species != own.Species || !_mobState.IsAlive(other))
                continue;

            // Reservoir sampling: every candidate is equally likely, nothing is allocated.
            count++;
            if (_random.Next(count) == 0)
                pick = other;
        }

        return pick;
    }

    /// <summary>
    /// A feeder or a water source that is near, any of them.
    /// </summary>
    private EntityUid? FindHangout(Entity<AnimalWanderComponent> ent)
    {
        var coordinates = Transform(ent).Coordinates;
        _feeders.Clear();
        _waters.Clear();
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.SocialRange, _feeders);
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.SocialRange, _waters);

        var count = _feeders.Count + _waters.Count;
        if (count == 0)
            return null;

        var pick = _random.Next(count);
        foreach (var feeder in _feeders)
        {
            if (pick-- == 0)
                return feeder;
        }

        foreach (var water in _waters)
        {
            if (pick-- == 0)
                return water;
        }

        return null;
    }

    /// <summary>
    /// The next place of a social stroll: a point on the circle round the player (the angle goes on from the last
    /// one, so the animal circles), or a point close to the mate or the feeder. It has to be reachable in a straight
    /// line, as for the roam.
    /// </summary>
    private bool TryFindSocialPlace(Entity<AnimalWanderComponent> ent, AnimalWalkerComponent walker, out EntityCoordinates place, out Vector2 direction)
    {
        place = default;
        direction = default;

        var wander = ent.Comp;
        var xform = Transform(ent);
        if (wander.Subject is not { } subject ||
            TerminatingOrDeleted(subject) ||
            xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var origin = _transform.GetWorldPosition(xform);
        var center = _transform.GetWorldPosition(subject);

        Vector2 point;
        if (wander.Mode == WanderMode.OrbitPlayer)
        {
            wander.OrbitAngle += _random.NextFloat(0.9f, 1.9f);
            point = center + new Vector2(MathF.Cos(wander.OrbitAngle), MathF.Sin(wander.OrbitAngle)) * Roll(wander.OrbitRadius);
        }
        else
        {
            point = center + _random.NextAngle().ToVec() * Roll(wander.CompanionDistance);
        }

        var diff = point - origin;
        var distance = diff.Length();
        if (distance < wander.MinSocialStep || distance > wander.SocialRange + wander.SocialRangeSlack)
            return false;

        var dir = diff / distance;
        var mask = _walker.GetHardMask(ent);
        var room = _walker.FreeLength(ent, xform.MapID, origin, dir, distance + walker.Clearance, walker.Clearance, mask) - walker.Clearance - wander.LegMargin;

        if (room < distance ||
            !_walker.IsSolidGround((gridUid, grid), origin, dir, distance) ||
            !_walker.IsTargetAllowed(ent, new MapCoordinates(point, xform.MapID)))
            return false;

        direction = dir;
        place = new EntityCoordinates(gridUid, Vector2.Transform(point, _transform.GetInvWorldMatrix(gridUid)));
        return true;
    }

    #endregion

    private float Roll(Vector2 range)
    {
        return _random.NextFloat(range.X, Math.Max(range.X, range.Y));
    }
}
