// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.PipeShuttle.Components;

/// <summary>
/// A button that toggles a pipe shuttle's flight mode between Manual and Automatic.
/// Must be placed on the same grid as the <see cref="PipeShuttleComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PipeShuttleModeSwitchComponent : Component
{
    [DataField]
    public EntityUid? Shuttle;
}
