using Content.Shared.Vehicle; // DS14 - VehicleSystem remains in the current engine-compatible namespace.
using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

/// <summary>
/// Occupies the operator's hands while they are operating a vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(VehicleSystem))]
public sealed partial class VehicleHandBlockerComponent : Component
{
    /// <summary>
    /// The number of hands to occupy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BlockedHands = 1;
}
