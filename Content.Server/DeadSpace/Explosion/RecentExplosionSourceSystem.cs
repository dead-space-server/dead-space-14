using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Explosion;

public sealed class RecentExplosionSourceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RecentExplosionSourceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.ExpiresAt)
                RemCompDeferred<RecentExplosionSourceComponent>(uid);
        }
    }

    public void SetSource(EntityUid target, EntityUid source)
    {
        var comp = EnsureComp<RecentExplosionSourceComponent>(target);
        comp.Source = source;
        comp.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(comp.Lifetime);
    }
}
