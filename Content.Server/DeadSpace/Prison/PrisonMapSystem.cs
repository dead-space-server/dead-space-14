using System.Numerics;
using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace.Prison.Components;
using Content.Server.Parallax;
using Content.Server.Shuttles.Components;
using Content.Shared.Atmos;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Prison;
using Content.Shared.GameTicking;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Shuttles.Components;
using Content.Shared.Warps;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Prison;

public sealed partial class PrisonMapSystem : EntitySystem
{
    private const string PrisonPlanet = "PrisonQuarry";
    private const string PrisonWarpLocation = "Prison Quarry";

    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private PrisonFaunaPopulationSystem _faunaPopulation = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityUid? _generatedMap;
    private bool _enabled;
    private bool _generationFailed;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_configuration, CCCCVars.PrisonEnabled, OnPrisonEnabledChanged, true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || _generationFailed)
            return;

        if (_generatedMap is { Valid: true } generatedMap && Exists(generatedMap) && !Deleted(generatedMap))
            return;

        _generatedMap = null;

        if (HasPrisonSpawnPoint())
            return;

        GeneratePrisonMap();
    }

    private void OnPrisonEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        if (enabled)
        {
            _generationFailed = false;
            return;
        }

        DeleteGeneratedMap();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        DeleteGeneratedMap();
        _generationFailed = false;
    }

    private bool HasPrisonSpawnPoint()
    {
        var query = EntityQueryEnumerator<PrisonSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != MapId.Nullspace)
                return true;
        }

        return false;
    }

    private void GeneratePrisonMap()
    {
        if (!_prototype.TryIndex<PrisonPlanetPrototype>(PrisonPlanet, out var planet))
        {
            Log.Error($"Unable to generate prison map: prisonPlanet prototype {PrisonPlanet} was not found.");
            _generationFailed = true;
            return;
        }

        try
        {
            var seed = _random.Next();
            var random = new Random(seed);
            var mapUid = _map.CreateMap(out var mapId, runMapInit: false);
            var grid = EnsureComp<MapGridComponent>(mapUid);
            _generatedMap = mapUid;

            SetupMetadata(mapUid, planet);
            SetupFtl(mapUid, planet);
            var biome = SetupBiome(mapUid, planet, seed);
            var marker = AddComp<PrisonMapComponent>(mapUid);
            marker.Planet = PrisonPlanet;

            _map.InitializeMap(mapId);
            _map.SetPaused(mapUid, true);

            PrepareMapBoundary(mapUid, grid, planet, random);
            var residences = CreateResidencePlacements(planet, random);
            PrepareResidenceReservations(mapUid, grid, biome, planet, residences, random);
            var residenceGrid = LoadResidenceGrids(mapId, planet, residences);
            PrepareLandingPad(mapUid, grid, biome, planet, random);
            CreateFtlBeacon(mapUid, planet, residenceGrid);
            CreateGhostWarp(mapUid);
            PreloadResidenceAreas(mapUid, biome, residences);
            PreloadLandingArea(mapUid, biome, planet);
            _faunaPopulation.SetupMap(
                mapUid,
                planet,
                residences.Select(residence => ToBox2(residence.ReservationBounds)).ToArray());

            _map.SetPaused(mapUid, false);
            Log.Info($"Generated prison map {planet.ID} with seed {seed}.");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to generate prison map {planet.ID}: {e}");
            DeleteGeneratedMap();
            _generationFailed = true;
        }
    }

    private void DeleteGeneratedMap()
    {
        if (_generatedMap is not { Valid: true } map)
        {
            _generatedMap = null;
            return;
        }

        if (Exists(map) && !Deleted(map))
            QueueDel(map);

        _generatedMap = null;
    }

    private void SetupMetadata(EntityUid mapUid, PrisonPlanetPrototype planet)
    {
        _metadata.SetEntityName(mapUid, planet.MapName);
    }

    private void SetupFtl(EntityUid mapUid, PrisonPlanetPrototype planet)
    {
        if (!planet.FtlEnabled)
            return;

        var destination = EnsureComp<FTLDestinationComponent>(mapUid);
        destination.Enabled = true;
        destination.BeaconsOnly = planet.FtlBeaconsOnly;
        destination.RequireCoordinateDisk = planet.RequireCoordinateDisk;
        destination.Whitelist = planet.FtlWhitelist;
        Dirty(mapUid, destination);
    }

    private BiomeComponent SetupBiome(EntityUid mapUid, PrisonPlanetPrototype planet, int seed)
    {
        var biome = EntityManager.ComponentFactory.GetComponent<BiomeComponent>();
        _biome.SetSeed(mapUid, biome, seed, false);
        _biome.SetTemplate(mapUid, biome, _prototype.Index(planet.Biome), false);
        _biome.SetBounds(mapUid, biome, CreateMapBounds(planet), false);
        AddComp(mapUid, biome, true);

        foreach (var markerLayer in planet.MarkerLayers)
        {
            _biome.AddMarkerLayer(mapUid, biome, markerLayer);
        }

        if (planet.Gravity)
        {
            var gravity = EnsureComp<GravityComponent>(mapUid);
            gravity.Enabled = true;
            gravity.Inherent = true;
            Dirty(mapUid, gravity);
        }

        if (planet.LightColor != null)
        {
            var light = EnsureComp<MapLightComponent>(mapUid);
            light.AmbientLightColor = planet.LightColor.Value;
            Dirty(mapUid, light);
        }

        var atmosphere = planet.Atmosphere != null
            ? CopyAtmosphere(planet.Atmosphere)
            : CreateDefaultAtmosphere();
        _atmosphere.SetMapAtmosphere(mapUid, false, atmosphere);

        return biome;
    }

    private static Box2i? CreateMapBounds(PrisonPlanetPrototype planet)
    {
        if (planet.MapHalfSize <= 0)
            return null;

        var halfSize = Math.Max(1, planet.MapHalfSize);
        return new Box2i(-halfSize, -halfSize, halfSize, halfSize);
    }

    private static GasMixture CreateDefaultAtmosphere()
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 14f;
        moles[(int) Gas.Nitrogen] = 23f;
        return new GasMixture(moles, 300f);
    }

    private static GasMixture CopyAtmosphere(GasMixture atmosphere)
    {
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        foreach (var (gas, amount) in atmosphere)
        {
            moles[(int) gas] = amount;
        }

        return new GasMixture(moles, atmosphere.Temperature, atmosphere.Volume);
    }

    private void PrepareMapBoundary(
        EntityUid mapUid,
        MapGridComponent grid,
        PrisonPlanetPrototype planet,
        Random random)
    {
        if (!planet.BoundaryEnabled || planet.MapHalfSize <= 0)
            return;

        var halfSize = Math.Max(1, planet.MapHalfSize);
        var wallWidth = Math.Max(1, planet.BoundaryWallWidth);
        var boundaryWidth = Math.Min(halfSize, wallWidth);

        if (boundaryWidth <= 0)
            return;

        var tileDef = _tileDefinition[planet.BoundaryTile];
        var capacity = GetSquareRingTileCount(halfSize, boundaryWidth);
        var tiles = new List<(Vector2i Index, Tile Tile)>(capacity);
        var wallTiles = new List<Vector2i>(capacity);

        for (var x = -halfSize; x < halfSize; x++)
        {
            for (var y = -halfSize; y < halfSize; y++)
            {
                var edgeDistance = GetDistanceToSquareEdge(x, y, halfSize);
                if (edgeDistance >= boundaryWidth)
                    continue;

                var index = new Vector2i(x, y);
                tiles.Add((index, CreateTile(tileDef, random)));

                wallTiles.Add(index);
            }
        }

        _map.SetTiles(mapUid, grid, tiles);

        foreach (var tile in wallTiles)
        {
            SpawnAnchored(planet.BoundaryWallEntity, mapUid, grid, tile);
        }
    }

    private static List<ResidencePlacement> CreateResidencePlacements(
        PrisonPlanetPrototype planet,
        Random random)
    {
        var placements = new List<ResidencePlacement>(planet.Residences.Count);
        if (!planet.RandomizeResidencePositions)
        {
            foreach (var residence in planet.Residences)
            {
                var offset = new Vector2i(
                    (int) MathF.Round(residence.GridOffset.X),
                    (int) MathF.Round(residence.GridOffset.Y));
                placements.Add(new ResidencePlacement(
                    residence,
                    offset,
                    TranslateBounds(GetResidenceRelativeBounds(planet, residence), offset)));
            }

            return placements;
        }

        var boundaryPadding = planet.BoundaryEnabled ? Math.Max(1, planet.BoundaryWallWidth) : 0;
        var mapLimit = planet.MapHalfSize - boundaryPadding -
                       (int) MathF.Ceiling(Math.Max(0f, planet.ResidenceMapEdgePadding));
        if (mapLimit <= 0)
            throw new InvalidOperationException("Prison residence placement area is empty.");

        var landingRadius = Math.Max(1, planet.LandingPadRadius) +
                            Math.Max(4f, planet.FaunaResidenceExclusionPadding);
        var landingBounds = Box2.CenteredAround(
            planet.FtlBeaconOffset,
            new Vector2(landingRadius * 2f + 1f));
        var minSeparationSquared = MathF.Pow(Math.Max(0f, planet.ResidenceMinSeparation), 2f);
        var attempts = Math.Max(1, planet.ResidencePlacementAttempts);

        foreach (var residence in planet.Residences)
        {
            var relativeBounds = GetResidenceRelativeBounds(planet, residence);
            var minX = -mapLimit - relativeBounds.Left;
            var maxX = mapLimit - relativeBounds.Right;
            var minY = -mapLimit - relativeBounds.Bottom;
            var maxY = mapLimit - relativeBounds.Top;
            if (minX > maxX || minY > maxY)
                throw new InvalidOperationException(
                    $"Prison residence {residence.GridPath} does not fit inside the map boundary.");

            var placed = false;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var offset = new Vector2i(
                    random.Next(minX, maxX + 1),
                    random.Next(minY, maxY + 1));
                var bounds = TranslateBounds(relativeBounds, offset);
                if (ToBox2(bounds).Intersects(landingBounds))
                    continue;

                var position = new Vector2(offset.X, offset.Y);
                if (placements.Any(existing =>
                        Vector2.DistanceSquared(position, existing.Offset) < minSeparationSquared))
                {
                    continue;
                }

                placements.Add(new ResidencePlacement(residence, position, bounds));
                placed = true;
                break;
            }

            if (!placed)
                throw new InvalidOperationException(
                    $"Unable to find a safe random position for prison residence {residence.GridPath}.");
        }

        return placements;
    }

    private void PrepareResidenceReservations(
        EntityUid mapUid,
        MapGridComponent grid,
        BiomeComponent biome,
        PrisonPlanetPrototype planet,
        IReadOnlyList<ResidencePlacement> residences,
        Random random)
    {
        if (!planet.ResidenceReservationEnabled)
            return;

        var tileDef = _tileDefinition[planet.ResidenceTile];
        foreach (var residence in residences)
        {
            var bounds = residence.ReservationBounds;
            var reserved = new List<(Vector2i Index, Tile Tile)>();
            _biome.ReserveTiles(mapUid, ToBox2(bounds), reserved, biome, grid);

            var tiles = new List<(Vector2i Index, Tile Tile)>(bounds.Area);
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                for (var y = bounds.Bottom; y < bounds.Top; y++)
                {
                    tiles.Add((new Vector2i(x, y), CreateTile(tileDef, random)));
                }
            }

            _map.SetTiles(mapUid, grid, tiles);
        }
    }

    private EntityUid? LoadResidenceGrids(
        MapId mapId,
        PrisonPlanetPrototype planet,
        IReadOnlyList<ResidencePlacement> residences)
    {
        EntityUid? firstGrid = null;
        foreach (var placement in residences)
        {
            var residence = placement.Definition;
            if (!_mapLoader.TryLoadGrid(mapId, residence.GridPath, out var grid, offset: placement.Offset))
                throw new InvalidOperationException(
                    $"Failed to load prison residence grid {residence.GridPath} for planet {planet.ID}.");

            if (grid == null)
                throw new InvalidOperationException(
                    $"Prison residence file {residence.GridPath} did not contain a grid.");

            firstGrid ??= grid;
            if (!string.IsNullOrWhiteSpace(residence.GridName))
                _metadata.SetEntityName(grid.Value, residence.GridName);
        }

        return firstGrid;
    }

    private void PrepareLandingPad(
        EntityUid mapUid,
        MapGridComponent grid,
        BiomeComponent biome,
        PrisonPlanetPrototype planet,
        Random random)
    {
        var radius = Math.Max(1, planet.LandingPadRadius);
        var bounds = Box2.CenteredAround(planet.FtlBeaconOffset, new Vector2(radius * 2 + 1, radius * 2 + 1));
        var reserved = new List<(Vector2i Index, Tile Tile)>();
        _biome.ReserveTiles(mapUid, bounds, reserved, biome, grid);

        var tileDef = _tileDefinition[planet.LandingPadTile];
        var tiles = new List<(Vector2i Index, Tile Tile)>();
        var radiusSquared = radius * radius;
        var center = new Vector2i(
            (int) MathF.Floor(planet.FtlBeaconOffset.X),
            (int) MathF.Floor(planet.FtlBeaconOffset.Y));

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > radiusSquared)
                    continue;

                tiles.Add((center + new Vector2i(x, y), CreateTile(tileDef, random)));
            }
        }

        _map.SetTiles(mapUid, grid, tiles);
    }

    private void CreateFtlBeacon(EntityUid mapUid, PrisonPlanetPrototype planet, EntityUid? residenceGrid)
    {
        if (!planet.FtlEnabled)
            return;

        var beaconUid = Spawn(null, new EntityCoordinates(mapUid, planet.FtlBeaconOffset));
        _metadata.SetEntityName(beaconUid, planet.FtlBeaconName);
        EnsureComp<FTLBeaconComponent>(beaconUid);

        var dockingBeacon = EnsureComp<FTLDockingBeaconComponent>(beaconUid);
        dockingBeacon.TargetGrid = residenceGrid;
        dockingBeacon.DockWhitelist = planet.FtlDockWhitelist;
        dockingBeacon.FallbackMinOffset = planet.FtlFallbackMinOffset;
        dockingBeacon.FallbackMaxOffset = planet.FtlFallbackMaxOffset;
    }

    private void CreateGhostWarp(EntityUid mapUid)
    {
        var warpUid = Spawn("GhostWarpPoint", new EntityCoordinates(mapUid, Vector2.Zero));
        var warp = EnsureComp<WarpPointComponent>(warpUid);
        warp.Location = PrisonWarpLocation;
        Dirty(warpUid, warp);

        _transform.AttachToGridOrMap(warpUid);
    }

    private void PreloadResidenceAreas(
        EntityUid mapUid,
        BiomeComponent biome,
        IReadOnlyList<ResidencePlacement> residences)
    {
        foreach (var residence in residences)
            _biome.Preload(mapUid, biome, ToBox2(residence.ReservationBounds).Enlarged(16f));
    }

    private void PreloadLandingArea(EntityUid mapUid, BiomeComponent biome, PrisonPlanetPrototype planet)
    {
        var radius = Math.Max(1, planet.LandingPadRadius);
        var size = new Vector2(radius * 2 + 1, radius * 2 + 1);
        _biome.Preload(mapUid, biome, Box2.CenteredAround(planet.FtlBeaconOffset, size).Enlarged(16f));
    }

    private void SpawnAnchored(
        string prototype,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i index)
    {
        var uid = Spawn(prototype, _map.GridTileToLocal(gridUid, grid, index));

        if (!_xformQuery.TryGetComponent(uid, out var xform) || xform.Anchored)
            return;

        _transform.AnchorEntity((uid, xform), (gridUid, grid), index);
    }

    private Tile CreateTile(ITileDefinition tileDef, Random random)
    {
        return new Tile(tileDef.TileId,
            variant: tileDef is ContentTileDefinition contentTile
                ? _tile.PickVariant(contentTile, random)
                : (byte) 0);
    }

    private static Box2i GetResidenceRelativeBounds(
        PrisonPlanetPrototype planet,
        PrisonResidenceDefinition residence)
    {
        var fallbackSize = Math.Max(1, planet.ResidenceReservationSize);
        var width = residence.ReservationSize.X > 0 ? residence.ReservationSize.X : fallbackSize;
        var height = residence.ReservationSize.Y > 0 ? residence.ReservationSize.Y : fallbackSize;
        var minX = residence.ReservationOffset.X - width / 2;
        var minY = residence.ReservationOffset.Y - height / 2;
        return new Box2i(minX, minY, minX + width, minY + height);
    }

    private static Box2i TranslateBounds(Box2i bounds, Vector2i offset)
    {
        return new Box2i(
            bounds.Left + offset.X,
            bounds.Bottom + offset.Y,
            bounds.Right + offset.X,
            bounds.Top + offset.Y);
    }

    private static Box2 ToBox2(Box2i bounds)
    {
        return new Box2(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
    }

    private static int GetDistanceToSquareEdge(int x, int y, int halfSize)
    {
        var left = x + halfSize;
        var right = halfSize - 1 - x;
        var bottom = y + halfSize;
        var top = halfSize - 1 - y;

        return Math.Min(Math.Min(left, right), Math.Min(bottom, top));
    }

    private static int GetSquareRingTileCount(int halfSize, int ringWidth)
    {
        if (ringWidth <= 0 || halfSize <= 0)
            return 0;

        var size = halfSize * 2;
        var innerSize = Math.Max(0, size - ringWidth * 2);
        return size * size - innerSize * innerSize;
    }

    private readonly record struct ResidencePlacement(
        PrisonResidenceDefinition Definition,
        Vector2 Offset,
        Box2i ReservationBounds);
}
