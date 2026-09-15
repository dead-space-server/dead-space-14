// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Pinpointer.UI;
using Content.Client.Shuttles.UI;
using Content.Shared.DeadSpace.BSAConsole;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.DeadSpace.BSAConsole;

public sealed partial class BSANavMapControl : BaseShuttleControl
{
    public Action<MapCoordinates>? OnMapClick;

    private const float DetailedGridMinSize = 12f;

    private readonly SharedTransformSystem _transform;
    private BSARadarState? _state;

    protected override bool Draggable => true;

    public BSANavMapControl() : base(16f, 512f, 256f)
    {
        _transform = EntManager.System<SharedTransformSystem>();
    }

    public void UpdateState(BSARadarState? state)
    {
        if (_state?.MapId != state?.MapId)
        {
            Offset = Vector2.Zero;
            TargetOffset = Vector2.Zero;
        }

        _state = state;
    }

    public Vector2? WorldToScreen(MapCoordinates coordinates)
    {
        if (_state == null || (int) coordinates.MapId != _state.MapId)
            return null;

        return WorldToScreen(coordinates.Position);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (_state == null || args.Function != EngineKeyFunctions.UIClick || OnMapClick == null)
            return;

        var localPosition = args.PointerLocation.Position - GlobalPixelPosition;
        var relativePosition = InverseMapPosition(localPosition);
        var mapPosition = new MapCoordinates(
            _state.Center + relativePosition,
            new MapId(_state.MapId));

        OnMapClick.Invoke(mapPosition);
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        DrawBacking(handle);

        if (_state == null)
        {
            DrawNoSignal(handle);
            return;
        }

        DrawCircles(handle);

        var viewCenter = _state.Center + Offset;
        var worldToView = Matrix3x2.CreateTranslation(-viewCenter) *
            Matrix3x2.CreateScale(MinimapScale, -MinimapScale) *
            Matrix3x2.CreateTranslation(MidPointVector);

        foreach (var grid in _state.Grids)
        {
            if (!IsVisible(grid, viewCenter))
                continue;

            var color = grid.Selected || ContainsPoint(grid, _state.Center)
                ? Color.MediumSpringGreen
                : Color.Gold;

            var projectedSize = MathF.Max(grid.HalfExtents.X, grid.HalfExtents.Y) * 2f * MinimapScale;
            if (projectedSize >= DetailedGridMinSize &&
                EntManager.TryGetEntity(grid.GridUid, out var gridUid) &&
                EntManager.TryGetComponent(gridUid, out MapGridComponent? mapGrid) &&
                mapGrid.ChunkCount > 0 &&
                EntManager.TryGetComponent(gridUid, out TransformComponent? xform) &&
                (int) xform.MapID == _state.MapId)
            {
                var gridToView = _transform.GetWorldMatrix(gridUid.Value) * worldToView;
                DrawGrid(handle, gridToView, (gridUid.Value, mapGrid), color);
            }
            else
            {
                DrawGridBounds(handle, grid, color);
            }

            var center = WorldToScreen(grid.Center);
            handle.DrawRect(new UIBox2(center.X - 2f, center.Y - 2f, center.X + 2f, center.Y + 2f), color);
        }
    }

    private bool IsVisible(BSARadarGridState grid, Vector2 viewCenter)
    {
        var cos = MathF.Abs(MathF.Cos(grid.Rotation));
        var sin = MathF.Abs(MathF.Sin(grid.Rotation));
        var worldHalfExtents = new Vector2(
            cos * grid.HalfExtents.X + sin * grid.HalfExtents.Y,
            sin * grid.HalfExtents.X + cos * grid.HalfExtents.Y);
        var relative = grid.Center - viewCenter;

        return MathF.Abs(relative.X) <= WorldRange + worldHalfExtents.X &&
            MathF.Abs(relative.Y) <= WorldRange + worldHalfExtents.Y;
    }

    private static bool ContainsPoint(BSARadarGridState grid, Vector2 point)
    {
        var local = new Angle(-grid.Rotation).RotateVec(point - grid.Center);
        return MathF.Abs(local.X) <= grid.HalfExtents.X &&
            MathF.Abs(local.Y) <= grid.HalfExtents.Y;
    }

    private void DrawGridBounds(DrawingHandleScreen handle, BSARadarGridState grid, Color color)
    {
        var rotation = new Angle(grid.Rotation);
        var half = grid.HalfExtents;
        var screenCorners = new[]
        {
            WorldToScreen(grid.Center + rotation.RotateVec(new Vector2(-half.X, -half.Y))),
            WorldToScreen(grid.Center + rotation.RotateVec(new Vector2(half.X, -half.Y))),
            WorldToScreen(grid.Center + rotation.RotateVec(new Vector2(half.X, half.Y))),
            WorldToScreen(grid.Center + rotation.RotateVec(new Vector2(-half.X, half.Y))),
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, screenCorners, color.WithAlpha(0.08f));

        for (var i = 0; i < screenCorners.Length; i++)
            handle.DrawLine(screenCorners[i], screenCorners[(i + 1) % screenCorners.Length], color);
    }

    private Vector2 WorldToScreen(Vector2 worldPosition)
    {
        var relative = worldPosition - _state!.Center - Offset;
        return ScalePosition(new Vector2(relative.X, -relative.Y));
    }
}

public sealed class BSAGridNavMapControl : NavMapControl
{
    public Action<MapCoordinates>? OnMapClick;

    private readonly SharedTransformSystem _transform;

    public BSAGridNavMapControl()
    {
        _transform = EntManager.System<SharedTransformSystem>();
    }

    public Vector2? WorldToScreen(MapCoordinates coordinates)
    {
        if (MapUid is not { } gridUid ||
            !EntManager.TryGetComponent(gridUid, out TransformComponent? xform) ||
            coordinates.MapId != xform.MapID)
        {
            return null;
        }

        var localPosition = Vector2.Transform(coordinates.Position, _transform.GetInvWorldMatrix(xform));
        var relativePosition = localPosition - GetOffset();
        return ScalePosition(new Vector2(relativePosition.X, -relativePosition.Y));
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (MapUid is not { } gridUid || args.Function != EngineKeyFunctions.UIClick || OnMapClick == null)
            return;

        var controlPosition = args.PointerLocation.Position - GlobalPixelPosition;
        var gridPosition = InverseMapPosition(controlPosition) + GetOffset() - Offset;
        var mapPosition = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, gridPosition));

        if (mapPosition.MapId == MapId.Nullspace)
            return;

        OnMapClick.Invoke(mapPosition);
        args.Handle();
    }
}
