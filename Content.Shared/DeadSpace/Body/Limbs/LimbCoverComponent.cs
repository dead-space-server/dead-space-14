// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Clothing.Components;
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

    /// <summary>
    /// Raised shield only. Back and sides stay open.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool OnlyWhenBlocking;

    [DataField, AutoNetworkedField]
    public float WoundMultiplier = 1f;

    /// <summary>
    /// Plate stops a clean hole and dumps blunt trauma behind it.
    /// </summary>
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

        if ((worn.Slots & SlotFlags.OUTERCLOTHING) != 0)
            return zone is LimbZone.Torso or LimbZone.Groin or LimbZone.ArmLeft or LimbZone.ArmRight or LimbZone.LegLeft or LimbZone.LegRight;

        return false;
    }
}
