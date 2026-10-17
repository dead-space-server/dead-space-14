// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.DeadSpace.Tiles;

public sealed class InvisibleTileOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;
    private List<Entity<MapGridComponent>> _grids = new();

    private static readonly Color EdgeColor = new(1f, 0.31f, 0.85f, 0.9f);
    private const float Thickness = 0.08f;

    public ushort TileTypeId;
    public bool Enabled;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public InvisibleTileOverlay()
    {
        IoCManager.InjectDependencies(this);
        _map = _entities.System<SharedMapSystem>();
        _transform = _entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!Enabled || TileTypeId == 0)
            return;

        var handle = args.WorldHandle;
        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId, args.WorldBounds, ref _grids);

        foreach (var grid in _grids)
        {
            handle.SetTransform(_transform.GetWorldMatrix(grid));

            foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, args.WorldBounds))
            {
                if (tile.Tile.TypeId != TileTypeId)
                    continue;

                DrawEdges(handle, grid, tile.GridIndices);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawEdges(DrawingHandleWorld handle, Entity<MapGridComponent> grid, Vector2i index)
    {
        var origin = new Vector2(index.X, index.Y);

        if (!IsSame(grid, new Vector2i(index.X, index.Y - 1)))
            handle.DrawRect(Box2.FromDimensions(origin, new Vector2(1f, Thickness)), EdgeColor);

        if (!IsSame(grid, new Vector2i(index.X, index.Y + 1)))
            handle.DrawRect(Box2.FromDimensions(origin + new Vector2(0f, 1f - Thickness), new Vector2(1f, Thickness)), EdgeColor);

        if (!IsSame(grid, new Vector2i(index.X - 1, index.Y)))
            handle.DrawRect(Box2.FromDimensions(origin, new Vector2(Thickness, 1f)), EdgeColor);

        if (!IsSame(grid, new Vector2i(index.X + 1, index.Y)))
            handle.DrawRect(Box2.FromDimensions(origin + new Vector2(1f - Thickness, 0f), new Vector2(Thickness, 1f)), EdgeColor);
    }

    private bool IsSame(Entity<MapGridComponent> grid, Vector2i indices)
    {
        return _map.TryGetTileRef(grid.Owner, grid.Comp, indices, out var tile) && tile.Tile.TypeId == TileTypeId;
    }
}
