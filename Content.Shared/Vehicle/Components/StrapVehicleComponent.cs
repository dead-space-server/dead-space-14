/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Vehicle.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Vehicle.Components;

/// <summary>
/// A <see cref="VehicleComponent"/> whose operator must be buckled to it.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(VehicleSystem))]
public sealed partial class StrapVehicleComponent : Component;
