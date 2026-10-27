// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void InitializeAmmoModes()
    {
        SubscribeLocalEvent<GunAmmoModesComponent, UpdateAmmoCounterEvent>(OnAmmoModesAmmoCount);
    }

    /// <summary>A fire mode loaded from a slot shows that slot's ammo (the gun's own provider stays quiet then).</summary>
    private void OnAmmoModesAmmoCount(Entity<GunAmmoModesComponent> ent, ref UpdateAmmoCounterEvent args)
    {
        if (!ent.Comp.SlotSelected)
            return;

        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(ent, ref ev);

        switch (args.Control)
        {
            case DefaultStatusControl control:
                control.Update(ev.Count, ev.Capacity);
                break;
            // A rifle with a bolt: the round loaded into the slot shows as the chambered one, no magazine.
            case ChamberMagazineStatusControl chamber:
                chamber.Update(ev.Count > 0, false, 0, 0);
                break;
        }
    }
}
