// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbCoverComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<LimbZone> Zones = new();

    [DataField, AutoNetworkedField]
    public bool Face;

    [DataField, AutoNetworkedField]
    public bool Ricochet;

    [DataField, AutoNetworkedField]
    public bool Grip;

    [DataField, AutoNetworkedField]
    public bool StopsFire;

    [DataField, AutoNetworkedField]
    public bool Blast;

    [DataField, AutoNetworkedField]
    public bool Sever;

    [DataField, AutoNetworkedField]
    public bool OnlyWhenBlocking;

    [DataField, AutoNetworkedField]
    public float WoundMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public bool BluntBackface;

    [DataField, AutoNetworkedField]
    public float Slip;

    public bool Covers(LimbZone zone)
    {
        if (OnlyWhenBlocking)
            return false;

        if (Zones.Contains(zone))
            return true;

        return Face && zone is LimbZone.Eyes or LimbZone.Mouth or LimbZone.Head;
    }
}

public static class LimbCoverRules
{
    public static bool Covers(EntityUid clothing, LimbZone zone, IEntityManager entities)
    {
        if (entities.TryGetComponent(clothing, out LimbCoverComponent? cover))
            return cover.Covers(zone);

        if (!entities.TryGetComponent(clothing, out ClothingComponent? worn))
            return false;

        if ((worn.Slots & SlotFlags.HEAD) != 0)
            return zone is LimbZone.Head or LimbZone.Eyes or LimbZone.Mouth;

        if ((worn.Slots & SlotFlags.GLOVES) != 0)
            return zone is LimbZone.HandLeft or LimbZone.HandRight;

        if ((worn.Slots & SlotFlags.FEET) != 0)
            return zone is LimbZone.FootLeft or LimbZone.FootRight;

        if ((worn.Slots & SlotFlags.MASK) != 0)
            return zone is LimbZone.Head or LimbZone.Eyes or LimbZone.Mouth;

        if ((worn.Slots & (SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING)) != 0)
            return zone is LimbZone.Torso or LimbZone.Groin or LimbZone.ArmLeft or LimbZone.ArmRight or LimbZone.LegLeft or LimbZone.LegRight;

        return false;
    }

    public static bool Worn(EntityUid body, LimbZone zone, IEntityManager entities, InventorySystem inventory)
    {
        var slots = inventory.GetSlotEnumerator(body);
        while (slots.NextItem(out var item, out var slot))
        {
            if (entities.HasComponent<BreathToolComponent>(item) || entities.HasComponent<MedicalGagComponent>(item))
            {
                if (zone == LimbZone.Mouth)
                    return true;

                continue;
            }

            if (entities.HasComponent<SurgeryBaldComponent>(item))
                continue;

            if (entities.TryGetComponent(item, out LimbCoverComponent? cover))
            {
                if (!cover.OnlyWhenBlocking && cover.Covers(zone))
                    return true;

                continue;
            }

            if (SlotCovers(slot.SlotFlags, zone))
                return true;
        }

        return false;
    }

    public static bool SlotCovers(SlotFlags flags, LimbZone zone)
    {
        if ((flags & SlotFlags.HEAD) != 0
            && zone is LimbZone.Head or LimbZone.Eyes or LimbZone.Mouth)
            return true;

        if ((flags & SlotFlags.MASK) != 0 && zone == LimbZone.Mouth)
            return true;

        if ((flags & SlotFlags.EYES) != 0 && zone == LimbZone.Eyes)
            return true;

        if ((flags & SlotFlags.GLOVES) != 0 && zone is LimbZone.HandLeft or LimbZone.HandRight)
            return true;

        if ((flags & (SlotFlags.FEET | SlotFlags.SOCKS)) != 0 && zone is LimbZone.FootLeft or LimbZone.FootRight)
            return true;

        if ((flags & (SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING)) != 0
            && zone is LimbZone.Torso or LimbZone.Groin or LimbZone.ArmLeft or LimbZone.ArmRight or LimbZone.LegLeft or LimbZone.LegRight)
            return true;

        if ((flags & SlotFlags.LEGS) != 0 && zone is LimbZone.Groin or LimbZone.LegLeft or LimbZone.LegRight)
            return true;

        if ((flags & SlotFlags.UNDERWEART) != 0 && zone == LimbZone.Torso)
            return true;

        if ((flags & SlotFlags.UNDERWEARB) != 0 && zone == LimbZone.Groin)
            return true;

        return false;
    }
}
