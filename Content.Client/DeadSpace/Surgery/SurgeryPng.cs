// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using System.IO.Compression;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.DeadSpace.Surgery;

public readonly record struct SurgeryArt(Rgba32[] Pixels, int Width, int Height);

public static class SurgeryPng
{
    private const int Ihdr = 0x49484452;
    private const int Plte = 0x504C5445;
    private const int Trns = 0x74524E53;
    private const int Idat = 0x49444154;
    private const int Iend = 0x49454E44;

    public static SurgeryArt? Decode(byte[] bytes, int frameWidth = 0, int frameHeight = 0)
    {
        if (bytes.Length < 33 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            return null;

        int width = 0, height = 0, depth = 0, color = 0;
        byte[]? palette = null;
        byte[]? alphas = null;
        using var packed = new MemoryStream();
        var at = 8;
        while (at + 8 <= bytes.Length)
        {
            var length = (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
            var type = Int(bytes, at + 4);
            var data = at + 8;
            if (length < 0 || data + length > bytes.Length)
                return null;

            switch (type)
            {
                case Ihdr:
                    width = Int(bytes, data);
                    height = Int(bytes, data + 4);
                    depth = bytes[data + 8];
                    color = bytes[data + 9];
                    if (bytes[data + 12] != 0)
                        return null;
                    break;
                case Plte:
                    palette = Slice(bytes, data, length);
                    break;
                case Trns:
                    alphas = Slice(bytes, data, length);
                    break;
                case Idat:
                    packed.Write(bytes, data, length);
                    break;
            }

            if (type == Iend)
                break;
            at = data + length + 4;
        }

        var channels = color switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (width <= 0 || height <= 0 || channels == 0 || depth is not (1 or 2 or 4 or 8 or 16) || packed.Length < 2)
            return null;

        var raw = Inflate(packed.ToArray());
        var stride = (width * channels * depth + 7) / 8;
        var step = Math.Max(1, channels * depth / 8);
        if (raw.Length < (stride + 1) * height)
            return null;

        var rows = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var source = y * (stride + 1) + 1;
            var row = y * stride;
            for (var i = 0; i < stride; i++)
            {
                var left = i >= step ? rows[row + i - step] : 0;
                var up = y > 0 ? rows[row - stride + i] : 0;
                var corner = y > 0 && i >= step ? rows[row - stride + i - step] : 0;
                var value = raw[source + i];
                rows[row + i] = (byte) (filter switch
                {
                    1 => value + left,
                    2 => value + up,
                    3 => value + (left + up) / 2,
                    4 => value + Paeth(left, up, corner),
                    _ => value,
                });
            }
        }

        var outWidth = frameWidth > 0 ? Math.Min(frameWidth, width) : width;
        var outHeight = frameHeight > 0 ? Math.Min(frameHeight, height) : height;
        var pixels = new Rgba32[outWidth * outHeight];
        for (var y = 0; y < outHeight; y++)
        {
            for (var x = 0; x < outWidth; x++)
                pixels[y * outWidth + x] = Pixel(rows, y * stride, x, depth, color, palette, alphas);
        }

        return new SurgeryArt(pixels, outWidth, outHeight);
    }

    private static Rgba32 Pixel(byte[] rows, int row, int x, int depth, int color, byte[]? palette, byte[]? alphas)
    {
        switch (color)
        {
            case 3:
            {
                var index = Sample(rows, row, x, depth);
                if (palette == null || index * 3 + 2 >= palette.Length)
                    return default;

                var a = alphas != null && index < alphas.Length ? alphas[index] : (byte) 255;
                return new Rgba32(palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2], a);
            }
            case 0:
            {
                var raw = Sample(rows, row, x, depth);
                var gray = (byte) (depth >= 8 ? raw : raw * 255 / ((1 << depth) - 1));
                var a = alphas is { Length: >= 2 } && ((alphas[0] << 8) | alphas[1]) == raw ? (byte) 0 : (byte) 255;
                return new Rgba32(gray, gray, gray, a);
            }
            default:
            {
                var channels = color switch { 2 => 3, 4 => 2, _ => 4 };
                var size = depth / 8;
                var start = row + x * channels * size;
                byte Get(int channel) => rows[start + channel * size];
                return color switch
                {
                    2 => new Rgba32(Get(0), Get(1), Get(2), 255),
                    4 => new Rgba32(Get(0), Get(0), Get(0), Get(1)),
                    _ => new Rgba32(Get(0), Get(1), Get(2), Get(3)),
                };
            }
        }
    }

    private static int Sample(byte[] rows, int row, int x, int depth)
    {
        if (depth == 16)
            return rows[row + x * 2];
        if (depth == 8)
            return rows[row + x];

        var bit = x * depth;
        var shift = 8 - depth - bit % 8;
        return (rows[row + bit / 8] >> shift) & ((1 << depth) - 1);
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static byte[] Slice(byte[] bytes, int at, int length)
    {
        var slice = new byte[length];
        Array.Copy(bytes, at, slice, 0, length);
        return slice;
    }

    private static int Int(byte[] bytes, int at)
    {
        return (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
    }

    private static byte[] Inflate(byte[] zlib)
    {
        using var input = new MemoryStream(zlib, 2, zlib.Length - 2);
        using var inflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflate.CopyTo(output);
        return output.ToArray();
    }
}
