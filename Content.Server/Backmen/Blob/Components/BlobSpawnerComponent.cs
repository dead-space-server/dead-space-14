using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobSpawnerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite),
     DataField("corePrototype")]
    public string CoreBlobPrototype = "CoreBlobTile";
}
