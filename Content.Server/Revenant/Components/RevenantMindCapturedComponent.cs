namespace Content.Server.Revenant.Components;

[RegisterComponent]
public sealed partial class RevenantMindCapturedComponent : Component
{

    [ViewVariables(VVAccess.ReadOnly)]
    public float Accumulator = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DurationOfCapture = 300f;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid RevenantUid = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid TargetUid = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public string ReturnTTSPrototype = default!;
}
