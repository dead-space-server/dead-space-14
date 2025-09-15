using Content.Shared.DeviceLinking;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeadSpace.Items.Devices;

[RegisterComponent]
public sealed partial class EmergencySignallerComponent : Component
{
    [DataField]
    public float Charge = 120f;

    [DataField]
    public LocId Message = "emergency-signaller-message";
}
