// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using System.Linq;
using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Humanoid.Markings;

[Serializable, NetSerializable]
public enum MarkingDyeStyle : byte
{
    Fade,
    Half,
    Inner,
    Highlight,
    Tips,
    Bangs,
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct MarkingDye
{
    public const char DbSeparator = ':';

    public const int MaxColors = 10;

    [DataField]
    public MarkingDyeStyle Style;

    [DataField]
    public List<Color> Colors = new() { Color.White };

    [DataField]
    public float Offset = 0.5f;

    public MarkingDye()
    {
        Colors = new List<Color> { Color.White };
    }

    public MarkingDye(Color color)
    {
        Colors = new List<Color> { color };
    }

    public MarkingDye(List<Color> colors)
    {
        Colors = colors.Count > 0 ? colors : new List<Color> { Color.White };
    }

    public MarkingDye Sanitized()
    {
        var colors = Colors
            .Take(MaxColors)
            .Select(HumanoidCharacterAppearance.ClampColor)
            .ToList();
        if (colors.Count == 0)
            colors.Add(Color.White);

        return this with
        {
            Style = Enum.IsDefined(Style) ? Style : MarkingDyeStyle.Fade,
            Colors = colors,
            Offset = float.IsFinite(Offset) ? Math.Clamp(Offset, 0f, 1f) : 0.5f,
        };
    }

    public bool Equals(MarkingDye other)
    {
        return Style == other.Style
            && Offset.Equals(other.Offset)
            && Colors.Count == other.Colors.Count
            && Colors.SequenceEqual(other.Colors);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Style);
        hash.Add(Offset);
        foreach (var color in Colors)
            hash.Add(color);
        return hash.ToHashCode();
    }

    public static string EncodeDb(string colors, MarkingDye? dye)
    {
        if (dye is not { } value)
            return colors;

        var palette = string.Join(',', value.Colors.Select(c => c.ToHex()));
        var offset = value.Offset.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{colors}{DbSeparator}{(byte) value.Style}:{palette}:{offset}";
    }

    public static string DecodeDb(string input, out MarkingDye? dye)
    {
        dye = null;
        var separator = input.IndexOf(DbSeparator);
        if (separator < 0)
            return input;

        var parts = input[(separator + 1)..].Split(':');
        if (parts.Length == 3
            && byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var style)
            && TryDecodePalette(parts[1], out var palette)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var offset))
        {
            dye = new MarkingDye
            {
                Style = (MarkingDyeStyle) style,
                Colors = palette,
                Offset = offset,
            }.Sanitized();
        }

        return input[..separator];
    }

    private static bool TryDecodePalette(string input, out List<Color> palette)
    {
        palette = new List<Color>();
        foreach (var hex in input.Split(','))
        {
            if (!Color.TryFromHex(hex, out var color))
                return false;
            palette.Add(color);
        }

        return palette.Count > 0;
    }
}
