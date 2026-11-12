<<<<<<< ours
﻿using System.Diagnostics.CodeAnalysis;
using Content.Server.DeviceNetwork.Components;
using Content.Server.Medical.CrewMonitoring;
using Content.Shared.Power;
||||||| base
﻿using System.Diagnostics.CodeAnalysis;
using Content.Server.DeviceNetwork.Components;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Station.Systems;
using Content.Shared.Power;
=======
using System.Diagnostics.CodeAnalysis;
>>>>>>> theirs
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Power;
using Content.Shared.Station.Systems;

namespace Content.Shared.DeviceNetwork.Systems;

/// <summary>
/// Keeps one active server entity per station. Activates another available one if the currently active server becomes unavailable
/// Server in this context means an entity that manages the devicenet packets like the <see cref="Content.Server.Medical.CrewMonitorin.CrewMonitoringServerSystem"/>.
/// </summary>
public sealed partial class SingletonDeviceNetServerSystem : EntitySystem
{
<<<<<<< ours
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly StationLimitedNetworkSystem _stationLimitedNetwork = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SingletonDeviceNetServerComponent, PowerChangedEvent>(OnPowerChanged);
    }
||||||| base
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SingletonDeviceNetServerComponent, PowerChangedEvent>(OnPowerChanged);
    }
=======
    [Dependency] private DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;
>>>>>>> theirs

    /// <summary>
    /// Returns whether the given entity is an active server or not.
    /// </summary>
    public bool IsActiveServer(EntityUid serverId, SingletonDeviceNetServerComponent? serverComponent = default)
    {
        return Resolve(serverId, ref serverComponent) && serverComponent.Active;
    }

    /// <summary>
    /// Returns the address of the currently active server for the given station id if there is one.<br/>
    /// What kind of server you're trying to get the active instance of is determined by the component type parameter TComp.<br/>
    /// <br/>
    /// Setting TComp to <see cref="Content.Server.Medical.CrewMonitoring"/>, for example, gives you the address of an entity containing the crew monitoring server component.<br/>
    /// </summary>
    /// <param name="stationId">The entityUid of the station</param>
    /// <param name="address">The address of the active server if it exists</param>
    /// <typeparam name="TComp">The component type that determines what type of server you're getting the address of</typeparam>
    /// <returns>True if there is an active serve. False otherwise</returns>
<<<<<<< ours
    public bool TryGetActiveServerAddress<TComp>(EntityUid stationId, [NotNullWhen(true)] out string? address, uint? receiveFrequency = null) where TComp : IComponent // DS14
||||||| base
    public bool TryGetActiveServerAddress<TComp>(EntityUid stationId, [NotNullWhen(true)] out string? address) where TComp : IComponent
=======
    public bool TryGetActiveServerAddress<TComp>(EntityUid stationId, [NotNullWhen(true)] out DeviceAddress? address) where TComp : IComponent
>>>>>>> theirs
    {
        var servers = EntityQueryEnumerator<
            SingletonDeviceNetServerComponent,
            DeviceNetworkComponent,
            TComp
        >();

        (EntityUid id, SingletonDeviceNetServerComponent server, DeviceNetworkComponent device)? last = default;

        while (servers.MoveNext(out var uid, out var server, out var device, out _))
        {
            // DS14-start
            if (_stationLimitedNetwork.GetNetworkStation(uid) != stationId)
                continue;

            if (receiveFrequency != null && device.ReceiveFrequency != receiveFrequency)
                continue;
            // DS14-end

            if (!server.Available)
            {
                DisconnectServer((uid, server, device));
                continue;
            }

            last = (uid, server, device);

            if (!server.Active || device.Address == 0)
                continue;

            address = device.Address;
            return true;
        }

        // If there was no active server for the station make the last available inactive one active
        if (last.HasValue)
        {
            ConnectServer((last.Value.id, last.Value.server, last.Value.device));
            address = last.Value.device.Address;
            return true;
        }

        address = null;
        return address != null;
    }

    /// <summary>
    /// Disconnects the server losing power.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<SingletonDeviceNetServerComponent> ent, ref PowerChangedEvent args)
    {
        ent.Comp.Available = args.Powered;

        if (!args.Powered && ent.Comp.Active)
            DisconnectServer(ent!);
    }

    private void ConnectServer(Entity<SingletonDeviceNetServerComponent?, DeviceNetworkComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp1, ref ent.Comp2))
            return;

        ent.Comp1.Active = true;

        var connectedEvent = new DeviceNetServerConnectedEvent();
        RaiseLocalEvent(ent, ref connectedEvent);

        if (_deviceNetworkSystem.IsDeviceConnected((ent.Owner, ent.Comp2)))
            return;

        _deviceNetworkSystem.ConnectDevice((ent.Owner, ent.Comp2));
    }

    /// <summary>
    /// Disconnects a server from the device network and clears the currently active server.
    /// </summary>
    private void DisconnectServer(Entity<SingletonDeviceNetServerComponent?, DeviceNetworkComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp1, ref ent.Comp2))
            return;

        ent.Comp1.Active = false;

        var disconnectedEvent = new DeviceNetServerDisconnectedEvent();
        RaiseLocalEvent(ent, ref disconnectedEvent);

        _deviceNetworkSystem.DisconnectDevice((ent.Owner, ent.Comp2), false);
    }
}

/// <summary>
/// Raised when a server gets activated and connected to the device net.
/// </summary>
[ByRefEvent]
public record struct DeviceNetServerConnectedEvent;

/// <summary>
/// Raised when a server gets disconnected.
/// </summary>
[ByRefEvent]
public record struct DeviceNetServerDisconnectedEvent;
