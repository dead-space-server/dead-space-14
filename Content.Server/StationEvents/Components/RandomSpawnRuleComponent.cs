using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// Spawns a single entity at a random tile on a station using TryGetRandomTile.
/// </summary>
[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm] // DS14
[RegisterComponent, Access(typeof(RandomSpawnRule))]
public sealed partial class RandomSpawnRuleComponent : Component
{
    /// <summary>
    /// The entity to be spawned.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;
}
