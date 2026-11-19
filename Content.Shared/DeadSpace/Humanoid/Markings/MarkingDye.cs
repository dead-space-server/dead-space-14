// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
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

    [DataField]
    public MarkingDyeStyle Style;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public float Offset = 0.5f;

    public MarkingDye()
    {
    }

    public MarkingDye Sanitized()
    {
        return this with
        {
            Style = Enum.IsDefined(Style) ? Style : MarkingDyeStyle.Fade,
            Color = HumanoidCharacterAppearance.ClampColor(Color),
            Offset = float.IsFinite(Offset) ? Math.Clamp(Offset, 0f, 1f) : 0.5f,
        };
    }

    public static string EncodeDb(string colors, MarkingDye? dye)
    {
        if (dye is not { } value)
            return colors;

        var offset = value.Offset.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{colors}{DbSeparator}{(byte) value.Style}:{value.Color.ToHex()}:{offset}";
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
            && Color.TryFromHex(parts[1], out var color)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var offset))
        {
            dye = new MarkingDye
            {
                Style = (MarkingDyeStyle) style,
                Color = color,
                Offset = offset,
            }.Sanitized();
        }

        return input[..separator];
    }
}
