// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneAuraComponent : Component
{
    [AutoNetworkedField]
    public List<string> Charms = new();

    [AutoNetworkedField]
    public int Shields;
}
