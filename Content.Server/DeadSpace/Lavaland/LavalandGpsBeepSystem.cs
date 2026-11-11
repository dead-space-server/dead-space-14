// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.GPS.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// A GPS on the lavaland beeps when a miniboss (the mad miner, an elite) or an elite tumor is near - faster the closer.
/// </summary>
public sealed class LavalandGpsBeepSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float Range = 14f;
    private static readonly TimeSpan IntervalFar = TimeSpan.FromSeconds(1.6);
    private static readonly TimeSpan IntervalNear = TimeSpan.FromSeconds(0.45);
    private static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(0.5);
    private static readonly SoundSpecifier Beep = new SoundPathSpecifier("/Audio/Effects/double_beep.ogg");

    /// <summary>When each GPS checks (and maybe beeps) next.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _next = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HandheldGPSComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_next.TryGetValue(uid, out var next) && now < next)
                continue;

            _next[uid] = now + IdleCheck;
            if (xform.MapUid is not { } map || !HasComp<LavalandMapComponent>(map))
                continue;

            var nearest = NearestMiniBoss(xform);
            if (nearest > Range)
                continue;

            var closeness = 1f - Math.Clamp(nearest / Range, 0f, 1f);
            _next[uid] = now + IntervalFar + (IntervalNear - IntervalFar) * closeness;
            _audio.PlayPvs(Beep, uid, AudioParams.Default.WithVolume(-2f).WithMaxDistance(10f));
        }

        // Deleted GPSes.
        if (_next.Count > 0 && _timing.CurTick.Value % 300 == 0)
        {
            foreach (var uid in new List<EntityUid>(_next.Keys))
            {
                if (TerminatingOrDeleted(uid))
                    _next.Remove(uid);
            }
        }
    }

    /// <summary>Distance to the nearest living miniboss or elite tumor; MaxValue if none is in range.</summary>
    private float NearestMiniBoss(TransformComponent xform)
    {
        var position = _transform.GetWorldPosition(xform);
        var nearest = float.MaxValue;
        foreach (var candidate in _lookup.GetEntitiesInRange(xform.MapID, position, Range))
        {
            var miniBoss = HasComp<LavalandEliteTumorComponent>(candidate) ||
                           (HasComp<LavalandMadMinerComponent>(candidate) || HasComp<LavalandEliteComponent>(candidate)) &&
                           TryComp<MobStateComponent>(candidate, out var mob) && mob.CurrentState == MobState.Alive;
            if (miniBoss)
                nearest = MathF.Min(nearest, Vector2.Distance(position, _transform.GetWorldPosition(candidate)));
        }

        return nearest;
    }
}
