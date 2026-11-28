// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using System.Linq;

namespace Content.Shared.DeadSpace.Humanoid.Markings;

public static class MarkingDyeStore
{
    private const char Marker = '~';
    private const char FieldSeparator = '-';
    private const char PaletteSeparator = ',';

    private const char OldMarker = ':';

    public static string Pack(string color, MarkingDye? dye)
    {
        if (dye is not { } value)
            return color;

        var style = ((byte) value.Style).ToString(CultureInfo.InvariantCulture);
        var offset = value.Offset.ToString("0.###", CultureInfo.InvariantCulture);
        var palette = string.Join(PaletteSeparator, value.Colors.Select(c => c.ToHex()));
        var payload = $"{style}{FieldSeparator}{offset}{FieldSeparator}{palette}";

        return $"{color}{Marker}{payload}";
    }

    public static string Unpack(string packed, out MarkingDye? dye)
    {
        dye = null;

        var cut = packed.IndexOf(Marker);
        if (cut >= 0)
        {
            TryRead(packed[(cut + 1)..].Split(FieldSeparator), out dye);
            return packed[..cut];
        }

        cut = packed.IndexOf(OldMarker);
        if (cut >= 0)
        {
            TryRead(packed[(cut + 1)..].Split(OldMarker), out dye);
            return packed[..cut];
        }

        return packed;
    }

    private static bool TryRead(string[] fields, out MarkingDye? dye)
    {
        dye = null;

        if (fields.Length != 3)
            return false;

        if (!byte.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var style)
            || !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offset))
        {
            return false;
        }

        var palette = new List<Color>();
        foreach (var hex in fields[2].Split(PaletteSeparator))
        {
            if (!Color.TryFromHex(hex, out var color))
                return false;

            palette.Add(color);
        }

        if (palette.Count == 0)
            return false;

        dye = new MarkingDye
        {
            Style = (MarkingDyeStyle) style,
            Colors = palette,
            Offset = offset,
        }.Cleaned();

        return true;
    }
}
