// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Loadout;

/// <summary>
/// Frame of the character preview. In the "Dead Space" style it is a projection grid with corner brackets, otherwise a
/// plain inset.
/// </summary>
public sealed class LoadoutPreviewFrame : PanelContainer
{
    private const float GridStep = 16f;

    private readonly LoadoutStyleBox _box = new(LoadoutBoxKind.Preview, 4);
    private readonly HologramInterference _interference = new();

    public LoadoutPreviewFrame()
    {
        PanelOverride = _box;
        AddChild(_interference);
    }

    /// <summary>The interference stays over the preview, whatever is added after it.</summary>
    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);
        if (newChild != _interference)
            _interference.SetPositionLast();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (!DeadSpaceStylePalette.Hologram)
            return;

        var size = PixelSize;
        var step = GridStep * UIScale;
        var color = DeadSpaceStylePalette.BorderDark.WithAlpha(0.45f);

        for (var x = step; x < size.X - 1; x += step)
        {
            handle.DrawLine(new Vector2(x, 1), new Vector2(x, size.Y - 1), color);
        }

        for (var y = step; y < size.Y - 1; y += step)
        {
            handle.DrawLine(new Vector2(1, y), new Vector2(size.X - 1, y), color);
        }
    }
}
