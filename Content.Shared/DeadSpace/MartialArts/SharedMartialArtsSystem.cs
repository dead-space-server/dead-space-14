// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.MartialArts.SmokingCarp.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.DeadSpace.MartialArts.SmokingCarp;

public abstract partial class SharedMartialArtsSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SmokingCarpNotShotComponent, ShotAttemptedEvent>(OnShotAttempt);
    }

    private void OnShotAttempt(Entity<SmokingCarpNotShotComponent> ent, ref ShotAttemptedEvent args)
    {
        _popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }
}