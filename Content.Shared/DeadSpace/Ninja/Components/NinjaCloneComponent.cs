using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaCloneComponent : Component
{
    [DataField]
    public EntProtoId CloneProto = "HolographicNinjaClone";

    [DataField]
    public float CloningChance = 0.5f;

    [DataField]
    public float MinSpawnRadius = 1f;

    [DataField]
    public float MaxSpawnRadius = 2f;
}