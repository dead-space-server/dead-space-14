using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Components;

namespace Content.Server.DeadSpace.Thief.Objectives.Components;

/// <summary>
/// DS14: Objective condition tracking how many dirty credits the thief has earned
/// through ВорПРО requests this round.
/// </summary>
[RegisterComponent]
public sealed partial class ThiefEarnConditionComponent : Component
{
    /// <summary>
    /// The amount of dirty credits required to complete the objective.
    /// </summary>
    [DataField]
    public int Target = 1000000;
}
