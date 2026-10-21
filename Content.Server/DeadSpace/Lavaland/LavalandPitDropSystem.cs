// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Things thrown into a way down (the Celestial pit, the chromite caves pit) fall for a while and then land below:
/// dropping out of the sky in front of whoever is there, or - nobody there to see it - just lying on the floor,
/// no animation to run.
/// </summary>
public sealed class LavalandPitDropSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly Robust.Shared.Containers.SharedContainerSystem _containers = default!;
    [Dependency] private readonly Robust.Shared.Physics.Systems.SharedPhysicsSystem _physics = default!;

    /// <summary>How long a thing shrinks away into the pit before it is gone down it.</summary>
    private const float SinkDuration = 0.6f;

    /// <summary>How long a thing falls before it lands.</summary>
    public static readonly TimeSpan FallTime = TimeSpan.FromSeconds(10);

    private const float DropDuration = 0.8f;
    private const float DropHeight = 9f;

    /// <summary>Players this close to where it lands see it fall.</summary>
    private const float WatchRange = 16f;

    private readonly List<PendingDrop> _pending = new();
    private readonly List<(EntityUid Uid, TimeSpan Until)> _dropping = new();

    private sealed class PendingDrop
    {
        public EntityUid Item;
        /// <summary>Still sinking into the pit until then, where it fell in.</summary>
        public TimeSpan? GoneAt;
        public MapCoordinates Holding;
        public TimeSpan LandAt;
        public MapCoordinates Destination;
        public float Spread;
    }

    /// <summary>
    /// The thing is gone down the pit: it waits out of sight at <paramref name="holding"/> and lands around
    /// <paramref name="destination"/> after <see cref="FallTime"/>.
    /// </summary>
    public void Drop(EntityUid item, MapCoordinates holding, MapCoordinates destination, float spread = 2f)
    {
        foreach (var pending in _pending)
        {
            if (pending.Item == item)
                return;
        }

        // First it sinks into the pit where it fell in, as into any chasm; only then is it gone down.
        var now = _timing.CurTime;
        _physics.SetLinearVelocity(item, Vector2.Zero);
        var sink = EnsureComp<LavalandSkyDropComponent>(item);
        sink.Start = now;
        sink.Duration = SinkDuration;
        sink.Sinking = true;
        sink.Rising = false;
        Dirty(item, sink);

        _pending.Add(new PendingDrop
        {
            Item = item,
            GoneAt = now + TimeSpan.FromSeconds(SinkDuration),
            Holding = holding,
            LandAt = now + TimeSpan.FromSeconds(SinkDuration) + FallTime,
            Destination = destination,
            Spread = spread,
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            if (pending.GoneAt is { } goneAt)
            {
                if (now < goneAt)
                    continue;

                pending.GoneAt = null;
                if (TerminatingOrDeleted(pending.Item))
                {
                    _pending.RemoveAt(i);
                    continue;
                }

                RemComp<LavalandSkyDropComponent>(pending.Item);

                // Snatched up while it was going down: it stays with whoever caught it.
                if (_containers.IsEntityInContainer(pending.Item))
                {
                    _pending.RemoveAt(i);
                    continue;
                }

                _transform.SetMapCoordinates(pending.Item, pending.Holding);
                continue;
            }

            if (now < pending.LandAt)
                continue;

            _pending.RemoveAt(i);
            if (TerminatingOrDeleted(pending.Item))
                continue;

            Land(pending, now);
        }

        for (var i = _dropping.Count - 1; i >= 0; i--)
        {
            var (uid, until) = _dropping[i];
            if (now < until)
                continue;

            _dropping.RemoveAt(i);
            if (!TerminatingOrDeleted(uid))
                RemComp<LavalandSkyDropComponent>(uid);
        }
    }

    private void Land(PendingDrop pending, TimeSpan now)
    {
        var spot = pending.Destination.Offset(_random.NextVector2(pending.Spread));
        _transform.SetMapCoordinates(pending.Item, spot);

        // Nobody down there: it simply lies on the floor.
        if (!AnyoneWatching(spot))
            return;

        var drop = EnsureComp<LavalandSkyDropComponent>(pending.Item);
        drop.Start = now;
        drop.Duration = DropDuration;
        drop.Height = DropHeight;
        drop.Rising = false;
        drop.Sinking = false;
        Dirty(pending.Item, drop);
        _dropping.Add((pending.Item, now + TimeSpan.FromSeconds(DropDuration + 0.2f)));
    }

    private bool AnyoneWatching(MapCoordinates spot)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob || HasComp<GhostComponent>(mob) ||
                !HasComp<MobStateComponent>(mob) || !_mobState.IsAlive(mob))
            {
                continue;
            }

            var coords = _transform.GetMapCoordinates(mob);
            if (coords.MapId == spot.MapId && Vector2.Distance(coords.Position, spot.Position) <= WatchRange)
                return true;
        }

        return false;
    }
}
