using System.Numerics;

namespace Content.Server.Revenant.Components;

[RegisterComponent]
public sealed partial class RevenantForcedSleepComponent : Component
{

    [ViewVariables(VVAccess.ReadOnly)]
    public float Accumulator = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SleepDelay = 30f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float StageDelay = 10f;

    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 DurationOfSleep = new Vector2(5, 10);
}
