// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client.DeadSpace.BSAConsole;

public sealed class CrosshairOverlay : Control
{
    private MapCoordinates? _worldCoords;
    private Func<MapCoordinates, Vector2?>? _toPixel;

    public void SetWorldPosition(MapCoordinates? coords, Func<MapCoordinates, Vector2?>? converter)
    {
        _worldCoords = coords;
        _toPixel = converter;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_worldCoords == null || _toPixel == null)
            return;

        var pos = _toPixel(_worldCoords.Value);
        if (pos == null)
            return;

        var p = pos.Value;
        const float size = 8f;
        const float gap = 3f;
        const float thickness = 2f;
        var color = new Color(1f, 0.2f, 0.2f, 0.9f);

        handle.DrawRect(
            new UIBox2(p.X - size, p.Y - thickness / 2, p.X - gap, p.Y + thickness / 2),
            color);
        handle.DrawRect(
            new UIBox2(p.X + gap, p.Y - thickness / 2, p.X + size, p.Y + thickness / 2),
            color);

        handle.DrawRect(
            new UIBox2(p.X - thickness / 2, p.Y - size, p.X + thickness / 2, p.Y - gap),
            color);
        handle.DrawRect(
            new UIBox2(p.X - thickness / 2, p.Y + gap, p.X + thickness / 2, p.Y + size),
            color);

        handle.DrawRect(
            new UIBox2(p.X - 1f, p.Y - 1f, p.X + 1f, p.Y + 1f),
            color);
    }
}
