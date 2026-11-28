using Robust.Shared.GameStates;

namespace Content.Server.Backmen.Blob.Components;

[RegisterComponent]
public sealed partial class BlobMineComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("core")]
    public EntityUid? Core;
}
