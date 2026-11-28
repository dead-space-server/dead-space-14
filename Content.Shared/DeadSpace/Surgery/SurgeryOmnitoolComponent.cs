// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class SurgeryOmnitoolComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Mode;

    [DataField]
    public List<SurgeryOmnitoolMode> Modes = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryHandBusyComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Hand;
}

[DataDefinition]
public sealed partial class SurgeryOmnitoolMode
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public string EquippedState = string.Empty;
}
