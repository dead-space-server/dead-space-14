// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.OrganDamage;

[Serializable, NetSerializable]
public enum OrganDamageVisuals : byte
{
    DamagePercent,

    Dead,
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganDamageVisualComponent : Component
{
}
