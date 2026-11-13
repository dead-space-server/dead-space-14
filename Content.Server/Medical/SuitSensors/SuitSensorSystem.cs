using Content.Server.DeviceNetwork.Systems;
using Content.Server.Medical.CrewMonitoring;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.Timing;

namespace Content.Server.Medical.SuitSensors;

public sealed partial class SuitSensorSystem : SharedSuitSensorSystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private SingletonDeviceNetServerSystem _singletonServerSystem = default!;
    [Dependency] private StationLimitedNetworkSystem _stationLimitedNetwork = default!; // DS14

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;
        var sensors = EntityQueryEnumerator<SuitSensorComponent, DeviceNetworkComponent>();

        while (sensors.MoveNext(out var uid, out var sensor, out var device))
        {
            if (device.TransmitFrequency is null)
                continue;

            // check if sensor is ready to update
            if (curTime < sensor.NextUpdate)
                continue;
            sensor.NextUpdate += sensor.UpdateRate;

            // DS14-start
            var stationId = UpdateSensorStation(uid, sensor);
            if (stationId == null)
                continue;
            // DS14-end

            // get sensor status
            var status = GetSensorState((uid, sensor));
            if (status == null)
                continue;

            //Retrieve active server address if the sensor isn't connected to a server
            if (sensor.ConnectedServer == null)
            {
                if (!_singletonServerSystem.TryGetActiveServerAddress<CrewMonitoringServerComponent>(stationId.Value, out var address, device.TransmitFrequency)) // DS14
                    continue;

                sensor.ConnectedServer = address;
            }

            // Send it to the connected server
            var payload = SuitSensorToPacket(status);

            // Clear the connected server if its address isn't on the network
            if (!_deviceNetworkSystem.IsAddressPresent(device.DeviceNetId, sensor.ConnectedServer))
            {
                sensor.ConnectedServer = null;
                continue;
            }

            _deviceNetworkSystem.QueuePacket(uid, sensor.ConnectedServer, payload, device: device);
        }
    }

    // DS14-start
    private EntityUid? UpdateSensorStation(EntityUid uid, SuitSensorComponent sensor)
    {
        var stationId = _stationLimitedNetwork.GetNetworkStation(uid);

        if (sensor.StationId == stationId)
            return stationId;

        sensor.StationId = stationId;
        sensor.ConnectedServer = null;
        Dirty(uid, sensor);
        return stationId;
    }
    // DS14-end
}
