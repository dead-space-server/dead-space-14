// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryFilmPainter
{
    private static readonly Vector2 Middle = new(16f, 16f);

    public readonly int Size;
    public readonly float Scale;
    public readonly Vector2 Stretch;

    private readonly float[] _r;
    private readonly float[] _g;
    private readonly float[] _b;
    private readonly float[] _a;

    public SurgeryFilmPainter(int scale, Vector2? stretch = null)
    {
        Scale = Math.Max(1, scale);
        Stretch = stretch ?? Vector2.One;
        Size = (int) (32 * Scale);
        _r = new float[Size * Size];
        _g = new float[Size * Size];
        _b = new float[Size * Size];
        _a = new float[Size * Size];
    }

    private Vector2 Map(Vector2 point)
    {
        return ((point - Middle) * Stretch + Middle) * Scale;
    }

    public void Capsule(Vector2 from, Vector2 to, float radius, Color color, float alpha)
    {
        var a = Map(from);
        var b = Map(to);
        var r = MathF.Max(0.6f, radius * Scale * (Stretch.X + Stretch.Y) * 0.5f);
        var left = (int) MathF.Floor(MathF.Min(a.X, b.X) - r - 1);
        var right = (int) MathF.Ceiling(MathF.Max(a.X, b.X) + r + 1);
        var top = (int) MathF.Floor(MathF.Min(a.Y, b.Y) - r - 1);
        var bottom = (int) MathF.Ceiling(MathF.Max(a.Y, b.Y) + r + 1);
        var span = b - a;
        var length2 = MathF.Max(0.0001f, span.LengthSquared());
        for (var y = Math.Max(0, top); y <= Math.Min(Size - 1, bottom); y++)
        {
            for (var x = Math.Max(0, left); x <= Math.Min(Size - 1, right); x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var t = Math.Clamp(Vector2.Dot(p - a, span) / length2, 0f, 1f);
                var distance = (p - (a + span * t)).Length();
                var cover = Math.Clamp(r - distance + 0.5f, 0f, 1f);
                if (cover > 0f)
                    Blend(x, y, color, alpha * cover);
            }
        }
    }

    public void Oval(Vector2 center, Vector2 radii, Color color, float alpha, float ring = 0f, bool dashed = false)
    {
        var c = Map(center);
        var rx = MathF.Max(0.6f, radii.X * Scale * Stretch.X);
        var ry = MathF.Max(0.6f, radii.Y * Scale * Stretch.Y);
        var width = ring * Scale * (Stretch.X + Stretch.Y) * 0.5f;
        for (var y = Math.Max(0, (int) (c.Y - ry - 2)); y <= Math.Min(Size - 1, (int) (c.Y + ry + 2)); y++)
        {
            for (var x = Math.Max(0, (int) (c.X - rx - 2)); x <= Math.Min(Size - 1, (int) (c.X + rx + 2)); x++)
            {
                var dx = (x + 0.5f - c.X) / rx;
                var dy = (y + 0.5f - c.Y) / ry;
                var norm = MathF.Sqrt(dx * dx + dy * dy);
                var edge = (1f - norm) * MathF.Min(rx, ry);
                float cover;
                if (width > 0f)
                {
                    cover = Math.Clamp(edge + 0.5f, 0f, 1f) * Math.Clamp(width - edge + 0.5f, 0f, 1f);
                    if (dashed && (int) ((MathF.Atan2(dy, dx) + MathF.PI) / (MathF.PI / 7f)) % 2 == 1)
                        cover = 0f;
                }
                else
                {
                    cover = Math.Clamp(edge + 0.5f, 0f, 1f);
                }

                if (cover > 0f)
                    Blend(x, y, color, alpha * cover);
            }
        }
    }

    public void Zigzag(Vector2 center, Vector2 along, float length, float radius, Color color, float alpha, int bends = 3)
    {
        if (along.LengthSquared() < 0.0001f)
            along = new Vector2(1f, 0f);

        along = Vector2.Normalize(along);
        var side = new Vector2(-along.Y, along.X);
        var previous = center - along * length / 2f;
        for (var i = 1; i <= bends + 1; i++)
        {
            var t = i / (float) (bends + 1);
            var swing = i == bends + 1 ? 0f : (i % 2 == 0 ? -1f : 1f) * length * 0.18f;
            var next = center - along * length / 2f + along * length * t + side * swing;
            Capsule(previous, next, radius, color, alpha);
            previous = next;
        }
    }

    public void Blit(Rgba32[] pixels, int width, int height, Vector2 center, float span, bool gray)
    {
        Blit(pixels, width, height, center, new Vector2(span, span), gray, 1f);
    }

    public void Blit(Rgba32[] pixels, int width, int height, Vector2 center, Vector2 span, bool gray, float alpha)
    {
        if (width <= 0 || height <= 0)
            return;

        var sizeX = span.X * Scale * Stretch.X;
        var sizeY = span.Y * Scale * Stretch.Y;
        var mapped = Map(center);
        var left = mapped.X - sizeX / 2f;
        var top = mapped.Y - sizeY / 2f;
        for (var y = 0; y < (int) sizeY; y++)
        {
            for (var x = 0; x < (int) sizeX; x++)
            {
                var sx = (int) (x / sizeX * width);
                var sy = (int) (y / sizeY * height);
                if ((uint) sx >= (uint) width || (uint) sy >= (uint) height)
                    continue;

                var sample = pixels[sy * width + sx];
                if (sample.A < 16)
                    continue;

                var color = new Color(sample.R / 255f, sample.G / 255f, sample.B / 255f);
                if (gray)
                {
                    var lum = 0.35f + (sample.R * 0.30f + sample.G * 0.59f + sample.B * 0.11f) / 255f * 0.65f;
                    color = new Color(lum * 0.92f, lum, lum * 1.04f);
                }

                Blend((int) (left + x), (int) (top + y), color, sample.A / 255f * alpha);
            }
        }
    }

    public Rgba32[] Pixels()
    {
        var pixels = new Rgba32[Size * Size];
        for (var i = 0; i < pixels.Length; i++)
        {
            var a = _a[i];
            if (a <= 0.001f)
                continue;

            pixels[i] = new Rgba32(
                (byte) Math.Clamp(_r[i] / a * 255f, 0f, 255f),
                (byte) Math.Clamp(_g[i] / a * 255f, 0f, 255f),
                (byte) Math.Clamp(_b[i] / a * 255f, 0f, 255f),
                (byte) Math.Clamp(a * 255f, 0f, 255f));
        }

        return pixels;
    }

    public SixLabors.ImageSharp.Image<Rgba32> ToImage(Rgba32[] pixels)
    {
        var image = new SixLabors.ImageSharp.Image<Rgba32>(Size, Size);
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var pixel = pixels[y * Size + x];
                if (pixel.A > 0)
                    image[x, y] = pixel;
            }
        }

        return image;
    }

    private void Blend(int x, int y, Color color, float alpha)
    {
        if ((uint) x >= (uint) Size || (uint) y >= (uint) Size || alpha <= 0f)
            return;

        alpha = MathF.Min(1f, alpha * color.A);
        var i = y * Size + x;
        var keep = 1f - alpha;
        _r[i] = color.R * alpha + _r[i] * keep;
        _g[i] = color.G * alpha + _g[i] * keep;
        _b[i] = color.B * alpha + _b[i] * keep;
        _a[i] = alpha + _a[i] * keep;
    }
}
