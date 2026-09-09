using Content.Shared.Medical.SuitSensor;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.CrewMonitoring;

[Serializable, NetSerializable]
public enum CrewMonitoringUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class CrewMonitoringState : BoundUserInterfaceState
{
    public List<SuitSensorStatus> Sensors;

    public CrewMonitoringConsolePingMode PingMode; // DS14

    public CrewMonitoringState(List<SuitSensorStatus> sensors, CrewMonitoringConsolePingMode pingMode) // DS14
    {
        Sensors = sensors;
        PingMode = pingMode; // DS14
    }
}

// DS14-start
[Serializable, NetSerializable]
public sealed class CrewMonitoringSetPingModeMessage : BoundUserInterfaceMessage
{
    public CrewMonitoringConsolePingMode Mode;

    public CrewMonitoringSetPingModeMessage(CrewMonitoringConsolePingMode mode)
    {
        Mode = mode;
    }
}

[Serializable, NetSerializable]
public enum CrewMonitoringConsolePingMode
{
    Severe,
    Critical,
    Dead,
    Disabled
}
// DS14-end
