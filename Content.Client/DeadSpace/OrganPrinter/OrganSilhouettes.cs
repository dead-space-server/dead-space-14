// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.OrganPrinter;

/// <summary>
/// Генерирует блеклые силуэты органов для поля мини-игры.
/// Маска задаётся в сетке 18x13 и масштабируется на игровое поле.
/// </summary>
public static class OrganSilhouettes
{
    public const int MaskW = 18;
    public const int MaskH = 13;

    private delegate bool ShapeFunc(float x, float y);

    private static readonly Dictionary<string, ShapeFunc> Shapes = new()
    {
        ["OrganHumanHeart"] = Heart,
        ["OrganHumanBrain"] = Brain,
        ["OrganHumanEyes"] = Eyes,
        ["OrganHumanLungs"] = Lungs,
        ["OrganHumanKidneys"] = Kidneys,
        ["OrganHumanLiver"] = Liver,
        ["OrganHumanStomach"] = Stomach,
        ["OrganHumanTongue"] = Tongue,
        ["OrganHumanEars"] = Ears,
        ["OrganHumanAppendix"] = Appendix,
    };

    /// <summary>
    /// Возвращает маску силуэта в координатах игровой сетки (уже масштабированную и отцентрованную).
    /// </summary>
    public static bool[,] GetMask(EntProtoId product, int fieldW, int fieldH)
    {
        var shape = Shapes.GetValueOrDefault(product.Id, Fallback);
        var raw = new bool[MaskW, MaskH];

        for (var x = 0; x < MaskW; x++)
        {
            for (var y = 0; y < MaskH; y++)
            {
                raw[x, y] = shape((x + 0.5f) / MaskW * 2f - 1f, (y + 0.5f) / MaskH * 2f - 1f);
            }
        }

        var scale = 2;
        var mask = new bool[fieldW, fieldH];
        var offsetX = (fieldW - MaskW * scale) / 2;
        var offsetY = (fieldH - MaskH * scale) / 2;

        for (var x = 0; x < MaskW; x++)
        {
            for (var y = 0; y < MaskH; y++)
            {
                if (!raw[x, y])
                    continue;

                for (var sx = 0; sx < scale; sx++)
                {
                    for (var sy = 0; sy < scale; sy++)
                    {
                        var fx = offsetX + x * scale + sx;
                        var fy = offsetY + y * scale + sy;
                        if (fx > 0 && fx < fieldW - 1 && fy > 0 && fy < fieldH - 1)
                            mask[fx, fy] = true;
                    }
                }
            }
        }

        return mask;
    }

    private static bool Heart(float x, float y)
    {
        var nx = x * 1.25f;
        var ny = -(y * 1.35f - 0.25f);
        var a = nx * nx + ny * ny - 1f;
        return a * a * a - nx * nx * ny * ny * ny < 0f;
    }

    private static bool Brain(float x, float y)
    {
        return Ellipse(x, y, 0.88f, 0.66f);
    }

    private static bool Eyes(float x, float y)
    {
        return Circle(x + 0.45f, y, 0.34f) || Circle(x - 0.45f, y, 0.34f);
    }

    private static bool Lungs(float x, float y)
    {
        return Ellipse(x + 0.42f, y + 0.05f, 0.3f, 0.72f) || Ellipse(x - 0.42f, y + 0.05f, 0.3f, 0.72f);
    }

    private static bool Kidneys(float x, float y)
    {
        return Ellipse(x + 0.55f, y, 0.22f, 0.58f) || Ellipse(x - 0.55f, y, 0.22f, 0.58f);
    }

    private static bool Liver(float x, float y)
    {
        return Ellipse(x + 0.1f, y - 0.1f, 0.85f, 0.6f) && !(x < -0.1f && y > 0.35f);
    }

    private static bool Stomach(float x, float y)
    {
        return Ellipse(x + 0.1f, y, 0.62f, 0.6f) && !Circle(x - 0.35f, y - 0.25f, 0.34f);
    }

    private static bool Tongue(float x, float y)
    {
        return MathF.Abs(x) < 0.78f && MathF.Abs(y + 0.05f) < 0.38f;
    }

    private static bool Ears(float x, float y)
    {
        return Ring(x + 0.5f, y, 0.32f, 0.14f) || Ring(x - 0.5f, y, 0.32f, 0.14f);
    }

    private static bool Appendix(float x, float y)
    {
        var path = -0.45f + 0.35f * MathF.Sin(x * 3.5f);
        return MathF.Abs(y - path) < 0.14f && MathF.Abs(x) < 0.75f;
    }

    private static bool Fallback(float x, float y)
    {
        return Ellipse(x, y, 0.7f, 0.5f);
    }

    private static bool Ellipse(float x, float y, float rx, float ry)
    {
        return x * x / (rx * rx) + y * y / (ry * ry) <= 1f;
    }

    private static bool Circle(float x, float y, float r)
    {
        return x * x + y * y <= r * r;
    }

    private static bool Ring(float x, float y, float outer, float inner)
    {
        var d = x * x + y * y;
        return d <= outer * outer && d >= inner * inner;
    }
}
