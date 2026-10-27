// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public partial class SharedGunSystem
{
    /// <summary>
    /// The gun's fire mode takes its ammo from a slot (<see cref="GunAmmoModesComponent"/>): its own self-recharging
    /// ammo neither fires nor shows in the counter meanwhile.
    /// </summary>
    protected bool UsesAmmoSlot(EntityUid gun)
    {
        return TryComp<GunAmmoModesComponent>(gun, out var modes) && modes.SlotSelected;
    }

    /// <summary>The gun's ammo counter is brought up to date (its ammo comes from somewhere else now).</summary>
    public void RefreshAmmoCount(EntityUid gun)
    {
        UpdateAmmoCount(gun);
    }
}
