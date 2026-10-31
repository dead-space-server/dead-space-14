// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.OrganDamage;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Microscope;

[Serializable, NetSerializable]
public sealed class MicroscopeBoundUserInterfaceState : BoundUserInterfaceState
{
    public NetEntity? Organ;

    public string OrganName = string.Empty;

    public FixedPoint2 Damage;

    public FixedPoint2 MaxDamage;

    public bool Dead;

    public List<OrganWound> Wounds = new();

    public MicroscopeBoundUserInterfaceState(
        NetEntity? organ,
        string organName,
        FixedPoint2 damage,
        FixedPoint2 maxDamage,
        bool dead,
        List<OrganWound> wounds)
    {
        Organ = organ;
        OrganName = organName;
        Damage = damage;
        MaxDamage = maxDamage;
        Dead = dead;
        Wounds = wounds;
    }
}
