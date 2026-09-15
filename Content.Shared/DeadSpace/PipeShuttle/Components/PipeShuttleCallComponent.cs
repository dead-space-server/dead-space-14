// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.PipeShuttle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PipeShuttleCallComponent : Component
{
    [DataField]
    public EntityUid? Shuttle;
}
