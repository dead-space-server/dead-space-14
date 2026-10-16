// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Flocks of little whelps (<see cref="LavalandWhelpFlockTriggerComponent"/>): they appear just off a player's
/// screen, mill about, and the moment the player sees them the whole flock scatters in all directions and flies off.
/// </summary>
public sealed class LavalandWhelpFlockSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan TriggerInterval = TimeSpan.FromSeconds(0.5);

    private readonly List<(MapId Map, Vector2 Position)> _viewers = new();
    private readonly List<EntityUid> _toDelete = new();
    private TimeSpan _nextTriggerCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandWhelpFlockTriggerComponent, ComponentShutdown>(OnTriggerShutdown);
    }

    private void OnTriggerShutdown(Entity<LavalandWhelpFlockTriggerComponent> ent, ref ComponentShutdown args)
    {
        // An unloaded chunk takes its sleeping flock with it; a flock already on the wing finishes its flight.
        foreach (var whelp in ent.Comp.Flock)
        {
            if (TryComp<LavalandWhelpFlockComponent>(whelp, out var flock) && flock.Stage == LavalandWhelpFlockStage.Idle)
                QueueDel(whelp);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        CollectViewers();

        if (now >= _nextTriggerCheck)
        {
            _nextTriggerCheck = now + TriggerInterval;
            UpdateTriggers(now);
        }

        UpdateFlocks(now, frameTime);
    }

    private void CollectViewers()
    {
        _viewers.Clear();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob || HasComp<GhostComponent>(mob) ||
                !HasComp<MobStateComponent>(mob) || !_mobState.IsAlive(mob))
            {
                continue;
            }

            var coords = _transform.GetMapCoordinates(mob);
            _viewers.Add((coords.MapId, coords.Position));
        }
    }

    private static bool InView(Vector2 viewer, Vector2 point, Vector2 halfSize, float margin = 0f)
    {
        var delta = point - viewer;
        return MathF.Abs(delta.X) <= halfSize.X + margin && MathF.Abs(delta.Y) <= halfSize.Y + margin;
    }

    private void UpdateTriggers(TimeSpan now)
    {
        var query = EntityQueryEnumerator<LavalandWhelpFlockTriggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trigger, out var xform))
        {
            trigger.Flock.RemoveAll(whelp => TerminatingOrDeleted(whelp));
            if (trigger.Flock.Count > 0 || now < trigger.NextTrigger)
                continue;

            var position = _transform.GetWorldPosition(xform);
            var near = false;
            var seen = false;
            foreach (var (map, viewer) in _viewers)
            {
                if (map != xform.MapID)
                    continue;

                if (InView(viewer, position, trigger.ViewHalfSize, trigger.HiddenMargin + trigger.FlockRadius))
                {
                    seen = true;
                    break;
                }

                if (Vector2.DistanceSquared(viewer, position) <= trigger.SpawnRange * trigger.SpawnRange)
                    near = true;
            }

            // Only behind the edge of every screen around: nobody may see them appear.
            if (!near || seen)
                continue;

            trigger.NextTrigger = now + trigger.Cooldown;
            SpawnFlock((uid, trigger), xform.Coordinates, now);
        }
    }

    private void SpawnFlock(Entity<LavalandWhelpFlockTriggerComponent> trigger, EntityCoordinates center, TimeSpan now)
    {
        var count = _random.Next(trigger.Comp.MinCount, Math.Max(trigger.Comp.MinCount, trigger.Comp.MaxCount) + 1);
        var home = _transform.ToMapCoordinates(center).Position;
        for (var i = 0; i < count; i++)
        {
            var offset = _random.NextAngle().ToVec() * MathF.Sqrt(_random.NextFloat()) * trigger.Comp.FlockRadius;
            var whelp = Spawn(trigger.Comp.Prototype, center.Offset(offset));
            var flock = EnsureComp<LavalandWhelpFlockComponent>(whelp);
            flock.Trigger = trigger.Owner;
            flock.Home = home;
            flock.HomeRadius = trigger.Comp.FlockRadius;
            flock.Stage = LavalandWhelpFlockStage.Idle;
            flock.WanderTarget = home + offset;
            flock.LastSeenNearby = now;
            _transform.SetLocalRotation(whelp, _random.NextAngle());
            trigger.Comp.Flock.Add(whelp);
        }
    }

    private void UpdateFlocks(TimeSpan now, float frameTime)
    {
        _toDelete.Clear();
        var query = EntityQueryEnumerator<LavalandWhelpFlockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flock, out var xform))
        {
            // Going away with its unloaded chunk (or its map): nothing to move any more.
            if (TerminatingOrDeleted(uid) || !xform.ParentUid.IsValid() || TerminatingOrDeleted(xform.ParentUid) ||
                xform.MapID == MapId.Nullspace)
            {
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            switch (flock.Stage)
            {
                case LavalandWhelpFlockStage.Idle:
                    if (TryGetSpotter(xform.MapID, position, flock, now, out var spotter))
                    {
                        Startle(uid, flock, spotter, now);
                        break;
                    }

                    if (now - flock.LastSeenNearby > flock.IdleLifetime)
                    {
                        _toDelete.Add(uid);
                        break;
                    }

                    Wander(uid, flock, position, frameTime);
                    break;

                case LavalandWhelpFlockStage.Startled:
                    if (now < flock.StageEnd)
                        break;

                    flock.Stage = LavalandWhelpFlockStage.Scatter;
                    flock.StageEnd = now + flock.ScatterTime;
                    if (flock.ScatterSound != null && _random.Prob(0.35f))
                        _audio.PlayPvs(flock.ScatterSound, uid);
                    break;

                case LavalandWhelpFlockStage.Scatter:
                    _transform.SetWorldPosition(uid, position + flock.Velocity * frameTime);
                    if (now >= flock.StageEnd)
                        StartRising(uid, flock, now);
                    break;

                case LavalandWhelpFlockStage.Rising:
                    // Still drifting on while climbing.
                    _transform.SetWorldPosition(uid, position + flock.Velocity * 0.5f * frameTime);
                    if (now >= flock.StageEnd)
                        _toDelete.Add(uid);
                    break;
            }
        }

        foreach (var uid in _toDelete)
        {
            QueueDel(uid);
        }
    }

    /// <summary>A player whose screen this whelp is on; also keeps the flock alive while somebody is around.</summary>
    private bool TryGetSpotter(MapId map, Vector2 position, LavalandWhelpFlockComponent flock, TimeSpan now, out Vector2 spotter)
    {
        spotter = default;
        var halfSize = TryComp<LavalandWhelpFlockTriggerComponent>(flock.Trigger, out var trigger)
            ? trigger.ViewHalfSize
            : new Vector2(11f, 8f);

        foreach (var (viewerMap, viewer) in _viewers)
        {
            if (viewerMap != map)
                continue;

            if (Vector2.DistanceSquared(viewer, position) < 40f * 40f)
                flock.LastSeenNearby = now;

            if (!InView(viewer, position, halfSize))
                continue;

            spotter = viewer;
            return true;
        }

        return false;
    }

    /// <summary>The whole flock is startled at once, each one away from the player with its own spread.</summary>
    private void Startle(EntityUid seenWhelp, LavalandWhelpFlockComponent seen, Vector2 spotter, TimeSpan now)
    {
        if (seen.Trigger is not { } triggerUid || !TryComp<LavalandWhelpFlockTriggerComponent>(triggerUid, out var trigger))
        {
            StartleOne(seenWhelp, seen, spotter, now);
            return;
        }

        foreach (var whelp in trigger.Flock)
        {
            if (TryComp<LavalandWhelpFlockComponent>(whelp, out var flock) && flock.Stage == LavalandWhelpFlockStage.Idle)
                StartleOne(whelp, flock, spotter, now);
        }
    }

    private void StartleOne(EntityUid uid, LavalandWhelpFlockComponent flock, Vector2 spotter, TimeSpan now)
    {
        var position = _transform.GetWorldPosition(uid);
        var away = position - spotter;
        var angle = away.LengthSquared() > 0.01f ? away.ToWorldAngle() : _random.NextAngle();
        angle += Angle.FromDegrees(_random.NextFloat(-flock.ScatterSpread, flock.ScatterSpread));

        var direction = angle.ToWorldVec();
        flock.Velocity = direction * _random.NextFloat(flock.ScatterSpeedMin, MathF.Max(flock.ScatterSpeedMin, flock.ScatterSpeedMax));
        flock.Stage = LavalandWhelpFlockStage.Startled;
        flock.StageEnd = now + TimeSpan.FromSeconds(_random.NextDouble() * flock.MaxStartleDelay.TotalSeconds);
        _transform.SetLocalRotation(uid, angle);
    }

    /// <summary>Slowly from spot to spot within the flock's circle, facing the way it goes.</summary>
    private void Wander(EntityUid uid, LavalandWhelpFlockComponent flock, Vector2 position, float frameTime)
    {
        var toTarget = flock.WanderTarget - position;
        var step = flock.WanderSpeed * frameTime;
        if (toTarget.LengthSquared() <= step * step)
        {
            // Sometimes they just sit for a moment.
            if (_random.Prob(0.02f))
                flock.WanderTarget = flock.Home + _random.NextAngle().ToVec() * MathF.Sqrt(_random.NextFloat()) * flock.HomeRadius;
            return;
        }

        var direction = Vector2.Normalize(toTarget);
        _transform.SetWorldPosition(uid, position + direction * step);
        _transform.SetLocalRotation(uid, direction.ToWorldAngle());
    }

    private void StartRising(EntityUid uid, LavalandWhelpFlockComponent flock, TimeSpan now)
    {
        flock.Stage = LavalandWhelpFlockStage.Rising;
        flock.StageEnd = now + TimeSpan.FromSeconds(flock.RiseTime);

        // Straight up on the screen: the sprite offset follows the entity rotation.
        _transform.SetLocalRotation(uid, Angle.Zero);
        var drop = EnsureComp<LavalandSkyDropComponent>(uid);
        drop.Start = now;
        drop.Duration = flock.RiseTime;
        drop.Height = flock.RiseHeight;
        drop.Rising = true;
        Dirty(uid, drop);
    }
}
