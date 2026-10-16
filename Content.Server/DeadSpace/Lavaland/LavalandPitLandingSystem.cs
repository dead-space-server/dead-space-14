// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Landing after a fall (Celestial maze, chromite caves) and climbing out of a pit: the client plays the doll
/// animation, the server keeps the player still and unhurt until it ends.
/// </summary>
public sealed class LavalandPitLandingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<EntityUid> _ended = new();

    /// <summary>Starts the landing. <paramref name="from"/> is set only for climbing out of a pit.</summary>
    public void StartLanding(EntityUid uid, TimeSpan duration, Vector2? from = null)
    {
        var landing = EnsureComp<LavalandPitLandingComponent>(uid);
        landing.Style = _random.Pick(Enum.GetValues<LavalandLandingStyle>());
        landing.Duration = (float) Math.Max(0.3, duration.TotalSeconds);
        landing.ClimbOut = from != null;
        landing.From = from ?? Vector2.Zero;
        landing.EndTime = _timing.CurTime + duration;
        Dirty(uid, landing);

        // Nothing hurts the doll while it lies in the flowers.
        EnsureComp<LavalandPitFrozenComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _ended.Clear();
        var query = EntityQueryEnumerator<LavalandPitLandingComponent>();
        while (query.MoveNext(out var uid, out var landing))
        {
            if (now >= landing.EndTime)
                _ended.Add(uid);
        }

        foreach (var uid in _ended)
        {
            RemComp<LavalandPitLandingComponent>(uid);
            // A Celestial sleep or another descent may still need the freeze.
            if (!HasComp<LavalandPitJumpingComponent>(uid) && !HasComp<LavalandPitFallingComponent>(uid) &&
                !HasComp<LavalandCaveClimbComponent>(uid))
            {
                RemComp<LavalandPitFrozenComponent>(uid);
            }
        }
    }
}
