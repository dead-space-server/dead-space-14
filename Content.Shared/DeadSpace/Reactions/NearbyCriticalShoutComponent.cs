// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Reactions;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NearbyCriticalShoutComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 10f;

    [DataField, AutoNetworkedField]
    public float Cooldown = 5f;

    [DataField, AutoNetworkedField]
    public bool OnlyWhileActive = true;

    [DataField, AutoNetworkedField]
    public List<LocId> Messages = new();

    public TimeSpan NextShout;
}
