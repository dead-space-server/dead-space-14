using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class FTLDiskSpawnerComponent : Component
{
    [DataField]
    public EntProtoId DiskPrototype = "CoordinatesDisk";

    [DataField]
    public EntProtoId CasePrototype = "DiskCase";
}