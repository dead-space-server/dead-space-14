using Content.Shared.DeadSpace.Destructible;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Destructible;

public sealed class ExplosionKnockoutSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Сохраняет эпицентр взрыва для сущности, чтобы behavior мог его использовать.
    /// Вызывается из ExplosionSystem.ProcessEntity.
    /// </summary>
    public void SetEpicenter(EntityUid uid, MapCoordinates epicenter, ExplosionKnockoutComponent? comp = null)
    {
        comp ??= EnsureComp<ExplosionKnockoutComponent>(uid);
        comp.RecentEpicenter = epicenter;
        comp.EpicenterExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(2);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ExplosionKnockoutComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.RecentEpicenter != null && _timing.CurTime >= comp.EpicenterExpiresAt)
                comp.RecentEpicenter = null;
        }
    }
}
