// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed class LimbWearSystem : EntitySystem
{
    private static readonly (SlotFlags Slots, LimbZone[] Zones)[] Needs =
    {
        (SlotFlags.FEET | SlotFlags.SOCKS, new[] { LimbZone.FootLeft, LimbZone.FootRight }),
        (SlotFlags.GLOVES, new[] { LimbZone.HandLeft, LimbZone.HandRight }),
        (SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES | SlotFlags.EARS, new[] { LimbZone.Head }),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, IsEquippingTargetAttemptEvent>(OnEquip);
    }

    private void OnEquip(Entity<LimbBodyComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (!LimbTuning.Enabled || !Missing(ent.Comp, args.SlotFlags))
            return;

        args.Reason = "limb-wear-missing";
        args.Cancel();
    }

    public static bool Missing(LimbBodyComponent limbs, SlotFlags slots)
    {
        foreach (var (needSlots, zones) in Needs)
        {
            if ((slots & needSlots) == 0)
                continue;

            foreach (var zone in zones)
            {
                var index = LimbWoundRules.Index(zone);
                if ((uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Severed)
                    return true;
            }
        }

        return false;
    }
}
