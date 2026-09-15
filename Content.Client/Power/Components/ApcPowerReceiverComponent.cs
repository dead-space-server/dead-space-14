using Content.Shared.Power.Components;

namespace Content.Client.Power.Components;

/// <inheritdoc />
[RegisterComponent]
public sealed partial class ApcPowerReceiverComponent : SharedApcPowerReceiverComponent
{
    // DS14-start
    public override float Load { get; set; }
    // DS14-end
}
