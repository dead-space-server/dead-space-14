using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Demons.DemonShadow.Components;

[RegisterComponent]
public sealed partial class DemonShadowFlareDamageComponent : Component
{
    [DataField]
    public float LightDamageMultiplier = 0.5f;
}
