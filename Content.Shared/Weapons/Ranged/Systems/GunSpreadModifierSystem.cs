using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed class GunSpreadModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunSpreadModifierComponent, GunGetAmmoSpreadEvent>(OnGunGetAmmoSpread);
        SubscribeLocalEvent<GunSpreadModifierComponent, ExaminedEvent>(OnExamine);
    }

    private void OnGunGetAmmoSpread(Entity<GunSpreadModifierComponent> ent, ref GunGetAmmoSpreadEvent args)
    {
        // DS14-start
        args.Spread *= GetSpread(ent);
        // DS14-end
    }

    private void OnExamine(Entity<GunSpreadModifierComponent> ent, ref ExaminedEvent args)
    {
        // DS14-start
        var percentage = Math.Round(GetSpread(ent) * 100);
        // DS14-end
        var loc = percentage < 100 ? "examine-gun-spread-modifier-reduction" : "examine-gun-spread-modifier-increase";
        percentage = percentage < 100 ? 100 - percentage : percentage - 100;
        var msg = Loc.GetString(loc, ("percentage", percentage));
        args.PushMarkup(msg);
    }

    // DS14-start
    private float GetSpread(Entity<GunSpreadModifierComponent> ent)
    {
        if (ent.Comp.WieldedSpread is { } wieldedSpread &&
            TryComp<WieldableComponent>(ent.Owner, out var wieldable) &&
            wieldable.Wielded)
        {
            return wieldedSpread;
        }

        return ent.Comp.Spread;
    }
    // DS14-end
}
