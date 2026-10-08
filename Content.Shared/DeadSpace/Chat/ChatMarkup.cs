using System.Collections.Generic;
using System.Text;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Shared.Chat;

/// <summary>
///     Помощники для оформления текста чата с помощью маркапа.
/// </summary>
public static class ChatMarkup
{
    /// <summary>
    ///     Оборачивает текст в градиент от <paramref name="start"/> к <paramref name="end"/>.
    ///     Каждый символ получает свой оттенок, поэтому текст можно безопасно вставлять в любой маркап.
    /// </summary>
    /// <remarks>
    ///     Ожидает уже экранированный текст (см. <see cref="FormattedMessage.EscapeText"/>).
    /// </remarks>
    public static string Gradient(string text, Color start, Color end)
    {
        var units = SplitUnits(text);
        if (units.Count == 0)
            return string.Empty;

        var builder = new StringBuilder(text.Length * 24);
        for (var i = 0; i < units.Count; i++)
        {
            var color = units.Count == 1
                ? start
                : Color.InterpolateBetween(start, end, i / (float) (units.Count - 1));

            AppendColored(builder, units[i], color);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Оборачивает текст в один цвет.
    /// </summary>
    public static string Colored(string text, Color color)
    {
        var builder = new StringBuilder(text.Length + 32);
        AppendColored(builder, text, color);
        return builder.ToString();
    }

    private static void AppendColored(StringBuilder builder, string text, Color color)
    {
        builder.Append("[color=").Append(color.ToHex()).Append(']').Append(text).Append("[/color]");
    }

    /// <summary>
    ///     Разбивает текст на единицы отрисовки, чтобы градиент не разрывал escape-последовательности маркапа.
    /// </summary>
    private static List<string> SplitUnits(string text)
    {
        var units = new List<string>(text.Length);

        var index = 0;
        while (index < text.Length)
        {
            var rune = Rune.GetRuneAt(text, index);
            index += rune.Utf16SequenceLength;

            // "\x" рисуется как один символ, поэтому и в градиенте должен быть одной единицей.
            if (rune.Value == '\\' && index < text.Length)
            {
                var next = Rune.GetRuneAt(text, index);
                index += next.Utf16SequenceLength;
                units.Add(string.Concat(rune.ToString(), next.ToString()));
                continue;
            }

            // Одиночные скобки ломают разметку, поэтому экранируем их на всякий случай.
            if (rune.Value is '[' or ']')
                units.Add("\\" + rune);
            else
                units.Add(rune.ToString());
        }

        return units;
    }
}
