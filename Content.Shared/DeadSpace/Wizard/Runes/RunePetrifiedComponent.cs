// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent]
public sealed partial class RunePetrifiedComponent : Component
{
}

[RegisterComponent, NetworkedComponent]
public sealed partial class RuneTimeHeldComponent : Component
{
    public readonly List<(LimbStrike Strike, bool Ready, EntityUid? Cause)> Strikes = new();
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
