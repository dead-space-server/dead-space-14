// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server.Weather;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.DeadSpace.CentComm;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.CentComm;

public sealed class CentCommTransferSystem : EntitySystem
{
    public static readonly TimeSpan PreparationTime = TimeSpan.FromMinutes(1);

    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IAdminLogManager _log = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly WeatherSystem _weather = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly GameRuleStationSystem _ruleStation = default!;

    private TimeSpan _nextUiUpdate;
    public event Action? StateChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CentCommTransferComponent, FTLCompletedEvent>(OnArrived);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => StateChanged?.Invoke());
    }

    public string[] GetParallaxes() => _prototypes.EnumeratePrototypes<ServerParallaxPrototype>()
        .Select(prototype => prototype.ID).OrderBy(id => id, StringComparer.Ordinal).ToArray();

    public string[] GetWeather() => _prototypes.EnumeratePrototypes<WeatherPrototype>()
        .Select(prototype => prototype.ID).OrderBy(id => id, StringComparer.Ordinal).ToArray();

    public bool CanStart(out string status)
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
        {
            status = Loc.GetString("centcomm-transfer-round-required");
            return false;
        }

        var grid = _roundEnd.GetCentcommGridEntity();
        if (grid == null || !HasComp<CentCommStationComponent>(_station.GetOwningStation(grid)) ||
            !HasComp<MapGridComponent>(grid) || !HasComp<PhysicsComponent>(grid) || !HasComp<ShuttleComponent>(grid))
        {
            status = Loc.GetString("cmd-addgamerulecentcomm-no-station");
            return false;
        }

        if (TryComp<CentCommTransferComponent>(grid, out var transfer))
        {
            status = Loc.GetString(transfer.Arrived ? "centcomm-transfer-cooldown" : transfer.JumpStarted
                ? "centcomm-transfer-travelling" : "centcomm-transfer-preparing",
                ("seconds", Math.Max(0, Math.Ceiling((transfer.StartAt - _timing.CurTime).TotalSeconds))));
            return false;
        }

        if (HasComp<FTLComponent>(grid))
        {
            status = Loc.GetString("centcomm-transfer-cooldown");
            return false;
        }

        status = Loc.GetString("centcomm-transfer-ready");
        return true;
    }

    public bool TryStart(ICommonSession? requester, CentCommTransferRequest request, out string result)
    {
        if (requester != null && !_admin.HasAdminFlag(requester, AdminFlags.Fun))
        {
            result = Loc.GetString("centcomm-permission-denied");
            return false;
        }

        if (!_prototypes.HasIndex<ServerParallaxPrototype>(request.Parallax))
        {
            result = Loc.GetString("centcomm-transfer-invalid-parallax");
            return false;
        }

        if (request.Weather != null && !_prototypes.HasIndex<WeatherPrototype>(request.Weather))
        {
            result = Loc.GetString("centcomm-transfer-invalid-weather");
            return false;
        }

        if (!CentCommTransferSettings.TryGetTemperature(request.Temperature, request.Celsius, out var temperature))
        {
            result = Loc.GetString("centcomm-transfer-invalid-temperature",
                ("min", CentCommTransferSettings.MinCelsius), ("max", CentCommTransferSettings.MaxCelsius));
            return false;
        }

        if (!CanStart(out result))
            return false;

        var grid = _roundEnd.GetCentcommGridEntity()!.Value;
        var transform = Transform(grid);
        if (transform.MapUid == null || transform.ParentUid != transform.MapUid)
        {
            result = Loc.GetString("centcomm-transfer-invalid-grid");
            return false;
        }

        var transfer = AddComp<CentCommTransferComponent>(grid);
        transfer.Origin = transform.Coordinates;
        transfer.Rotation = transform.LocalRotation;
        transfer.StartAt = _timing.CurTime + PreparationTime;
        transfer.Parallax = request.Parallax;
        transfer.Temperature = temperature;
        transfer.Weather = request.Weather;
        transfer.Requester = requester;

        var announcement = Loc.GetString("centcomm-transfer-announcement");
        _chat.DispatchAdminFilteredAnnouncement(_ruleStation.GetEventPlayers(grid), announcement,
            sender: Loc.GetString("chat-manager-sender-announcement"),
            colorOverride: Color.FromHex("#1d8bad"),
            announcementSound: new SoundPathSpecifier("/Audio/Misc/gamma.ogg"),
            originalMessage: announcement, voice: "Announcer");
        _log.Add(LogType.AdminMessage, LogImpact.High,
            $"{requester?.Name ?? "Server console"} queued CentComm transfer: parallax={request.Parallax}, temperature={temperature?.ToString() ?? "space"} K, weather={request.Weather ?? "none"}");
        StateChanged?.Invoke();
        result = Loc.GetString("centcomm-transfer-preparing", ("seconds", PreparationTime.TotalSeconds));
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CentCommTransferComponent>();
        while (query.MoveNext(out var grid, out var transfer))
        {
            if (TerminatingOrDeleted(grid) || EntityManager.IsQueuedForDeletion(grid))
                continue;

            if (transfer.Arrived)
            {
                if (!HasComp<FTLComponent>(grid))
                {
                    RemCompDeferred<CentCommTransferComponent>(grid);
                    StateChanged?.Invoke();
                }
                continue;
            }

            if (transfer.JumpStarted)
                continue;

            if (_ticker.RunLevel != GameRunLevel.InRound || !Exists(transfer.Origin.EntityId) ||
                (transfer.Requester is { } requester &&
                 (requester.Status is SessionStatus.Disconnected or SessionStatus.Zombie ||
                  !_admin.HasAdminFlag(requester, AdminFlags.Fun))))
            {
                RemCompDeferred<CentCommTransferComponent>(grid);
                _log.Add(LogType.AdminMessage, $"Cancelled CentComm transfer before departure: {ToPrettyString(grid)}");
                StateChanged?.Invoke();
                continue;
            }

            if (_timing.CurTime < transfer.StartAt)
                continue;

            if (HasComp<FTLComponent>(grid) || !TryComp<ShuttleComponent>(grid, out var shuttle))
            {
                RemCompDeferred<CentCommTransferComponent>(grid);
                StateChanged?.Invoke();
                continue;
            }

            foreach (var dock in _docking.GetDocks(grid))
            {
                if (dock.Comp.DockedWith is { } other)
                    transfer.Docks.Add((dock.Owner, other));
            }

            transfer.JumpStarted = true;
            _shuttle.FTLToCoordinates(grid, shuttle, transfer.Origin, transfer.Rotation);
            StateChanged?.Invoke();
        }

        if (_timing.CurTime >= _nextUiUpdate)
        {
            _nextUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
            StateChanged?.Invoke();
        }
    }

    private void OnArrived(Entity<CentCommTransferComponent> ent, ref FTLCompletedEvent args)
    {
        if (!ent.Comp.JumpStarted || ent.Comp.Arrived)
            return;

        ent.Comp.Arrived = true;
        // FTL enables dynamic physics; Central Command must remain a fixed station after arriving.
        _shuttle.Disable(ent.Owner);
        Comp<ShuttleComponent>(ent).Enabled = false;

        if (Transform(ent).MapUid != ent.Comp.Origin.EntityId)
        {
            Log.Error("CentComm did not return to its original map; its environment was not changed.");
            StateChanged?.Invoke();
            return;
        }

        var map = ent.Comp.Origin.EntityId;
        var parallax = EnsureComp<ParallaxComponent>(map);
        parallax.Parallax = ent.Comp.Parallax;
        Dirty(map, parallax);
        var mixture = GasMixture.SpaceGas;
        if (ent.Comp.Temperature is { } temperature)
        {
            mixture = new GasMixture(Atmospherics.CellVolume) { Temperature = temperature };
            var moles = Atmospherics.OneAtmosphere * Atmospherics.CellVolume / (Atmospherics.R * temperature);
            mixture.SetMoles(Gas.Oxygen, moles * 0.21f);
            mixture.SetMoles(Gas.Nitrogen, moles * 0.79f);
        }
        _atmosphere.SetMapAtmosphere(map, ent.Comp.Temperature == null, mixture);
        _weather.SetWeather(Transform(ent).MapID,
            ent.Comp.Weather is { } weather ? _prototypes.Index<WeatherPrototype>(weather) : null, null);

        foreach (var (dock, other) in ent.Comp.Docks)
        {
            if (TryComp<DockingComponent>(dock, out var a) && TryComp<DockingComponent>(other, out var b) &&
                _docking.CanDock((dock, a), (other, b)))
                _docking.Dock((dock, a), (other, b));
        }

        _log.Add(LogType.AdminMessage, LogImpact.High, $"CentComm transfer completed at {ent.Comp.Origin}");
        StateChanged?.Invoke();
    }
}
