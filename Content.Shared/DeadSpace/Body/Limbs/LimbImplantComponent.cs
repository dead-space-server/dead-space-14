// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Body.Limbs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbImplantComponent : Component
{
    [DataField(required: true)]
    public List<string> Slots = new();

    [DataField]
    public int Size = 1;

    [DataField]
    public EntProtoId? ImplantAction;

    [DataField]
    public ComponentRegistry ImplantComponents = new();

    [DataField]
    public ComponentRegistry ToggleComponents = new();

    [DataField, AutoNetworkedField]
    public bool Toggled;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Host;

    [DataField, AutoNetworkedField]
    public EntityUid? Action;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class LimbImplantsComponent : Component;
