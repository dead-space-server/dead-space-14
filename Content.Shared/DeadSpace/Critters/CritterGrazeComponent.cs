using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Critters;

[RegisterComponent, NetworkedComponent]
public sealed partial class CritterGrazeComponent : Component
{
    [DataField]
    public TimeSpan MinCooldown = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan MaxCooldown = TimeSpan.FromSeconds(30);

    public TimeSpan NextGraze;

    [DataField]
    public float SleepRange = 12f;
}
