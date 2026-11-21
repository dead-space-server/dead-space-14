using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// Used an event that spawns an anomaly somewhere random on the map.
/// </summary>
[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm] // DS14
[RegisterComponent, Access(typeof(AnomalySpawnRule))]
public sealed partial class AnomalySpawnRuleComponent : Component
{
    [DataField("anomalySpawnerPrototype")]
    public string AnomalySpawnerPrototype = "RandomAnomalySpawner";
}
