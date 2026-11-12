using Content.Shared.DeviceNetwork;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Medical.CrewMonitoring;

namespace Content.Server.Medical.CrewMonitoring;

<<<<<<< ours
[AutoGenerateComponentPause] // DS14
||||||| base
=======
/// <summary>
/// Stores suit sensor status received by a crew monitoring console.
/// </summary>
>>>>>>> theirs
[RegisterComponent]
[Access(typeof(CrewMonitoringConsoleSystem))]
public sealed partial class CrewMonitoringConsoleComponent : Component
{
    /// <summary>
    /// List of all currently connected sensors to this console.
    /// </summary>
    public Dictionary<DeviceAddress, SuitSensorStatus> ConnectedSensors = new();

    /// <summary>
    /// How long without an update before a sensor is considered lost, in seconds.
    /// </summary>
    [DataField]
    public float SensorTimeout = 10f;

    // DS14-start
    /// <summary>
    /// Read suit sensors directly for an administrator's personal monitor, without a monitoring server.
    /// </summary>
    [DataField]
    public bool AdminMonitor;

    [DataField]
    [AutoPausedField]
    public TimeSpan NextSound = TimeSpan.Zero;

    [DataField]
    public TimeSpan SoundInterval = TimeSpan.FromSeconds(10);

    public CrewMonitoringConsolePingMode CurrentPingMode = CrewMonitoringConsolePingMode.Severe;
    // DS14-end
}
