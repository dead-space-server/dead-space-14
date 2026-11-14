using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes.Layers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Shared.Parallax.Biomes;

public abstract class SharedBiomeSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;
    [Dependency] protected readonly ITileDefinitionManager TileDefManager = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    protected const byte ChunkSize = 8;

    // DS-14 start
    private readonly Dictionary<NoiseKey, (FastNoiseLite Template, FastNoiseLite Copy)> _noiseCache = new();

    private readonly struct NoiseKey : IEquatable<NoiseKey>
    {
        public readonly int Id;
        public readonly int Seed;

        public NoiseKey(FastNoiseLite template, int seed)
        {
            Id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(template);
            Seed = seed;
        }

        public bool Equals(NoiseKey other)
        {
            return Id == other.Id && Seed == other.Seed;
        }

        public override bool Equals(object? obj)
        {
            return obj is NoiseKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Seed);
        }
    }

    protected void ClearBiomeNoiseCache()
    {
        _noiseCache.Clear();
        _spacedCells.Clear();
    }

    private const int SpacedCellMinDistance = 25;
    private readonly Dictionary<SpacedKey, SpacedCell> _spacedCells = new();

    private readonly record struct SpacedKey(int Layer, int Seed, int Index, Vector2i Cell);

    private readonly record struct SpacedCell(bool Found, Vector2i Tile, float Value);

    private bool IsSpacedWinner(BiomeEntityLayer layer, FastNoiseLite noise, int index, int seed, Vector2i indices)
    {
        var size = layer.MinimumDistance;
        var cell = new Vector2i(FloorDiv(indices.X, size), FloorDiv(indices.Y, size));
        var own = GetSpacedCell(layer, noise, index, seed, cell);
        if (!own.Found || own.Tile != indices)
            return false;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var other = GetSpacedCell(layer, noise, index, seed, cell + new Vector2i(x, y));
                if (!other.Found ||
                    Math.Max(Math.Abs(other.Tile.X - indices.X), Math.Abs(other.Tile.Y - indices.Y)) > size)
                {
                    continue;
                }

                if (other.Value > own.Value)
                    return false;
            }
        }

        return true;
    }

    private SpacedCell GetSpacedCell(BiomeEntityLayer layer, FastNoiseLite noise, int index, int seed, Vector2i cell)
    {
        var key = new SpacedKey(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(layer), seed, index, cell);
        if (_spacedCells.TryGetValue(key, out var cached))
            return cached;

        var size = layer.MinimumDistance;
        var found = false;
        var best = default(Vector2i);
        var bestValue = float.MinValue;
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                var tileX = cell.X * size + x;
                var tileY = cell.Y * size + y;
                var value = noise.GetNoise(tileX, tileY);
                value = layer.Invert ? value * -1 : value;
                if (value < layer.Threshold)
                    continue;

                var strength = noise.GetNoise(tileX, tileY, index);
                if (found && strength <= bestValue)
                    continue;

                found = true;
                best = new Vector2i(tileX, tileY);
                bestValue = strength;
            }
        }

        cached = new SpacedCell(found, best, bestValue);
        _spacedCells[key] = cached;
        return cached;
    }

    private static int FloorDiv(int value, int size)
    {
        return value >= 0 ? value / size : (value - size + 1) / size;
    }
    // DS-14 end

    public void SetBounds(EntityUid uid, BiomeComponent component, Box2i? bounds, bool dirty = true)
    {
        component.Bounds = bounds;

        if (dirty)
            Dirty(uid, component);
    }

    private T Pick<T>(List<T> collection, float value)
    {
        // Listen I don't need this exact and I'm too lazy to finetune just for random ent picking.
        value %= 1f;
        value = Math.Clamp(value, 0f, 1f);

        if (collection.Count == 1)
            return collection[0];

        var randValue = value * collection.Count;

        foreach (var item in collection)
        {
            randValue -= 1f;

            if (randValue <= 0f)
            {
                return item;
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    private int Pick(int count, float value)
    {
        value %= 1f;
        value = Math.Clamp(value, 0f, 1f);

        if (count == 1)
            return 0;

        value *= count;

        for (var i = 0; i < count; i++)
        {
            value -= 1f;

            if (value <= 0f)
            {
                return i;
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    public bool TryGetBiomeTile(EntityUid uid, MapGridComponent grid, Vector2i indices, [NotNullWhen(true)] out Tile? tile)
    {
        if (_map.TryGetTileRef(uid, grid, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tile = tileRef.Tile;
            return true;
        }

        if (!TryComp<BiomeComponent>(uid, out var biome))
        {
            tile = null;
            return false;
        }

        return TryGetBiomeTile(indices, biome.Layers, biome.Seed, (uid, grid), out tile);
    }

    /// <summary>
    /// Tries to get the tile, real or otherwise, for the specified indices.
    /// </summary>
    public bool TryGetBiomeTile(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        if (grid is { } gridEnt && _map.TryGetTileRef(gridEnt, gridEnt.Comp, indices, out var tileRef) && !tileRef.Tile.IsEmpty)
        {
            tile = tileRef.Tile;
            return true;
        }

        return TryGetTile(indices, layers, seed, grid, out tile);
    }

    /// <summary>
    /// Tries to get the tile, real or otherwise, for the specified indices.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetBiomeTile(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetBiomeTile(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out tile);
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    public bool TryGetTile(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid, [NotNullWhen(true)] out Tile? tile)
    {
        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];
            var noiseCopy = GetNoise(layer.Noise, seed);

            var invert = layer.Invert;
            var value = noiseCopy.GetNoise(indices.X, indices.Y);
            value = invert ? value * -1 : value;

            if (value < layer.Threshold)
                continue;

            // Check if the tile is from meta layer, otherwise fall back to default layers.
            if (layer is BiomeMetaLayer meta)
            {
                if (TryGetBiomeTile(indices, ProtoManager.Index<BiomeTemplatePrototype>(meta.Template).Layers, seed, grid, out tile))
                {
                    return true;
                }

                continue;
            }

            if (layer is not BiomeTileLayer tileLayer)
                continue;

            if (TryGetTile(indices, noiseCopy, tileLayer.Invert, tileLayer.Threshold, ProtoManager.Index(tileLayer.Tile), tileLayer.Variants, out tile))
            {
                return true;
            }
        }

        tile = null;
        return false;
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetTile(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent? grid, [NotNullWhen(true)] out Tile? tile)
    {
        return TryGetTile(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out tile);
    }

    /// <summary>
    /// Gets the underlying biome tile, ignoring any existing tile that may be there.
    /// </summary>
    private bool TryGetTile(Vector2i indices, FastNoiseLite noise, bool invert, float threshold, ContentTileDefinition tileDef, List<byte>? variants, [NotNullWhen(true)] out Tile? tile)
    {
        var found = noise.GetNoise(indices.X, indices.Y);
        found = invert ? found * -1 : found;

        if (found < threshold)
        {
            tile = null;
            return false;
        }

        byte variant = 0;
        var variantCount = variants?.Count ?? tileDef.Variants;

        // Pick a variant tile if they're available as well
        if (variantCount > 1)
        {
            var variantValue = (noise.GetNoise(indices.X * 8, indices.Y * 8, variantCount) + 1f) * 100;
            variant = _tile.PickVariant(tileDef, (int)variantValue);
        }

        tile = new Tile(tileDef.TileId, variant);
        return true;
    }

    /// <summary>
    /// Tries to get the relevant entity for this tile.
    /// </summary>
    public bool TryGetEntity(Vector2i indices, BiomeComponent component, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out string? entity)
    {
        if (!TryGetBiomeTile(indices, component.Layers, component.Seed, grid, out var tile))
        {
            entity = null;
            return false;
        }

        return TryGetEntity(indices, component.Layers, tile.Value, component.Seed, grid, out entity);
    }

    /// <summary>
    /// Tries to get the relevant entity for this tile.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetEntity(Vector2i indices, BiomeComponent component, MapGridComponent grid,
        [NotNullWhen(true)] out string? entity)
    {
        return TryGetEntity(indices, component, grid == null ? null : (grid.Owner, grid), out entity);
    }

    public bool TryGetEntity(Vector2i indices, List<IBiomeLayer> layers, Tile tileRef, int seed, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out string? entity)
    {
        var tileId = TileDefManager[tileRef.TypeId].ID;

        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];

            switch (layer)
            {
                case BiomeDummyLayer:
                    continue;
                case IBiomeWorldLayer worldLayer:
                    if (!worldLayer.AllowedTiles.Contains(tileId))
                        continue;

                    break;
                case BiomeMetaLayer:
                    break;
                default:
                    continue;
            }

            var noiseCopy = GetNoise(layer.Noise, seed);

            var invert = layer.Invert;
            var value = noiseCopy.GetNoise(indices.X, indices.Y);
            value = invert ? value * -1 : value;

            if (value < layer.Threshold)
                continue;

            if (layer is BiomeMetaLayer meta)
            {
                if (TryGetEntity(indices, ProtoManager.Index<BiomeTemplatePrototype>(meta.Template).Layers, tileRef, seed, grid, out entity))
                {
                    return true;
                }

                continue;
            }

            // Decals might block entity so need to check if there's one in front of us.
            if (layer is not BiomeEntityLayer biomeLayer)
            {
                entity = null;
                return false;
            }

            var noiseValue = noiseCopy.GetNoise(indices.X, indices.Y, i);
            // DS14-start
            // Keep only the strongest candidate in the configured neighborhood. This gives
            // sparse deterministic placement without clusters or chunk-boundary seams.
            if (biomeLayer.MinimumDistance >= SpacedCellMinDistance)
            {
                if (!IsSpacedWinner(biomeLayer, noiseCopy, i, seed, indices))
                {
                    entity = null;
                    return false;
                }
            }
            else if (biomeLayer.MinimumDistance > 0)
            {
                for (var x = -biomeLayer.MinimumDistance; x <= biomeLayer.MinimumDistance; x++)
                {
                    for (var y = -biomeLayer.MinimumDistance; y <= biomeLayer.MinimumDistance; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        var neighborX = indices.X + x;
                        var neighborY = indices.Y + y;
                        var neighborBase = noiseCopy.GetNoise(neighborX, neighborY);
                        neighborBase = invert ? neighborBase * -1 : neighborBase;
                        if (neighborBase < layer.Threshold)
                            continue;

                        if (noiseCopy.GetNoise(neighborX, neighborY, i) > noiseValue)
                        {
                            entity = null;
                            return false;
                        }
                    }
                }
            }
            // DS14-end
            entity = Pick(biomeLayer.Entities, (noiseValue + 1f) / 2f);
            return true;
        }

        entity = null;
        return false;
    }

    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetEntity(Vector2i indices, List<IBiomeLayer> layers, Tile tileRef, int seed, MapGridComponent grid,
        [NotNullWhen(true)] out string? entity)
    {
        return TryGetEntity(indices, layers, tileRef, seed, grid == null ? null : (grid.Owner, grid), out entity);
    }

    /// <summary>
    /// Tries to get the relevant decals for this tile.
    /// </summary>
    public bool TryGetDecals(Vector2i indices, List<IBiomeLayer> layers, int seed, Entity<MapGridComponent>? grid,
        [NotNullWhen(true)] out List<(string ID, Vector2 Position, Color Color)>? decals) // DS14
    {
        if (!TryGetBiomeTile(indices, layers, seed, grid, out var tileRef))
        {
            decals = null;
            return false;
        }

        var tileId = TileDefManager[tileRef.Value.TypeId].ID;

        for (var i = layers.Count - 1; i >= 0; i--)
        {
            var layer = layers[i];

            // Entities might block decal so need to check if there's one in front of us.
            switch (layer)
            {
                case BiomeDummyLayer:
                    continue;
                case IBiomeWorldLayer worldLayer:
                    if (!worldLayer.AllowedTiles.Contains(tileId))
                        continue;

                    break;
                case BiomeMetaLayer:
                    break;
                default:
                    continue;
            }

            var invert = layer.Invert;
            var noiseCopy = GetNoise(layer.Noise, seed);
            var value = noiseCopy.GetNoise(indices.X, indices.Y);
            value = invert ? value * -1 : value;

            if (value < layer.Threshold)
                continue;

            if (layer is BiomeMetaLayer meta)
            {
                if (TryGetDecals(indices, ProtoManager.Index<BiomeTemplatePrototype>(meta.Template).Layers, seed, grid, out decals))
                {
                    return true;
                }

                continue;
            }

            // Check if the other layer should even render, if not then keep going.
            if (layer is not BiomeDecalLayer decalLayer)
            {
                decals = null;
                return false;
            }

            decals = new List<(string ID, Vector2 Position, Color Color)>(); // DS14

            for (var x = 0; x < decalLayer.Divisions; x++)
            {
                for (var y = 0; y < decalLayer.Divisions; y++)
                {
                    var index = new Vector2(indices.X + x * 1f / decalLayer.Divisions, indices.Y + y * 1f / decalLayer.Divisions);
                    var decalValue = noiseCopy.GetNoise(index.X, index.Y);
                    decalValue = invert ? decalValue * -1 : decalValue;

                    if (decalValue < decalLayer.Threshold)
                        continue;

                    decals.Add((Pick(decalLayer.Decals, (noiseCopy.GetNoise(indices.X, indices.Y, x + y * decalLayer.Divisions) + 1f) / 2f), index, decalLayer.Color)); // DS14
                }
            }

            // Check other layers
            if (decals.Count == 0)
                continue;

            return true;
        }

        decals = null;
        return false;
    }

    /// <summary>
    /// Tries to get the relevant decals for this tile.
    /// </summary>
    [Obsolete("Use the Entity<MapGridComponent>? overload")]
    public bool TryGetDecals(Vector2i indices, List<IBiomeLayer> layers, int seed, MapGridComponent grid,
        [NotNullWhen(true)] out List<(string ID, Vector2 Position, Color Color)>? decals) // DS14
    {
        return TryGetDecals(indices, layers, seed, grid == null ? null : (grid.Owner, grid), out decals);
    }

    private FastNoiseLite GetNoise(FastNoiseLite seedNoise, int seed)
    {
        // DS-14 start
        var key = new NoiseKey(seedNoise, seed);
        if (_noiseCache.TryGetValue(key, out var cached) && ReferenceEquals(cached.Template, seedNoise))
            return cached.Copy;
        // DS-14 end

        var noiseCopy = new FastNoiseLite();
        _serManager.CopyTo(seedNoise, ref noiseCopy, notNullableOverride: true);
        noiseCopy.SetSeed(noiseCopy.GetSeed() + seed);
        // Ensure re-calculate is run.
        noiseCopy.SetFractalOctaves(noiseCopy.GetFractalOctaves());
        _noiseCache[key] = (seedNoise, noiseCopy); //DS-14
        return noiseCopy;
    }
}
