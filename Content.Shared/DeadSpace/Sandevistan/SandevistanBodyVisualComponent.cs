// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Sandevistan;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SandevistanBodyVisualComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResPath Sprite = new("/Textures/_DeadSpace/Mobs/Effects/sandevistan.rsi");

    [DataField, AutoNetworkedField]
    public string State = "body";
}
