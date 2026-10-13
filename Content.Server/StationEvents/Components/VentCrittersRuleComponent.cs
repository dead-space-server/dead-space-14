using Content.Server.StationEvents.Events;
using Content.Shared.Storage;

namespace Content.Server.StationEvents.Components;

[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm] // DS14
[RegisterComponent, Access(typeof(VentCrittersRule))]
public sealed partial class VentCrittersRuleComponent : Component
{
    [DataField("entries")]
    public List<EntitySpawnEntry> Entries = new();

    /// <summary>
    /// At least one special entry is guaranteed to spawn
    /// </summary>
    [DataField("specialEntries")]
    public List<EntitySpawnEntry> SpecialEntries = new();
}
