// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Weapons.Smart;

[Serializable, NetSerializable]
public sealed class SmartWeaponTargetChangedEvent : EntityEventArgs
{
    public NetEntity Gun;
    public NetEntity? Target;

    public SmartWeaponTargetChangedEvent(NetEntity gun, NetEntity? target)
    {
        Gun = gun;
        Target = target;
    }
}