// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Power.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Computer;
using Content.Shared.DeadSpace.Storyteller;
using Content.Shared.Disposal.Components;
using Content.Shared.Light;
using Content.Shared.Power;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Storyteller;

public sealed partial class StorytellerSystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;

    private EntityUid? _stageMap;
    private Entity<MapGridComponent>? _stageGrid;
    private string? _stageLocation;
    private readonly Dictionary<ushort, (string Proto, Vector2i Tile, Direction Dir, EntityUid Uid)> _stageProps = new();
    private readonly Dictionary<Vector2i, EntityUid> _stageWalls = new();

    public IReadOnlyDictionary<Vector2i, EntityUid> StageWalls => _stageWalls;

    public EntityUid? StageProp(ushort id)
    {
        return _stageProps.TryGetValue(id, out var prop) && !Deleted(prop.Uid) ? prop.Uid : null;
    }

    private void SyncStage(string locationId, StoryPropState[]? props)
    {
        if (!_prototypes.TryIndex<StoryLocationPrototype>(locationId, out var location))
            return;

        if (_stageLocation != location.ID || _stageGrid is not { } grid || Deleted(grid))
        {
            ClearStage();
            if (!BuildStage(location))
                return;
        }

        var seen = new HashSet<ushort>();
        foreach (var prop in props ?? Array.Empty<StoryPropState>())
        {
            if (!seen.Add(prop.Id))
                continue;

            var tile = new Vector2i(prop.X, prop.Y);
            if (_stageProps.TryGetValue(prop.Id, out var existing) && !Deleted(existing.Uid) && existing.Proto == prop.Proto)
            {
                if (existing.Tile == tile && existing.Dir == prop.Dir)
                    continue;

                Del(existing.Uid);
            }
            else if (existing.Uid.IsValid() && !Deleted(existing.Uid))
            {
                Del(existing.Uid);
            }

            _stageProps.Remove(prop.Id);
            if (Place(prop.Proto, tile, prop.Dir) is { } uid)
                _stageProps[prop.Id] = (prop.Proto, tile, prop.Dir, uid);
        }

        foreach (var id in new List<ushort>(_stageProps.Keys))
        {
            if (seen.Contains(id))
                continue;

            if (!Deleted(_stageProps[id].Uid))
                Del(_stageProps[id].Uid);
            _stageProps.Remove(id);
        }
    }

    private bool BuildStage(StoryLocationPrototype location)
    {
        var settings = StoryStage.Settings(_prototypes);
        try
        {
            _stageMap = _map.CreateMap(out var mapId, runMapInit: false);
            _stageGrid = _map.CreateGridEntity(mapId);
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to create a story stage map: {e.Message}");
            ClearStage();
            return false;
        }

        _stageLocation = location.ID;
        var floor = location.Floor is { } floorId && _tileDefs.TryGetDefinition(floorId, out var floorDef)
            ? floorDef
            : _tileDefs["Plating"];
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < settings.Width; x++)
        {
            for (var y = 0; y < settings.Height; y++)
                tiles.Add((GridTile(new Vector2i(x, y)), new Tile(floor.TileId)));
        }

        _map.SetTiles(_stageGrid.Value, tiles);

        if (location.Wall is { } wall)
        {
            for (var x = 0; x < settings.Width; x++)
                PlaceWall(wall, new Vector2i(x, 0));

            for (var y = 1; y < settings.Height; y++)
            {
                if (y != settings.DoorY)
                    PlaceWall(wall, new Vector2i(0, y));
                PlaceWall(wall, new Vector2i(settings.Width - 1, y));
            }
        }

        if (location.Door is { } door)
            PlaceWall(door, new Vector2i(0, settings.DoorY));

        return true;
    }

    private void PlaceWall(EntProtoId proto, Vector2i tile)
    {
        if (Place(proto, tile, Direction.South) is { } uid)
            _stageWalls[tile] = uid;
    }

    private EntityUid? Place(string proto, Vector2i tile, Direction dir)
    {
        if (_stageGrid is not { } grid || Deleted(grid) || !_prototypes.HasIndex<EntityPrototype>(proto))
            return null;

        EntityUid? uid = null;
        try
        {
            var at = GridTile(tile);
            uid = SpawnAttachedTo(proto, new EntityCoordinates(grid, at.X + 0.5f, at.Y + 0.5f));
            var xform = Transform(uid.Value);
            _transform.SetLocalRotation(uid.Value, dir.ToAngle(), xform);
            if (!xform.Anchored)
                _transform.AnchorEntity((uid.Value, xform), grid);
            Switch(uid.Value);
            return uid;
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to place story prop {proto}: {e.Message}");
            if (uid is { } failed && !Deleted(failed))
                Del(failed);
            return null;
        }
    }

    private void Switch(EntityUid uid)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, PowerDeviceVisuals.Powered, true, appearance);
            _appearance.SetData(uid, ComputerVisuals.Powered, true, appearance);
            _appearance.SetData(uid, PoweredLightVisuals.BulbState, PoweredLightState.On, appearance);
            _appearance.SetData(uid, DisposalUnitComponent.Visuals.VisualState, DisposalUnitComponent.VisualState.Anchored, appearance);
            if (HasComp<SolutionContainerVisualsComponent>(uid))
                _appearance.SetData(uid, SolutionContainerVisuals.FillFraction, 1f, appearance);
        }

        if (TryComp<ApcPowerReceiverComponent>(uid, out var power) && !power.Powered)
        {
            power.Powered = true;
            var ev = new PowerChangedEvent(true, 0f);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    private static Vector2i GridTile(Vector2i stage)
    {
        return new Vector2i(stage.X, -stage.Y);
    }

    private void ClearStage()
    {
        if (_stageMap is { } map && !Deleted(map))
            Del(map);

        _stageMap = null;
        _stageGrid = null;
        _stageLocation = null;
        _stageProps.Clear();
        _stageWalls.Clear();
    }
}
