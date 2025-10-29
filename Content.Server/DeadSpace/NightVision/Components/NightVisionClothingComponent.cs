
namespace Content.Server.DeadSpace.Components.NightVision;

[RegisterComponent]
public sealed partial class NightVisionClothingComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Color Color = Color.LimeGreen;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool HasNightVision = false;
}
