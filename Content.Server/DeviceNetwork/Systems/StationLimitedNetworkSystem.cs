using Content.Server.DeviceNetwork.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.DeviceNetwork.Events;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Server.DeviceNetwork.Systems
{
    /// <summary>
    /// This system requires the StationLimitedNetworkComponent to be on the the sending entity as well as the receiving entity
    /// </summary>
    [UsedImplicitly]
    public sealed partial class StationLimitedNetworkSystem : EntitySystem
    {
        [Dependency] private StationSystem _stationSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StationLimitedNetworkComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<StationLimitedNetworkComponent, BeforePacketSentEvent>(OnBeforePacketSent);
        }

        /// <summary>
        /// Sets the station id the device is limited to.
        /// </summary>
        public void SetStation(EntityUid uid, EntityUid? stationId, StationLimitedNetworkComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.StationId = stationId;
        }

        /// <summary>
        /// Tries to set the station id to the current station if the device is currently on a station
        /// </summary>
        public bool TrySetStationId(EntityUid uid, StationLimitedNetworkComponent? component = null)
        {
            if (!Resolve(uid, ref component) || !Transform(uid).GridUid.HasValue)
                return false;

            component.StationId = GetNetworkStation(uid); // DS14
            return component.StationId.HasValue;
        }

        /// <summary>
        /// Set the station id to the one the entity is on when the station limited component is added
        /// </summary>
        private void OnMapInit(EntityUid uid, StationLimitedNetworkComponent networkComponent, MapInitEvent args)
        {
            networkComponent.StationId = GetNetworkStation(uid); // DS14
        }

        // DS14-start
        /// <summary>
        /// Returns the normal owning station, or the CentComm grid when the device is on the emergency CentComm map.
        /// </summary>
        public EntityUid? GetNetworkStation(EntityUid uid)
        {
            if (_stationSystem.GetOwningStation(uid) is { } station)
                return station;

            var xform = Transform(uid);
            if (xform.GridUid is not { } grid)
                return null;

            var centcommQuery = EntityQueryEnumerator<StationCentcommComponent>();
            while (centcommQuery.MoveNext(out _, out var centcomm))
            {
                if (centcomm.Entity == grid)
                    return grid;
            }

            return null;
        }
        // DS14-end

        /// <summary>
        /// Checks if both devices are limited to the same station
        /// </summary>
        private void OnBeforePacketSent(EntityUid uid, StationLimitedNetworkComponent component, BeforePacketSentEvent args)
        {
            RefreshNetworkStation(uid, component); // DS14

            if (!CheckStationId(args.Sender, component.AllowNonStationPackets, component.StationId))
            {
                args.Cancel();
            }
        }

        /// <summary>
        /// Compares the station IDs of the sending and receiving network components.
        /// Returns false if either of them doesn't have a station ID or if their station ID isn't equal.
        /// Returns true even when the sending entity isn't tied to a station if `allowNonStationPackets` is set to true.
        /// </summary>
        private bool CheckStationId(EntityUid senderUid, bool allowNonStationPackets, EntityUid? receiverStationId, StationLimitedNetworkComponent? sender = null)
        {
            if (!receiverStationId.HasValue)
                return false;

            if (!Resolve(senderUid, ref sender, false))
                return allowNonStationPackets;

            RefreshNetworkStation(senderUid, sender); // DS14

            return sender.StationId == receiverStationId;
        }

        // DS14-start
        private void RefreshNetworkStation(EntityUid uid, StationLimitedNetworkComponent component)
        {
            var stationId = GetNetworkStation(uid);

            if (!component.StationId.HasValue ||
                IsCentcommGrid(stationId) ||
                IsCentcommGrid(component.StationId))
            {
                component.StationId = stationId;
            }
        }

        private bool IsCentcommGrid(EntityUid? uid)
        {
            if (uid == null)
                return false;

            var centcommQuery = EntityQueryEnumerator<StationCentcommComponent>();
            while (centcommQuery.MoveNext(out _, out var centcomm))
            {
                if (centcomm.Entity == uid)
                    return true;
            }

            return false;
        }
        // DS14-end
    }
}
