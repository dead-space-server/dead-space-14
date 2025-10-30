
namespace Content.Server.DeadSpace.Components.NightVision;

[RegisterComponent]
public sealed partial class PNVComponent : Component
{
    [DataField]
    public Color Color = new Color(80f / 255f, 220f / 255f, 70f / 255f, 0.2f);

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public bool HasNightVision = false;
}
