using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CrewMonitoring;

public sealed partial class CrewMonitoringServerSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private SingletonDeviceNetServerSystem _singletonServerSystem = default!;

    private const float UpdateRate = 3f;
    private float _updateDiff;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitoringServerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CrewMonitoringServerComponent, DeviceNetServerDisconnectedEvent>(OnDisconnected);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // check update rate
        _updateDiff += frameTime;
        if (_updateDiff < UpdateRate)
            return;
        _updateDiff -= UpdateRate;

        var servers = EntityQueryEnumerator<CrewMonitoringServerComponent>();

        while (servers.MoveNext(out var id, out var server))
        {
            if (!_singletonServerSystem.IsActiveServer(id))
                continue;

            UpdateTimeout(id);
            BroadcastSensorStatus(id, server);
        }
    }

    /// <summary>
    /// Adds or updates a sensor status entry if the received package is a sensor status update
    /// </summary>
    [SubscribeLocalEvent]
    private void OnSensorStatus(Entity<CrewMonitoringServerComponent> ent, ref DeviceNetworkPacketEvent<SuitSensorStatusPayload> args)
    {
<<<<<<< ours
        var sensorStatus = _sensors.PacketToSuitSensor(args.Data);
        if (sensorStatus == null)
            return;

        // DS14-start
        if (NeedPing(sensorStatus,
                component.SensorStatus.GetValueOrDefault(args.SenderAddress),
                out var pingMode))
        {
            Send(uid, pingMode);
        }
        // DS14-end

        sensorStatus.Timestamp = _gameTiming.CurTime;
        component.SensorStatus[args.SenderAddress] = sensorStatus;
||||||| base
        var sensorStatus = _sensors.PacketToSuitSensor(args.Data);
        if (sensorStatus == null)
            return;

        sensorStatus.Timestamp = _gameTiming.CurTime;
        component.SensorStatus[args.SenderAddress] = sensorStatus;
=======
        var sensorData = args.Data.Data;
        sensorData.Timestamp = _gameTiming.CurTime;
        ent.Comp.SensorStatus[args.SenderAddress] = sensorData;
>>>>>>> theirs
    }

    // DS14-start
    internal static bool NeedPing(
        SuitSensorStatus newSensor,
        SuitSensorStatus? oldSensor,
        out CrewMonitoringConsolePingMode pingMode)
    {
        pingMode = default;

        if (newSensor is { Coordinates: null })
        {
            return false;
        }

        if (!newSensor.IsAlive)
        {
            if (oldSensor is { IsAlive: false })
                return false;

            pingMode = CrewMonitoringConsolePingMode.Dead;
            return true;
        }

        if (newSensor.DamagePercentage is not { } newDamage)
            return false;

        var oldDamage = oldSensor != null
            ? oldSensor.DamagePercentage
            : null;

        if (newDamage >= 1f && (oldDamage == null || oldDamage < 1f))
        {
            pingMode = CrewMonitoringConsolePingMode.Critical;
            return true;
        }

        if (newDamage >= 0.6f && (oldDamage == null || oldDamage < 0.6f))
        {
            pingMode = CrewMonitoringConsolePingMode.Severe;
            return true;
        }

        return false;
    }

    private void Send(EntityUid uid, CrewMonitoringConsolePingMode pingMode)
    {
        var payload = new NetworkPayload
        {
            ["PingMode"] = pingMode,
        };
        _deviceNetworkSystem.QueuePacket(uid, null, payload);
    }
    // DS14-end

    /// <summary>
    /// Clears the servers sensor status list
    /// </summary>
    private void OnRemove(EntityUid uid, CrewMonitoringServerComponent component, ComponentRemove args)
    {
        component.SensorStatus.Clear();
    }

    /// <summary>
    /// Drop the sensor status if it hasn't been updated for to long
    /// </summary>
    private void UpdateTimeout(EntityUid uid, CrewMonitoringServerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        foreach (var (address, sensor) in component.SensorStatus)
        {
            var dif = _gameTiming.CurTime - sensor.Timestamp;
            if (dif.Seconds > component.SensorTimeout)
                component.SensorStatus.Remove(address);
        }
    }

    /// <summary>
    /// Broadcasts the status of all connected sensors
    /// </summary>
    private void BroadcastSensorStatus(EntityUid uid, CrewMonitoringServerComponent? serverComponent = null, DeviceNetworkComponent? device = null)
    {
        if (!Resolve(uid, ref serverComponent, ref device))
            return;

        var payload = new BroadcastSuitSensorStatePayload
        {
            SensorStatus = serverComponent.SensorStatus,
        };

        _deviceNetworkSystem.SendPacket((uid, device), null, ref payload);
    }

    /// <summary>
    /// Clears sensor data on disconnect
    /// </summary>
    private void OnDisconnected(EntityUid uid, CrewMonitoringServerComponent component, ref DeviceNetServerDisconnectedEvent _)
    {
        component.SensorStatus.Clear();
    }
}
