// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.AghostColor;

[RegisterComponent,NetworkedComponent]
[AutoGenerateComponentState(true)]
public sealed partial class AghostColorComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}
