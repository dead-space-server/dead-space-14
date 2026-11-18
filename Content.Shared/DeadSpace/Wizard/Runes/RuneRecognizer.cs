// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public static class RuneRecognizer
{
    public const int Samples = 48;
    public const float MinLength = 0.05f;
    private const float WorstDistance = 0.6f;

    public static Vector2[]? Normalize(IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2)
            return null;

        var length = 0f;
        for (var i = 1; i < points.Count; i++)
            length += Vector2.Distance(points[i - 1], points[i]);

        var min = points[0];
        var max = points[0];
        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        var extent = MathF.Max(max.X - min.X, max.Y - min.Y);
        if (extent <= 0f || length < extent * MinLength)
            return null;

        var resampled = Resample(points, length);
        var centroid = Vector2.Zero;
        foreach (var point in resampled)
            centroid += point;

        centroid /= resampled.Length;
        for (var i = 0; i < resampled.Length; i++)
            resampled[i] = (resampled[i] - centroid) / extent;

        return resampled;
    }

    public static float Score(Vector2[] stroke, Vector2[] template)
    {
        var forward = 0f;
        var backward = 0f;
        for (var i = 0; i < Samples; i++)
        {
            forward += Vector2.Distance(stroke[i], template[i]);
            backward += Vector2.Distance(stroke[i], template[Samples - 1 - i]);
        }

        var distance = MathF.Min(forward, backward) / Samples;
        return Math.Clamp(1f - distance / WorstDistance, 0f, 1f);
    }

    public static bool TryMatch(
        IReadOnlyList<Vector2> stroke,
        IEnumerable<(string Id, Vector2[] Template)> templates,
        float threshold,
        out string? id,
        out float score)
    {
        id = null;
        score = 0f;

        var normalized = Normalize(stroke);
        if (normalized == null)
            return false;

        foreach (var (candidate, template) in templates)
        {
            var current = Score(normalized, template);
            if (current <= score)
                continue;

            score = current;
            id = candidate;
        }

        return id != null && score >= threshold;
    }

    private static Vector2[] Resample(IReadOnlyList<Vector2> points, float length)
    {
        var step = length / (Samples - 1);
        var result = new Vector2[Samples];
        result[0] = points[0];
        var count = 1;
        var carried = 0f;
        var previous = points[0];

        for (var i = 1; i < points.Count && count < Samples; i++)
        {
            var current = points[i];
            var segment = Vector2.Distance(previous, current);

            while (segment > 0f && carried + segment >= step && count < Samples)
            {
                var t = (step - carried) / segment;
                var point = Vector2.Lerp(previous, current, t);
                result[count++] = point;
                segment -= step - carried;
                previous = point;
                carried = 0f;
            }

            carried += segment;
            previous = current;
        }

        while (count < Samples)
            result[count++] = points[^1];

        return result;
    }
}
