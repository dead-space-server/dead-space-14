// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Components;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgerySlimeDissolveSystem : EntitySystem
{
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SurgerySlimeDissolveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var dissolve, out var xform))
        {
            dissolve.Left -= frameTime;
            if (dissolve.Left > 0f)
                continue;

            var solution = new Solution();
            solution.AddReagent("SlimeGel", FixedPoint2.New(dissolve.Amount * 0.5f));
            solution.AddReagent("Slime", FixedPoint2.New(dissolve.Amount * 0.5f));
            _puddle.TrySpillAt(xform.Coordinates, solution, out _);
            QueueDel(uid);
        }
    }
}
