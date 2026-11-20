// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

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
    public const int MaxColors = 10;

    public const float DefaultOffset = 0.5f;

    [DataField]
    public MarkingDyeStyle Style = MarkingDyeStyle.Fade;

    [DataField]
    public List<Color> Colors = new() { Color.White };

    [DataField]
    public float Offset = DefaultOffset;

    public MarkingDye()
    {
    }

    public MarkingDye(Color color)
    {
        Colors = new List<Color> { color };
    }

    public MarkingDye(List<Color> colors)
    {
        Colors = colors.Count == 0 ? new List<Color> { Color.White } : colors;
    }

    public MarkingDye Cleaned()
    {
        var palette = Colors
            .Take(MaxColors)
            .Select(HumanoidCharacterAppearance.ClampColor)
            .ToList();

        if (palette.Count == 0)
            palette.Add(Color.White);

        return this with
        {
            Style = Enum.IsDefined(Style) ? Style : MarkingDyeStyle.Fade,
            Colors = palette,
            Offset = float.IsFinite(Offset) ? Math.Clamp(Offset, 0f, 1f) : DefaultOffset,
        };
    }

    public bool Equals(MarkingDye other)
    {
        if (Style != other.Style || !Offset.Equals(other.Offset))
            return false;

        return Colors.SequenceEqual(other.Colors);
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
}
