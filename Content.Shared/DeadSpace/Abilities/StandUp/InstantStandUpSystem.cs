// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Stunnable;

namespace Content.Shared.DeadSpace.Abilities.StandUp;

public sealed partial class InstantStandUpSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InstantStandUpComponent, GetStandUpTimeEvent>(OnGetStandUpTime);
    }

    private void OnGetStandUpTime(Entity<InstantStandUpComponent> ent, ref GetStandUpTimeEvent args)
    {
        args.DoAfterTime = TimeSpan.Zero;
    }
}
