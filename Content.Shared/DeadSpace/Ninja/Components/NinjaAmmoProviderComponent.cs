using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaAmmoProviderComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Proto = default!;

    [DataField(required: true)]
    public float EnergyPerShoot;
}