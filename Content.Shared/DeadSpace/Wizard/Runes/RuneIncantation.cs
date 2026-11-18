// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Text;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Wizard.Runes;

public static class RuneIncantation
{
    public const string WaveTag = "runewave";
    public const int MaxLength = 160;

    private static readonly float[] CapsFraction = { 0f, 0f, 0.35f, 0.7f, 1f, 1f };
    private static readonly int[] FontSize = { 10, 12, 12, 13, 15, 19 };
    private static readonly float[] Range = { 3f, 10f, 12f, 16f, 22f, 30f };

    public static float HearingRange(int loudness)
    {
        return Range[Math.Clamp(loudness, 0, Range.Length - 1)];
    }

    public static string Plain(string text, int loudness, int absurdity)
    {
        return Shape(Trim(text), loudness, absurdity);
    }

    public static string Markup(string text, int loudness, int absurdity, int seed = 0)
    {
        loudness = Math.Clamp(loudness, 0, SharedRuneCasterSystem.MaxLoudness);
        absurdity = Math.Clamp(absurdity, 0, SharedRuneCasterSystem.MaxAbsurdity);

        var shaped = Shape(Trim(text), loudness, absurdity);
        var size = FontSize[loudness] + (absurdity >= 4 ? 2 : 0);
        var body = new StringBuilder();

        if (absurdity >= 3)
        {
            var visible = 0;
            foreach (var rune in shaped.EnumerateRunes())
            {
                var piece = FormattedMessage.EscapeText(rune.ToString());
                if (Rune.IsWhiteSpace(rune))
                {
                    body.Append(piece);
                    continue;
                }

                var hue = (visible * 0.09f + seed * 0.13f) % 1f;
                var tint = Color.FromHsv(new Vector4(hue, 0.8f, 1f, 1f));
                body.Append("[color=").Append(tint.ToHex()).Append(']').Append(piece).Append("[/color]");
                visible++;
            }
        }
        else
        {
            body.Append(FormattedMessage.EscapeText(shaped));
        }

        var result = new StringBuilder();
        result.Append("[font size=").Append(size).Append(']');

        var bold = loudness >= 3;
        var italic = loudness == 0;
        var color = loudness switch
        {
            0 => "#9a9aa8",
            4 => "#ff6040",
            5 => "#ff1a1a",
            _ => null,
        };

        if (absurdity == 2)
            color = "#f2a7ff";

        if (absurdity >= 3)
            color = null;

        if (color != null)
            result.Append("[color=").Append(color).Append(']');
        if (bold)
            result.Append("[bold]");
        if (italic)
            result.Append("[italic]");
        if (absurdity >= 5)
            result.Append('[').Append(WaveTag).Append(']');

        result.Append(body);

        if (absurdity >= 5)
            result.Append("[/").Append(WaveTag).Append(']');
        if (italic)
            result.Append("[/italic]");
        if (bold)
            result.Append("[/bold]");
        if (color != null)
            result.Append("[/color]");

        result.Append("[/font]");
        return result.ToString();
    }

    public static string Gold(string text)
    {
        var glyphs = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune))
                glyphs++;
        }

        var body = new StringBuilder();
        var index = 0;
        var last = Math.Max(glyphs - 1, 1);
        foreach (var rune in text.EnumerateRunes())
        {
            var piece = FormattedMessage.EscapeText(rune.ToString());
            if (Rune.IsWhiteSpace(rune))
            {
                body.Append(piece);
                continue;
            }

            body.Append("[color=").Append(GoldHex(index / (float) last)).Append(']').Append(piece).Append("[/color]");
            index++;
        }

        return "[font size=16][bold]" + body + "[/bold][/font]";
    }

    private static string GoldHex(float t)
    {
        int ar, ag, ab, br, bg, bb;
        float u;
        if (t < 0.5f)
        {
            ar = 255;
            ag = 244;
            ab = 196;
            br = 255;
            bg = 196;
            bb = 58;
            u = t * 2f;
        }
        else
        {
            ar = 255;
            ag = 196;
            ab = 58;
            br = 184;
            bg = 122;
            bb = 28;
            u = (t - 0.5f) * 2f;
        }

        var r = (int) MathF.Round(ar + (br - ar) * u);
        var g = (int) MathF.Round(ag + (bg - ag) * u);
        var b = (int) MathF.Round(ab + (bb - ab) * u);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static string Shape(string text, int loudness, int absurdity)
    {
        loudness = Math.Clamp(loudness, 0, SharedRuneCasterSystem.MaxLoudness);
        absurdity = Math.Clamp(absurdity, 0, SharedRuneCasterSystem.MaxAbsurdity);

        if (absurdity >= 1)
            return Alternate(text, absurdity >= 2 ? 0 : text.Length / 2);

        var fraction = CapsFraction[loudness];
        if (fraction <= 0f)
            return text;

        var letters = 0;
        foreach (var c in text)
        {
            if (char.IsLetter(c))
                letters++;
        }

        var from = (int) MathF.Floor(letters * (1f - fraction));
        var builder = new StringBuilder(text.Length + 3);
        var index = 0;
        foreach (var c in text)
        {
            if (!char.IsLetter(c))
            {
                builder.Append(c);
                continue;
            }

            builder.Append(index >= from ? char.ToUpperInvariant(c) : c);
            index++;
        }

        if (loudness >= 5)
            builder.Append("!!");

        return builder.ToString();
    }

    private static string Alternate(string text, int from)
    {
        var builder = new StringBuilder(text.Length);
        var index = 0;
        var flip = false;
        foreach (var c in text)
        {
            if (!char.IsLetter(c))
            {
                builder.Append(c);
                continue;
            }

            if (index++ < from)
            {
                builder.Append(c);
                continue;
            }

            builder.Append(flip ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            flip = !flip;
        }

        return builder.ToString();
    }

    private static string Trim(string text)
    {
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
