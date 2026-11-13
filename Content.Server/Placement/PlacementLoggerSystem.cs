using Content.Server.Administration.Logs;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Robust.Shared.Map;
using Robust.Shared.Placement;
using Robust.Shared.Player;

namespace Content.Server.Placement;

public sealed partial class PlacementLoggerSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlacementEntityEvent>(OnEntityPlacement);
        SubscribeLocalEvent<PlacementTileEvent>(OnTilePlacement);
    }

    private void OnEntityPlacement(PlacementEntityEvent ev)
    {
        _player.TryGetSessionById(ev.PlacerNetUserId, out var actor);
        var actorEntity = actor?.AttachedEntity;

        var logType = ev.PlacementEventAction switch
        {
            PlacementEventAction.Create => LogType.EntitySpawn,
            PlacementEventAction.Erase => LogType.EntityDelete,
            _ => LogType.Action
        };

        var station = GetStationSuffix(ev.EditedEntity);

        if (actorEntity != null)
            _adminLogger.Add(logType, LogImpact.Medium,
                $"{ToPrettyString(actorEntity.Value):actor} used placement system to {ev.PlacementEventAction.ToString().ToLower()} {ToPrettyString(ev.EditedEntity):subject}{station} at {ev.Coordinates}");
        else if (actor != null)
            _adminLogger.Add(logType, LogImpact.Medium,
                $"{actor:actor} used placement system to {ev.PlacementEventAction.ToString().ToLower()} {ToPrettyString(ev.EditedEntity):subject}{station} at {ev.Coordinates}");
        else
            _adminLogger.Add(logType, LogImpact.Medium,
                $"Placement system {ev.PlacementEventAction.ToString().ToLower()}ed {ToPrettyString(ev.EditedEntity):subject}{station} at {ev.Coordinates}");
    }

    private void OnTilePlacement(PlacementTileEvent ev)
    {
        _player.TryGetSessionById(ev.PlacerNetUserId, out var actor);
        var actorEntity = actor?.AttachedEntity;
        var station = GetStationSuffix(ev.Coordinates);

        if (actorEntity != null)
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"{ToPrettyString(actorEntity.Value):actor} used placement system to set tile {_tileDefinitionManager[ev.TileType].Name}{station} at {ev.Coordinates}");
        else if (actor != null)
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"{actor} used placement system to set tile {_tileDefinitionManager[ev.TileType].Name}{station} at {ev.Coordinates}");
        else
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"Placement system set tile {_tileDefinitionManager[ev.TileType].Name}{station} at {ev.Coordinates}");
    }

    private string GetStationSuffix(EntityUid uid)
    {
        if (!Exists(uid) || _station.GetOwningStation(uid) is not { } station)
            return string.Empty;

        return $" on station {ToPrettyString(station):station}";
    }

    private string GetStationSuffix(EntityCoordinates coordinates)
    {
        if (!Exists(coordinates.EntityId) || _station.GetOwningStation(coordinates.EntityId) is not { } station)
            return string.Empty;

        return $" on station {ToPrettyString(station):station}";
    }
}
