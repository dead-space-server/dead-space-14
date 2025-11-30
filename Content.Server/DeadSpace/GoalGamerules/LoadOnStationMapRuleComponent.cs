// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

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
    /// <summary>
    /// Radius out of the station.
    /// </summary>
    [DataField]
    public float Radius;
}
