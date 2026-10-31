// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.Clothing;

[RegisterComponent, NetworkedComponent]
public sealed partial class HolsterDrawComponent : Component
{
    [DataField("actionProto")]
    public EntProtoId ActionProto = "ActionHolsterDrawBlueShield";
    [DataField("weaponProto", required: true)]
    public EntProtoId WeaponProto;

    [DataField]
    public EntityUid? ActionEntity;
}

public sealed partial class HolsterDrawActionEvent : InstantActionEvent
{
}
