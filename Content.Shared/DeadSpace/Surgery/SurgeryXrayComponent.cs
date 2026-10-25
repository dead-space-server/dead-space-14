// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryXrayComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool On;

    [DataField]
    public List<string> Boneless = new();

    [DataField]
    public string Sprite = "/Textures/_DeadSpace/Mobs/Surgery/skeleton.rsi";

    [DataField]
    public string State = "skeleton";
}
