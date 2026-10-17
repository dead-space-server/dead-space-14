using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.PdaPainter.UI;

public enum PdaPainterTool
{
    Brush,
    Eraser,
    Pipette,
}

public sealed class PdaPainterCanvas : Control
{
    public const int CanvasSize = 32;
    public const int PixelScale = 8;

    public Dictionary<int, int> Pixels = new();

    public Texture? Backdrop;

    public PdaPainterTool Tool = PdaPainterTool.Brush;
    public int BrushSize = 1;

    public Func<Color>? ColorProvider;

    public Func<int, Color?>? PixelSampler;

    public event Action<List<int>, List<int>>? PixelsChanged;

    private bool _painting;
    private readonly List<int> _batchIndices = new();
    private readonly List<int> _batchColors = new();
    private int _lastIndex = -1;

    private static readonly Color CheckerA = Color.FromHex("#4a4a4a");
    private static readonly Color CheckerB = Color.FromHex("#3c3c3c");

    public PdaPainterCanvas()
    {
        MinSize = new Vector2(CanvasSize * PixelScale, CanvasSize * PixelScale);
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = CanvasSize * PixelScale;

        for (var y = 0; y < CanvasSize; y += 2)
        {
            for (var x = 0; x < CanvasSize; x += 2)
            {
                handle.DrawRect(new UIBox2(x * PixelScale, y * PixelScale, (x + 1) * PixelScale, (y + 1) * PixelScale), CheckerA);
                handle.DrawRect(new UIBox2((x + 1) * PixelScale, y * PixelScale, (x + 2) * PixelScale, (y + 1) * PixelScale), CheckerB);
                handle.DrawRect(new UIBox2(x * PixelScale, (y + 1) * PixelScale, (x + 1) * PixelScale, (y + 2) * PixelScale), CheckerB);
                handle.DrawRect(new UIBox2((x + 1) * PixelScale, (y + 1) * PixelScale, (x + 2) * PixelScale, (y + 2) * PixelScale), CheckerA);
            }
        }

        if (Backdrop != null)
        {
            var box = new UIBox2(0, 0, size, size);
            var region = new UIBox2i(0, 0, Backdrop.Size.X, Backdrop.Size.Y);
            handle.DrawTextureRectRegion(Backdrop, box, region);
        }

        foreach (var (index, packed) in Pixels)
        {
            var x = index % CanvasSize;
            var y = index / CanvasSize;
            handle.DrawRect(
                new UIBox2(x * PixelScale, y * PixelScale, (x + 1) * PixelScale, (y + 1) * PixelScale),
                UnpackColor(packed));
        }

        handle.DrawRect(new UIBox2(0, 0, size, 1), Color.Black);
        handle.DrawRect(new UIBox2(0, size - 1, size, size), Color.Black);
        handle.DrawRect(new UIBox2(0, 0, 1, size), Color.Black);
        handle.DrawRect(new UIBox2(size - 1, 0, size, size), Color.Black);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.Use)
            return;

        var index = ScreenToPixel(args.RelativePosition);
        if (index == -1)
            return;

        switch (Tool)
        {
            case PdaPainterTool.Brush:
            case PdaPainterTool.Eraser:
                _painting = true;
                _batchIndices.Clear();
                _batchColors.Clear();
                _lastIndex = index;
                Paint(index);
                break;
            case PdaPainterTool.Pipette:
                if (Pixels.TryGetValue(index, out var picked))
                {
                    PickedColor?.Invoke(UnpackColor(picked));
                }
                else if (PixelSampler?.Invoke(index) is { } sampled)
                {
                    PickedColor?.Invoke(sampled);
                }
                break;
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.Use || !_painting)
            return;

        _painting = false;
        if (_batchIndices.Count > 0)
            PixelsChanged?.Invoke(new List<int>(_batchIndices), new List<int>(_batchColors));

        _batchIndices.Clear();
        _batchColors.Clear();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_painting)
            return;

        var index = ScreenToPixel(args.RelativePosition);
        if (index == -1 || index == _lastIndex)
            return;

        DrawLine(_lastIndex, index);
        _lastIndex = index;
    }

    public event Action<Color>? PickedColor;

    private int ScreenToPixel(Vector2 relative)
    {
        var pos = relative;
        if (pos.X < 0 || pos.Y < 0 || pos.X >= CanvasSize * PixelScale || pos.Y >= CanvasSize * PixelScale)
            return -1;

        return (int)(pos.Y / PixelScale) * CanvasSize + (int)(pos.X / PixelScale);
    }

    private void DrawLine(int from, int to)
    {
        var x0 = from % CanvasSize;
        var y0 = from / CanvasSize;
        var x1 = to % CanvasSize;
        var y1 = to / CanvasSize;

        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            Paint(y0 * CanvasSize + x0);

            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void Paint(int index)
    {
        var color = Tool == PdaPainterTool.Eraser ? 0 : PackColor(ColorProvider?.Invoke() ?? Color.White);
        var half = BrushSize / 2;

        var cx = index % CanvasSize;
        var cy = index / CanvasSize;

        for (var dy = 0; dy < BrushSize; dy++)
        {
            for (var dx = 0; dx < BrushSize; dx++)
            {
                var x = cx + dx - half;
                var y = cy + dy - half;
                if (x < 0 || y < 0 || x >= CanvasSize || y >= CanvasSize)
                    continue;

                var i = y * CanvasSize + x;
                if (color == 0)
                {
                    if (Pixels.Remove(i))
                    {
                        _batchIndices.Add(i);
                        _batchColors.Add(0);
                    }
                }
                else
                {
                    Pixels[i] = color;
                    _batchIndices.Add(i);
                    _batchColors.Add(color);
                }
            }
        }

        UpdateDraw();
    }

    public static int PackColor(Color color)
    {
        var r = (int)(color.RByte);
        var g = (int)(color.GByte);
        var b = (int)(color.BByte);
        var a = (int)(color.AByte);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    public static Color UnpackColor(int packed)
    {
        var r = (packed >> 16) & 0xFF;
        var g = (packed >> 8) & 0xFF;
        var b = packed & 0xFF;
        var a = (packed >> 24) & 0xFF;
        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
