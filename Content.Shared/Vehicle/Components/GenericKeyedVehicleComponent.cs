/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Vehicle.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

/// <summary>
/// This is used for a vehicle which can only be operated when a specific key matching a whitelist is inserted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // DS14 - AutoGenerateComponentState
[Access(typeof(VehicleSystem))]
public sealed partial class GenericKeyedVehicleComponent : Component
{
    /// <summary>
    /// The ID corresponding to the container where the "key" must be inserted.
    /// </summary>
    [DataField(required: true)]
    public string ContainerId;

    /// <summary>
    /// A whitelist determining what qualifies as a valid key for this vehicle.
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist KeyWhitelist = new();

    /// <summary>
    /// If true, prevents keys which do not pass the <see cref="KeyWhitelist"/> from being inserted into <see cref="ContainerId"/>
    /// </summary>
    [DataField]
    public bool PreventInvalidInsertion = true;
    // DS14-start
    /// <summary>
    /// The key that was first inserted into this vehicle.
    /// Once set, only this specific key will be accepted.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? BoundKey;

    [ViewVariables]
    public TimeSpan NextWrongKeyPopup = TimeSpan.Zero;
    // DS14-end
}
