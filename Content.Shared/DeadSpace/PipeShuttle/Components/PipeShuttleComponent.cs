// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.PipeShuttle.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class PipeShuttleComponent : Component
{
    [DataField("destinations")]
    public List<PipeShuttleDestination> Destinations = new();

    [DataField("currentDestId"), AutoNetworkedField]
    public string? CurrentDestId;

    [DataField("targetDestId"), AutoNetworkedField]
    public string? TargetDestId;

    [DataField("travelling"), AutoNetworkedField]
    public bool Travelling;

    [DataField("moveSpeed")]
    public float MoveSpeed = 8f;

    [DataField("arrivalThreshold")]
    public float ArrivalThreshold = 0.5f;

    [DataField("cooldown")]
    public float Cooldown = 10f;

    [DataField("positionOffset")]
    public Vector2 PositionOffset;

    [DataField("flightMode"), AutoNetworkedField]
    public PipeShuttleFlightMode FlightMode = PipeShuttleFlightMode.Automatic;

    [ViewVariables]
    public bool DoorsSecured;

    /// <summary>
    /// Whether the shuttle body has been switched to dynamic for manual flight.
    /// Prevents re-applying every frame.
    /// </summary>
    [ViewVariables]
    public bool ManualBodyActive;
}

public enum PipeShuttleFlightMode : byte
{
    Automatic,
    Manual,
}

[Serializable, DataDefinition, NetSerializable]
public sealed partial class PipeShuttleDestination
{
    [DataField("id")]
    public string Id = string.Empty;

    [DataField("name")]
    public LocId Name = string.Empty;

    [DataField("position")]
    public Vector2 Position;
}
