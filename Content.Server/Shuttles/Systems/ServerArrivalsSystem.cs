using System.Numerics;
using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Doors.Systems;
using Content.Server.GameTicking.Events;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Events;
using Content.Shared.Administration;
using Content.Shared.Antag;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.RoundEnd;
using Content.Shared.Salvage;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Spawners.Components;
using Content.Shared.Station.Systems;
using Content.Shared.Tiles;
using Robust.Shared.Collections;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Shuttles.Systems;

// DS14-start: the fork's arrivals flow differs from upstream.
// Latejoin players spawn directly on the arrivals shuttle, which waits on a hidden holding map and flies to the station.
// There is no separate arrivals terminal grid; the holding map carries the ArrivalsSourceComponent.
// DS14-end
/// <inheritdoc/>
public sealed partial class ServerArrivalsSystem : ArrivalsSystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private ActorSystem _actor = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private DoorSystem _door = default!; // DS14
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _station = default!;

    [Dependency] private EntityQuery<PendingClockInComponent> _pendingQuery;

    // DS14-start
    private static readonly TimeSpan DepartureDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EmptyReturnDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(1);
    // DS14-end

    public override void Initialize()
    {
        base.Initialize();

        Cfg.OnValueChanged(CCVars.ArrivalsShuttles, SetArrivals);

        // Command so admins can set these for funsies
        _console.RegisterCommand("arrivals", ArrivalsCommand, ArrivalsCompletion);
    }

    // DS14-start: holding map shuttle loop
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ArrivalsShuttleComponent, ShuttleComponent, TransformComponent>();
        var curTime = Timing.CurTime;

        while (query.MoveNext(out var uid, out var comp, out var shuttle, out var xform))
        {
            ProcessPendingClockIns(uid, comp);

            if (HasComp<FTLComponent>(uid) || comp.NextTransfer > curTime)
                continue;

            var tripTime = TimeSpan.FromSeconds(_shuttles.DefaultTravelTime + _shuttles.DefaultStartupTime);
            var onHoldingMap = xform.MapUid == comp.HoldingMap;
            SetShuttleDoorBolts(uid, onHoldingMap);

            if (onHoldingMap)
            {
                if (!HasPendingOnShuttle(uid) && !HasActivePlayersOnShuttle(uid))
                {
                    comp.NextArrivalsTime = TimeSpan.Zero;
                    comp.NextTransfer = curTime + IdlePollInterval;
                    continue;
                }

                var targetGrid = _station.GetLargestGrid(comp.Station);
                if (targetGrid == null)
                {
                    comp.NextTransfer = curTime + IdlePollInterval;
                    continue;
                }

                _shuttles.FTLToDock(uid, shuttle, targetGrid.Value);
                comp.NextArrivalsTime = curTime + tripTime;
                comp.NextTransfer = curTime + tripTime + EmptyReturnDelay;
                continue;
            }

            if (HasActivePlayersOnShuttle(uid))
            {
                comp.NextTransfer = curTime + EmptyReturnDelay;
                continue;
            }

            if (!TryComp<MapComponent>(comp.HoldingMap, out var holdingMap))
            {
                comp.NextTransfer = curTime + IdlePollInterval;
                continue;
            }

            var returnCoords = new EntityCoordinates(comp.HoldingMap, Vector2.Zero);
            _shuttles.FTLToCoordinates(uid, shuttle, returnCoords, Angle.Zero, useProximity: true);
            comp.NextArrivalsTime = TimeSpan.Zero;
            comp.NextTransfer = curTime + tripTime;
            _mapSystem.SetPaused(holdingMap.MapId, false);
        }
    }

    private void ProcessPendingClockIns(EntityUid shuttleUid, ArrivalsShuttleComponent shuttle)
    {
        var query = AllEntityQuery<PendingClockInComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == shuttleUid)
                continue;

            if (xform.MapUid == shuttle.HoldingMap)
            {
                if (TryPickShuttleSpawn(shuttleUid, out var spawnLoc))
                    _transform.SetCoordinates(uid, xform, spawnLoc);

                continue;
            }

            CompleteClockIn(uid, shuttle.Station);
        }
    }

    private void CompleteClockIn(EntityUid uid, EntityUid station)
    {
        RemCompDeferred<PendingClockInComponent>(uid);
        RemCompDeferred<AutoOrientComponent>(uid);

        if (ArrivalsGodmode)
            RemCompDeferred<GodmodeComponent>(uid);

        if (_actor.TryGetSession(uid, out var session) && session is not null)
            _antag.TryMakeLateJoinAntag(session);
    }

    private bool HasPendingOnShuttle(EntityUid shuttleUid)
    {
        var query = AllEntityQuery<PendingClockInComponent, TransformComponent>();

        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == shuttleUid)
                return true;
        }

        return false;
    }

    private bool HasActivePlayersOnShuttle(EntityUid shuttleUid)
    {
        var query = AllEntityQuery<ActorComponent, MobStateComponent, TransformComponent>();

        while (query.MoveNext(out _, out _, out _, out var xform))
        {
            if (xform.GridUid == shuttleUid)
                return true;
        }

        return false;
    }

    private void SetShuttleDoorBolts(EntityUid shuttleUid, bool bolted)
    {
        var query = AllEntityQuery<DoorComponent, DoorBoltComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var door, out var bolt, out var xform))
        {
            if (xform.GridUid != shuttleUid)
                continue;

            if (bolted)
            {
                _door.SetBoltLightsEnabled((uid, bolt), true);

                if (door.State != DoorState.Closed)
                {
                    if (door.State != DoorState.Closing)
                        _door.TryClose(uid, door);

                    continue;
                }
            }

            _door.SetBoltsDown((uid, bolt), bolted);
        }
    }

    private void SetDockedDoorBoltLights(EntityUid shuttleUid, bool enabled)
    {
        var query = AllEntityQuery<DockingComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var dock, out var xform))
        {
            if (xform.GridUid != shuttleUid || dock.DockedWith == null)
                continue;

            SetDoorBoltLights(uid, enabled);
            SetDoorBoltLights(dock.DockedWith.Value, enabled);
        }
    }

    private void SetDoorBoltLights(EntityUid uid, bool enabled)
    {
        if (TryComp<DoorBoltComponent>(uid, out var bolt))
            _door.SetBoltLightsEnabled((uid, bolt), enabled);
    }

    private bool TryGetArrivalsShuttle(EntityUid? gridUid, out Entity<ArrivalsShuttleComponent> shuttle)
    {
        if (gridUid != null && TryComp<ArrivalsShuttleComponent>(gridUid.Value, out var comp))
        {
            shuttle = (gridUid.Value, comp);
            return true;
        }

        shuttle = default;
        return false;
    }

    private bool TryPickShuttleSpawn(EntityUid shuttleUid, out EntityCoordinates spawnLoc)
    {
        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var possiblePositions = new ValueList<EntityCoordinates>(16);

        while (points.MoveNext(out _, out var spawnPoint, out var xform))
        {
            if (spawnPoint.SpawnType != SpawnPointType.LateJoin || xform.GridUid != shuttleUid)
                continue;

            possiblePositions.Add(xform.Coordinates);
        }

        if (possiblePositions.Count == 0)
        {
            spawnLoc = default;
            return false;
        }

        spawnLoc = Random.Pick(possiblePositions);
        return true;
    }

    private void QueueDepartureIfNeeded(Entity<ArrivalsShuttleComponent> shuttle)
    {
        var xform = Transform(shuttle.Owner);
        if (xform.MapUid != shuttle.Comp.HoldingMap)
            return;

        var departure = Timing.CurTime + DepartureDelay;
        if (shuttle.Comp.NextTransfer < Timing.CurTime || shuttle.Comp.NextTransfer > departure)
            shuttle.Comp.NextTransfer = departure;

        shuttle.Comp.NextArrivalsTime =
            departure + TimeSpan.FromSeconds(_shuttles.DefaultStartupTime + _shuttles.DefaultTravelTime);
    }
    // DS14-end

    private void SetupArrivalsStation()
    {
        // DS14: no arrivals terminal grid, every station gets its own shuttle on a holding map.
        var query = AllEntityQuery<StationArrivalsComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            SetupShuttle(uid, comp);
        }
    }

    private void SetArrivals(bool obj)
    {
        Enabled = obj;

        if (Enabled)
        {
            SetupArrivalsStation();
        }
        else
        {
            var shuttleQuery = AllEntityQuery<ArrivalsShuttleComponent>();

            while (shuttleQuery.MoveNext(out var uid, out _))
            {
                QueueDel(uid);
            }

            var sourceQuery = AllEntityQuery<ArrivalsSourceComponent>();

            while (sourceQuery.MoveNext(out var uid, out _))
            {
                QueueDel(uid);
            }
        }
    }

    private void SetupShuttle(EntityUid uid, StationArrivalsComponent component)
    {
        if (!Deleted(component.Shuttle))
            return;

        // DS14-start: load the shuttle on a hidden holding map instead of docking it to an arrivals terminal.
        var holdingMap = _mapSystem.CreateMap(out var holdingMapId, runMapInit: false);
        _metaData.SetEntityName(holdingMap, Loc.GetString("map-name-terminal"));
        EnsureComp<ArrivalsSourceComponent>(holdingMap);
        _mapSystem.InitializeMap(holdingMapId);

        if (!_loader.TryLoadGrid(holdingMapId, component.ShuttlePath, out var shuttle))
        {
            _mapSystem.DeleteMap(holdingMapId);
            return;
        }

        component.Shuttle = shuttle.Value;
        var arrivalsComp = EnsureComp<ArrivalsShuttleComponent>(component.Shuttle);
        arrivalsComp.Station = uid;
        arrivalsComp.HoldingMap = holdingMap;
        arrivalsComp.NextTransfer = Timing.CurTime + IdlePollInterval;
        arrivalsComp.NextArrivalsTime = TimeSpan.Zero;

        EnsureComp<ProtectedGridComponent>(component.Shuttle);
        EnsureComp<PreventPilotComponent>(component.Shuttle);
        SetShuttleDoorBolts(component.Shuttle, true);
        // DS14-end
    }

    [SubscribeLocalEvent]
    private void OnRoundStarting(RoundStartingEvent ev)
    {
        // Setup arrivals station
        if (!Enabled)
            return;

        SetupArrivalsStation();
    }

    [SubscribeLocalEvent]
    private void OnStationPostInit(EntityUid uid, StationArrivalsComponent component, ref StationPostInitEvent args)
    {
        if (!Enabled)
            return;

        // If it's a latespawn station then this will fail but that's okey
        SetupShuttle(uid, component);
    }

    /// <summary>
    ///     Sends shuttle timer data.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnArrivalsFTL(EntityUid shuttleUid, ArrivalsShuttleComponent component, ref FTLStartedEvent args)
    {
        if (!TryComp<DeviceNetworkComponent>(shuttleUid, out var netComp))
            return;

        TryComp<FTLComponent>(shuttleUid, out var ftlComp);
        var ftlTime = TimeSpan.FromSeconds(ftlComp?.TravelTime ?? _shuttles.DefaultTravelTime);

        // DS14-start
        var payload = new ScreenShuttlePayload
        {
            Shuttle = shuttleUid,
            ShuttleTime = ftlTime,
            SourceMap = args.FromMapUid,
            SourceTime = ftlTime,
        };

        if (Exists(args.TargetCoordinates.EntityId))
        {
            payload.DestinationMap = Transform(args.TargetCoordinates.EntityId).MapUid;
            payload.DestinationTime = ftlTime;
        }
        // DS14-end

        _deviceNetworkSystem.SendPacket(shuttleUid, null, ref payload, netComp.TransmitFrequency);
    }

    [SubscribeLocalEvent]
    private void OnArrivalsDocked(EntityUid uid, ArrivalsShuttleComponent component, ref FTLCompletedEvent args)
    {
        // DS14-start
        var onHoldingMap = args.MapUid == component.HoldingMap;
        SetShuttleDoorBolts(uid, onHoldingMap);

        if (!onHoldingMap)
            SetDockedDoorBoltLights(uid, false);

        var dockTime = component.NextTransfer > Timing.CurTime
            ? component.NextTransfer - Timing.CurTime + TimeSpan.FromSeconds(_shuttles.DefaultStartupTime)
            : TimeSpan.Zero;
        // DS14-end

        if (TryComp<DeviceNetworkComponent>(uid, out var netComp))
        {
            var payload = new ScreenShuttlePayload
            {
                Shuttle = uid,
                ShuttleTime = dockTime,
                SourceMap = args.MapUid,
                SourceTime = dockTime,
                Docked = true,
            };
            _deviceNetworkSystem.SendPacket(uid, null, ref payload, netComp.TransmitFrequency);
        }
    }

    // DS14-start
    [SubscribeLocalEvent]
    private void OnDockUndocked(EntityUid uid, DockingComponent component, ref UndockEvent args)
    {
        if (!HasComp<ArrivalsShuttleComponent>(args.GridAUid) &&
            !HasComp<ArrivalsShuttleComponent>(args.GridBUid))
        {
            return;
        }

        if (TryComp<DoorBoltComponent>(uid, out var bolt))
            _door.SetBoltLightsEnabled((uid, bolt), true);
    }
    // DS14-end

    [SubscribeLocalEvent]
    private void OnShuttleTag(EntityUid uid, ArrivalsShuttleComponent component, ref FTLTagEvent args)
    {
        if (args.Handled)
            return;

        // Just saves mappers forgetting. (v2 boogaloo)
        args.Handled = true;
        args.Tag = "DockArrivals";
    }

    [SubscribeLocalEvent]
    private void SendDirections(PlayerSpawnCompleteEvent ev)
    {
        if (!Enabled || !ev.LateJoin || ev.Silent || !_pendingQuery.HasComp(ev.Mob))
            return;

        // DS14-start: the shared spawning handler puts latejoiners on the shuttle; send it out shortly after.
        if (TryGetArrivalsShuttle(Transform(ev.Mob).GridUid, out var shuttle))
            QueueDepartureIfNeeded(shuttle);

        _chat.DispatchServerMessage(ev.Player, Loc.GetString("latejoin-arrivals-boarded"));
        // DS14-end
    }

    private bool TryTeleportToMapSpawn(EntityUid player, EntityUid stationId, TransformComponent? transform = null)
    {
        if (!Resolve(player, ref transform))
            return false;

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var possiblePositions = new ValueList<EntityCoordinates>(32);

        // Find a spawnpoint on the same map as the player is already docked with now.
        while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (spawnPoint.SpawnType == SpawnPointType.LateJoin &&
                _station.GetOwningStation(uid, xform) == stationId)
            {
                // Add to list of possible spawn locations
                possiblePositions.Add(xform.Coordinates);
            }
        }

        if (possiblePositions.Count > 0)
        {
            // Move the player to a random late-join spawnpoint.
            _transform.SetCoordinates(player, transform, Random.Pick(possiblePositions));
            if (_actor.TryGetSession(player, out var session))
            {
                _chat.DispatchServerMessage(session!, Loc.GetString("latejoin-arrivals-teleport-to-spawn"));
            }
            return true;
        }

        return false;
    }

    // DS14-start
    private void ForcePendingArrivals(IConsoleShell shell)
    {
        var query = AllEntityQuery<PendingClockInComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var pendingXform))
        {
            if (!TryGetArrivalsShuttle(pendingXform.GridUid, out var shuttle))
                continue;

            if (!TryTeleportToMapSpawn(uid, shuttle.Comp.Station, pendingXform))
                continue;

            CompleteClockIn(uid, shuttle.Comp.Station);
            shell.WriteLine(Loc.GetString("cmd-arrivals-forced", ("uid", ToPrettyString(uid))));
        }
    }
    // DS14-end

    [AdminCommand(AdminFlags.Fun)]
    private void ArrivalsCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("cmd-arrivals-invalid"));
            return;
        }

        switch (args[0])
        {
            case "enable":
                Cfg.SetCVar(CCVars.ArrivalsShuttles, true);
                break;
            case "disable":
                Cfg.SetCVar(CCVars.ArrivalsShuttles, false);
                break;
            case "returns":
                var existing = Cfg.GetCVar(CCVars.ArrivalsReturns);
                Cfg.SetCVar(CCVars.ArrivalsReturns, !existing);
                shell.WriteLine(Loc.GetString("cmd-arrivals-returns", ("value", !existing)));
                break;
            case "force":
                ForcePendingArrivals(shell); // DS14
                break;
            default:
                shell.WriteError(Loc.GetString($"cmd-arrivals-invalid"));
                break;
        }
    }

    private CompletionResult ArrivalsCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        return new CompletionResult(new CompletionOption[]
        {
            // Enables and disable are separate comms in case you don't want to accidentally toggle it, compared to
            // returns which doesn't have an immediate effect
            new("enable", Loc.GetString("cmd-arrivals-enable-hint")),
            new("disable", Loc.GetString("cmd-arrivals-disable-hint")),
            new("returns", Loc.GetString("cmd-arrivals-returns-hint")),
            new ("force", Loc.GetString("cmd-arrivals-force-hint"))
        },
            "Option");
    }
}
