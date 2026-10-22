// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Item;

namespace Content.Shared.DeadSpace.Lavaland;

public sealed class LavalandSoulscytheBoundSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandSoulscytheBoundComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(Entity<LavalandSoulscytheBoundComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.Master is { } master && args.User != master)
            args.Cancel();
    }
}
