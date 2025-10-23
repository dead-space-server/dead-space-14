// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Abilities.Bloodsucker.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class BloodIncubatorComponent : Component
{

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilUpdate = TimeSpan.Zero;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float UpdateDuration = 0f;

    #region Visualizer

    [DataField]
    public List<string> States { get; set; } = new List<string>();

    #endregion
}
