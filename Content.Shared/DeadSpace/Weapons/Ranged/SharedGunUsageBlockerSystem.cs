// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.DeadSpace.Weapons.Ranged;

public sealed partial class SharedGunUsageBlockerSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunUsageBlockerComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<GunUsageBlockerComponent> ent, ref ShotAttemptedEvent args)
    {
        _popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }
}
