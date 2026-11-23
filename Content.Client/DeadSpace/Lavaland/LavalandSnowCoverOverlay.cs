// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Blizzard on lavaland: snow cover creeps in from the map borders, whitening floor, rocks and flora under it.
/// Only the planet's own open land is covered: not the outpost, a shelter or any other grid on it, nor anything
/// under a roof or on an indoor floor - where the storm does not reach, there is no snow.
/// </summary>
public sealed partial class LavalandSnowCoverOverlay : Overlay
{
    private const int Strips = 12;

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly ITileDefinitionManager _tileDefinitions;
    private readonly SharedMapSystem _map;
    private readonly SharedRoofSystem _roof;

    private List<Entity<MapGridComponent>> _otherGrids = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public LavalandSnowCoverOverlay(IEntityManager entities, IGameTiming timing, ITileDefinitionManager tileDefinitions)
    {
        _entities = entities;
        _timing = timing;
        _tileDefinitions = tileDefinitions;
        _map = entities.System<SharedMapSystem>();
        _roof = entities.System<SharedRoofSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.MapUid.IsValid() && _entities.HasComponent<LavalandSnowCoverComponent>(args.MapUid);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entities.TryGetComponent<LavalandSnowCoverComponent>(args.MapUid, out var cover) ||
            !_entities.TryGetComponent<MapGridComponent>(args.MapUid, out var planet))
        {
            return;
        }

        var progress = Math.Clamp((float) (_timing.CurTime - cover.Start).TotalSeconds / MathF.Max(1f, cover.GrowSeconds), 0f, 1f);
        var depth = cover.Depth * progress;
        if (depth <= 0.5f)
            return;

        var handle = args.WorldHandle;
        var half = cover.HalfSize;
        var strip = depth / Strips;
        _entities.TryGetComponent<RoofComponent>(args.MapUid, out var roof);

        _otherGrids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _otherGrids);
        var planetUid = args.MapUid;
        _otherGrids.RemoveAll(grid => grid.Owner == planetUid);

        // Thick at the border, fading inward - and in drifts over the whole planet, which spread as the blizzard
        // goes on. Tile by tile, only where the storm reaches.
        foreach (var tile in _map.GetTilesIntersecting(args.MapUid, planet, args.WorldAABB))
        {
            var center = new Vector2(tile.GridIndices.X + 0.5f, tile.GridIndices.Y + 0.5f);
            var fromBorder = half - MathF.Max(MathF.Abs(center.X), MathF.Abs(center.Y));
            if (fromBorder < 0f)
                continue;

            var alpha = 0f;
            if (fromBorder <= depth)
            {
                var index = Math.Min(Strips - 1, (int) (fromBorder / strip));
                alpha = 0.55f * (1f - index / (float) Strips) + 0.05f;
            }

            // A drift: where the noise is above the line that sinks as the blizzard grows.
            var drift = DriftNoise(tile.GridIndices, cover.Start.Ticks) - (1f - DriftCoverage * progress);
            if (drift > 0f)
                alpha = MathF.Max(alpha, MathF.Min(0.5f, 0.15f + drift * 2.5f));

            if (alpha <= 0f || !IsOpenLand(args.MapUid, planet, roof, tile, center))
                continue;

            handle.DrawRect(Box2.FromDimensions(tile.GridIndices, Vector2.One), cover.Color.WithAlpha(alpha * progress));
        }

        // Everything beyond the map border is fully snowed in.
        var far = half + 200f;
        var full = cover.Color.WithAlpha(0.6f * progress);
        handle.DrawRect(new Box2(-far, half, far, far), full);
        handle.DrawRect(new Box2(-far, -far, far, -half), full);
        handle.DrawRect(new Box2(half, -half, far, half), full);
        handle.DrawRect(new Box2(-far, -half, -half, half), full);
    }

    /// <summary>At the full blizzard, about this share of the open land lies under drifts.</summary>
    private const float DriftCoverage = 0.4f;

    /// <summary>Size (tiles) of the cells the drifts are laid out on: one drift spans a few of them.</summary>
    private const int DriftCell = 9;

    /// <summary>
    /// Smooth value noise in 0..1 over the tiles: blobs some cells across rather than salt and pepper. Seeded by the
    /// blizzard, so each one lays its drifts elsewhere, and the same for every player.
    /// </summary>
    private static float DriftNoise(Vector2i tile, long seed)
    {
        var x = tile.X / (float) DriftCell;
        var y = tile.Y / (float) DriftCell;
        var x0 = (int) MathF.Floor(x);
        var y0 = (int) MathF.Floor(y);
        var fx = Smooth(x - x0);
        var fy = Smooth(y - y0);

        var top = Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), fx);
        var bottom = Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), fx);
        return Lerp(top, bottom, fy);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Hash(int x, int y, long seed)
    {
        var h = unchecked((uint) (x * 374761393 + y * 668265263) ^ (uint) seed ^ (uint) (seed >> 32));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    private bool IsOpenLand(EntityUid planetUid, MapGridComponent planet, RoofComponent? roof, TileRef tile, Vector2 center)
    {
        if (roof != null && _roof.IsRooved((planetUid, planet, roof), tile.GridIndices))
            return false;

        if (_tileDefinitions[tile.Tile.TypeId] is ContentTileDefinition { Weather: false })
            return false;

        // The outpost, a shelter, a ruin: a grid of its own over the land keeps the snow off its floor.
        foreach (var other in _otherGrids)
        {
            if (_map.TryGetTileRef(other.Owner, other.Comp, _map.WorldToTile(other.Owner, other.Comp, center), out var otherTile) &&
                !otherTile.Tile.IsEmpty)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed partial class LavalandSnowCoverSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    private LavalandSnowCoverOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LavalandSnowCoverOverlay(EntityManager, _timing, _tileDefinitions);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}
