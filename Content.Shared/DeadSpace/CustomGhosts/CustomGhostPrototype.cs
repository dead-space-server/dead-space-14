using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.CustomGhosts;

[Prototype]
public sealed partial class CustomGhostPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Login = default!;

    [DataField]
    public ResPath SpritePath;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public float Alpha = -1;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;
}

[Serializable, NetSerializable]
public enum CustomGhostAppearance
{
    Sprite,
    ColorOverride
}
