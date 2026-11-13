using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// This is used for an event that spawns an artifact
/// somewhere random on the station.
/// </summary>
[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm] // DS14
[RegisterComponent, Access(typeof(BluespaceArtifactRule))]
public sealed partial class BluespaceArtifactRuleComponent : Component
{
    [DataField("artifactSpawnerPrototype")]
    public string ArtifactSpawnerPrototype = "RandomArtifactSpawner";

    [DataField("artifactFlashPrototype")]
    public string ArtifactFlashPrototype = "EffectFlashBluespace";

    [DataField("possibleSightings")]
    public List<string> PossibleSighting = new()
    {
        "bluespace-artifact-sighting-1",
        "bluespace-artifact-sighting-2",
        "bluespace-artifact-sighting-3",
        "bluespace-artifact-sighting-4",
        "bluespace-artifact-sighting-5",
        "bluespace-artifact-sighting-6",
        "bluespace-artifact-sighting-7"
    };
}
