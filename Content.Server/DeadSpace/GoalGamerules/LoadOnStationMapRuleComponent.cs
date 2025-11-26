using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.GoalGamerules;

[RegisterComponent, Access(typeof(LoadOnStationMapRuleSystem))]
public sealed partial class LoadOnStationMapRuleComponent : Component
{
    /// <summary>
    /// A grid to load on a new map.
    /// </summary>
    [DataField]
    public ResPath? GridPath;
}
