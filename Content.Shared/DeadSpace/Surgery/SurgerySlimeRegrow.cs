// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgerySlimeRegrowComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionSurgerySlimeRegrow";

    [DataField]
    public EntityUid? ActionEntity;
}

public sealed partial class SurgerySlimeRegrowEvent : InstantActionEvent;

[RegisterComponent]
public sealed partial class SurgerySlimeDissolveComponent : Component
{
    [DataField]
    public float Left = 20f;

    [DataField]
    public float Amount = 15f;
}
