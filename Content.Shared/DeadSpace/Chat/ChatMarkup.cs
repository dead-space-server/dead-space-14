using System.Text;

namespace Content.Shared.Chat;

public static class ChatMarkup
{
    public static string Gradient(string text, Color start, Color end)
    {
        var units = SplitUnits(text);
        if (units.Count == 0)
            return string.Empty;

        var builder = new StringBuilder(text.Length * 24);
        for (var i = 0; i < units.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(units[i]))
            {
                builder.Append(units[i]);
                continue;
            }

            var color = units.Count == 1
                ? start
                : Color.InterpolateBetween(start, end, i / (float)(units.Count - 1));

            builder.Append("[color=").Append(color.ToHex()).Append(']').Append(units[i]).Append("[/color]");
        }

        return builder.ToString();
    }

    public static string Colored(string text, Color color)
    {
        return $"[color={color.ToHex()}]{text}[/color]";
    }

    private static List<string> SplitUnits(string text)
    {
        var units = new List<string>(text.Length);

        var index = 0;
        while (index < text.Length)
        {
            if (!Rune.TryGetRuneAt(text, index, out var rune))
            {
                rune = Rune.ReplacementChar;
                index++;
            }
            else
            {
                index += rune.Utf16SequenceLength;
            }

            if (rune.Value == '\\')
            {
                if (index < text.Length && Rune.TryGetRuneAt(text, index, out var next))
                {
                    index += next.Utf16SequenceLength;
                    units.Add("\\" + next);
                }
                else
                {
                    units.Add("\\\\");
                }

                continue;
            }

            units.Add(rune.Value is '[' or ']' ? "\\" + rune : rune.ToString());
        }

        return units;
    }
}