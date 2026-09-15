/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Damage;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicle.Components;

/// <summary>
/// Vehicles are objects that have the behavior of moving when a player "operates" them.
/// The details of when the vehicle can operate and who the operator is are not defined here.
/// This simply contains the baseline behavior of the vehicle itself.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(VehicleSystem))]
public sealed partial class VehicleComponent : Component
{
    /// <summary>
    /// The driver of this vehicle.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Operator;

    /// <summary>
    /// Simple whitelist for determining who can operate this vehicle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? OperatorWhitelist;

    /// <summary>
    /// If true, damage to the vehicle will be transferred to the operator.
    /// This damage is modified by <see cref="TransferDamageModifier"/>
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TransferDamage = true;

    /// <summary>
    /// A damage modifier set that adjusts the damage passed from the vehicle to the operator.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageModifierSet? TransferDamageModifier;

    /// <summary>
    /// Whether the operator requires hands to operate this vehicle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequiresHands = true;

    /// <summary>
    /// Whether the operator can attack while operating this vehicle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanAttack;

    // DS14-start
    /// <summary>
    /// If true, the vehicle requires an operator to run.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequiresOperator = true;

    /// <summary>
    /// If true, only the current operator can manually eject keys from this vehicle.
    /// System removals, such as destruction, are still allowed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool KeyEjectRequiresOperator;

    /// <summary>
    /// If true, other users cannot unstrap the operator unless the operator is incapacitated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProtectOperatorUnstrap;

    /// <summary>
    /// Cooldown for the "no key" popup to prevent spam.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextNoKeyPopup = TimeSpan.Zero;

    /// <summary>
    /// Sound played periodically while the vehicle is moving.
    /// Analogous to FootstepModifierComponent but for vehicles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? MovementSound;

    /// <summary>
    /// Distance the vehicle must travel before the movement sound plays again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MovementSoundDistance = 2f;

    /// <summary>
    /// Internal: accumulated movement distance since the last sound.
    /// Not networked: tracked per-side for client-side prediction.
    /// </summary>
    [ViewVariables]
    public float MovementSoundAccumulatedDistance;

    /// <summary>
    /// Internal: last vehicle position used for distance calculation.
    /// Reset when operator changes to prevent false distance jumps.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? MovementSoundLastPosition;

    [DataField]
    public List<EntProtoId> BreakOnCollideWith = new();
    // DS14-end
}

[Serializable, NetSerializable]
public enum VehicleVisuals : byte
{
    HasOperator,    // The vehicle has a valid operator
    CanRun          // The vehicle can be moved by the operator (turned on :flushed:)
}

/// <summary>
/// Event raised on operator when they begin to operate a vehicle
/// Values are configured before this event is raised.
/// </summary>
[ByRefEvent, UsedImplicitly]
public readonly record struct OnVehicleEnteredEvent(Entity<VehicleComponent> Vehicle, EntityUid Operator);

/// <summary>
/// Event raised on operator when they stop operating a vehicle.
/// Values are configured after this event is raised.
/// </summary>
[ByRefEvent, UsedImplicitly]
public readonly record struct OnVehicleExitedEvent(Entity<VehicleComponent> Vehicle, EntityUid Operator);

/// <summary>
/// Event raised on the vehicle after an operator is set.
/// New operator can be null.
/// </summary>
[ByRefEvent, UsedImplicitly]
public readonly record struct VehicleOperatorSetEvent(EntityUid? NewOperator, EntityUid? OldOperator);

/// <summary>
/// Event raised on a vehicle to check if it can run/move around.
/// </summary>
[ByRefEvent, UsedImplicitly]
public readonly record struct VehicleCanRunEvent(Entity<VehicleComponent> Vehicle, bool CanRun = true);
