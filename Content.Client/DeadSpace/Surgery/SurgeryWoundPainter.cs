// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Surgery;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.DeadSpace.Surgery;

public static class SurgeryWoundPainter
{
    public const int Size = 32;

    private readonly record struct Palette(Rgba32 Cut, Rgba32 Tip, Rgba32 Gape, Rgba32 Lip, Rgba32 Rim, Rgba32 Pit, bool Dotted);

    private static readonly Palette Flesh = new(
        new Rgba32(92, 11, 14, 255),
        new Rgba32(150, 34, 38, 255),
        new Rgba32(52, 4, 6, 255),
        new Rgba32(170, 52, 60, 255),
        new Rgba32(214, 120, 124, 255),
        new Rgba32(22, 2, 3, 255),
        false);

    private static readonly Palette Machine = new(
        new Rgba32(40, 44, 48, 255),
        new Rgba32(200, 150, 60, 255),
        new Rgba32(16, 18, 22, 255),
        new Rgba32(118, 128, 138, 255),
        new Rgba32(178, 186, 196, 255),
        new Rgba32(8, 10, 12, 255),
        true);

    private static readonly Palette Gel = new(
        new Rgba32(46, 120, 104, 230),
        new Rgba32(120, 214, 184, 220),
        new Rgba32(28, 82, 74, 225),
        new Rgba32(96, 190, 160, 210),
        new Rgba32(176, 240, 214, 190),
        new Rgba32(16, 56, 52, 235),
        false);

    private static readonly Palette Bark = new(
        new Rgba32(58, 36, 16, 255),
        new Rgba32(212, 156, 52, 255),
        new Rgba32(34, 22, 10, 255),
        new Rgba32(132, 92, 46, 255),
        new Rgba32(222, 196, 128, 255),
        new Rgba32(70, 96, 34, 255),
        false);

    private static readonly Palette Acid = new(
        new Rgba32(40, 92, 12, 255),
        new Rgba32(132, 214, 40, 255),
        new Rgba32(22, 54, 8, 255),
        new Rgba32(96, 170, 30, 255),
        new Rgba32(186, 240, 96, 255),
        new Rgba32(10, 30, 4, 255),
        false);

    public static List<(int X, int Y)> Trace(Vector2 a, Vector2 b, float fraction = 1f)
    {
        var points = new List<(int X, int Y)>();
        var end = a + (b - a) * Math.Clamp(fraction, 0f, 1f);
        var steps = (int) MathF.Ceiling(MathF.Max(MathF.Abs(end.X - a.X), MathF.Abs(end.Y - a.Y)));
        steps = Math.Max(steps, 1);
        for (var i = 0; i <= steps; i++)
        {
            var at = a + (end - a) * (i / (float) steps);
            var point = ((int) MathF.Floor(at.X), (int) MathF.Floor(at.Y));
            if (points.Count == 0 || points[^1] != point)
                points.Add(point);
        }

        return points;
    }

    public static Image<Rgba32> Paint(Vector2 a, Vector2 b, string state, SurgeryInk ink = SurgeryInk.Flesh)
    {
        var palette = ink switch
        {
            SurgeryInk.Machine => Machine,
            SurgeryInk.Gel => Gel,
            SurgeryInk.Bark => Bark,
            SurgeryInk.Acid => Acid,
            _ => Flesh,
        };
        var image = new Image<Rgba32>(Size, Size);
        var horizontal = MathF.Abs(b.X - a.X) > MathF.Abs(b.Y - a.Y);
        var side = horizontal ? (0, 1) : (1, 0);
        switch (state)
        {
            case "open":
                Body(image, Trace(a, b), side, palette.Gape, palette.Lip, palette.Rim);
                break;
            case "deep":
                Body(image, Trace(a, b), side, palette.Pit, palette.Gape, palette.Lip);
                break;
            default:
                var fraction = state.Length > 3 && int.TryParse(state[3..], out var stage) ? stage / 4f : 1f;
                var points = Trace(a, b, fraction);
                for (var i = 0; i < points.Count; i++)
                {
                    var tip = i == points.Count - 1 && points.Count > 1 || palette.Dotted && i % 2 == 0;
                    Put(image, points[i].X, points[i].Y, tip ? palette.Tip : palette.Cut);
                }
                break;
        }

        return image;
    }

    private static void Body(Image<Rgba32> image, List<(int X, int Y)> points, (int X, int Y) side, Rgba32 core, Rgba32 lip, Rgba32 tip)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var (x, y) = points[i];
            if (i == 0 || i == points.Count - 1)
            {
                Put(image, x, y, tip);
                continue;
            }

            Put(image, x - side.X, y - side.Y, lip);
            Put(image, x + side.X, y + side.Y, lip);
        }

        for (var i = 1; i < points.Count - 1; i++)
            Put(image, points[i].X, points[i].Y, core);
    }

    private static void Put(Image<Rgba32> image, int x, int y, Rgba32 color)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size)
            return;

        image[x, y] = color;
    }
}
