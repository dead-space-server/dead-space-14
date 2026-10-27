// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Actions.Events;
using Content.Shared.Standing;

namespace Content.Shared.DeadSpace.Items;

public sealed class PreventForcedDropSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PreventForcedDropComponent, FellDownThrowAttemptEvent>(OnFellDownThrowAttempt);
        SubscribeLocalEvent<PreventForcedDropComponent, DisarmAttemptEvent>(OnDisarmAttempt);
    }

    private void OnFellDownThrowAttempt(Entity<PreventForcedDropComponent> ent, ref FellDownThrowAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnDisarmAttempt(Entity<PreventForcedDropComponent> ent, ref DisarmAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
