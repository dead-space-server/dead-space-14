// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent]
public sealed partial class RunePetrifiedComponent : Component
{
}

[RegisterComponent]
public sealed partial class RuneTimeHeldComponent : Component
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneScreenFlashComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier? Sprite;

    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan End;
}
