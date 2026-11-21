using Content.Shared.Actions;

namespace Content.Shared.Decals;

public sealed partial class PlaceDecalActionEvent : WorldTargetActionEvent
{
    [DataField("decalId", required:true)]
    public string DecalId = string.Empty;

    [DataField("color")]
    public Color Color;

    [DataField("rotation")]
    public double Rotation;

    [DataField("snap")]
    public bool Snap;

    [DataField("zIndex")]
    public int ZIndex;

    [DataField("cleanable")]
    public bool Cleanable;
}
