// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.DeadSpace.Wizard.Runes;

public static class RuneCircleArt
{
    public const int RingSize = 512;
    public const float RingRadius = 248f;

    private static readonly Dictionary<string, Texture> Cache = new();

    public static float RingDiameter(float radius)
    {
        return radius * RingSize / RingRadius;
    }

    public static Texture Glow(IClyde clyde)
    {
        return Get(clyde, "glow", () => Paint(128, (x, y) =>
        {
            var d = MathF.Sqrt(x * x + y * y);
            var a = Math.Clamp(1f - d, 0f, 1f);
            return a * a * a;
        }));
    }

    public static Texture Ring(IClyde clyde, float thickness, int dashes = 0, float duty = 1f)
    {
        thickness = MathF.Round(thickness * 2f) / 2f;
        return Get(clyde, $"ring:{thickness}:{dashes}:{duty}", () => Paint(RingSize, (x, y) =>
        {
            var px = x * RingSize / 2f;
            var py = y * RingSize / 2f;
            var distance = MathF.Abs(MathF.Sqrt(px * px + py * py) - RingRadius);
            var alpha = Math.Clamp(thickness / 2f + 0.75f - distance, 0f, 1f);
            if (alpha <= 0f || dashes <= 0)
                return alpha;

            var turn = (MathF.Atan2(py, px) + MathF.PI) / MathF.Tau * dashes % 1f;
            if (turn > duty)
                return 0f;

            var edge = MathF.Min(turn, duty - turn) * MathF.Tau * RingRadius / dashes;
            return alpha * Math.Clamp(edge + 0.5f, 0f, 1f);
        }));
    }

    public static Texture Band(IClyde clyde, float width)
    {
        width = MathF.Round(width);
        return Get(clyde, $"band:{width}", () => Paint(RingSize, (x, y) =>
        {
            var px = x * RingSize / 2f;
            var py = y * RingSize / 2f;
            var distance = (MathF.Sqrt(px * px + py * py) - RingRadius) / (width / 2f);
            return MathF.Exp(-distance * distance * 2.2f);
        }));
    }

    public static Texture Comet(IClyde clyde, float width, float length = 0.22f)
    {
        width = MathF.Round(width);
        return Get(clyde, $"comet:{width}:{length}", () => Paint(RingSize, (x, y) =>
        {
            var px = x * RingSize / 2f;
            var py = y * RingSize / 2f;
            var distance = (MathF.Sqrt(px * px + py * py) - RingRadius) / (width / 2f);
            var band = MathF.Exp(-distance * distance * 3f);
            var turn = (MathF.Atan2(py, px) + MathF.PI) / MathF.Tau;
            if (turn > length)
                return 0f;

            var tail = turn / length;
            return band * tail * tail;
        }));
    }

    private static Texture Get(IClyde clyde, string key, Func<Image<Rgba32>> paint)
    {
        if (Cache.TryGetValue(key, out var texture))
            return texture;

        texture = clyde.LoadTextureFromImage(paint(), "rune-circle-" + key);
        Cache[key] = texture;
        return texture;
    }

    private static Image<Rgba32> Paint(int size, Func<float, float, float> alpha)
    {
        var image = new Image<Rgba32>(size, size);
        var half = size / 2f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var value = alpha((x + 0.5f - half) / half, (y + 0.5f - half) / half);
            image[x, y] = new Rgba32(255, 255, 255, (byte) (Math.Clamp(value, 0f, 1f) * 255f));
        }

        return image;
    }
}
