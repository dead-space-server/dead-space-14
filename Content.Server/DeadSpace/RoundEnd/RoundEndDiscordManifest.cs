// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using System.IO;
using System.Linq;
using Content.Server.Discord;
using Content.Shared.GameTicking;
using Robust.Shared.Utility;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageColor = SixLabors.ImageSharp.Color;

namespace Content.Server.DeadSpace.RoundEnd;

/// <summary>
/// A static version of the evacuation credits for the Discord round log.
/// </summary>
internal sealed class RoundEndDiscordManifest
{
    private const int Width = 960;
    private const int MaxHeight = 820;
    private const int Margin = 36;
    private const int TextWidth = Width - 2 * Margin;
    private const int ColumnGap = 28;
    private const int Columns = 3;
    private const int CellWidth = (TextWidth - (Columns - 1) * ColumnGap) / Columns;
    private const int RowGap = 24;

    private static readonly ImageColor Text = ImageColor.ParseHex("E8E8E8");
    private static readonly ImageColor Dim = ImageColor.ParseHex("8A93A0");
    private static readonly ImageColor Blood = ImageColor.ParseHex("E05252");
    private static readonly ImageColor Victory = ImageColor.ParseHex("6FCF97");
    private static readonly ImageColor Partial = ImageColor.ParseHex("F2994A");

    private static readonly Lazy<(FontFamily Regular, FontFamily Bold)> Fonts = new(() =>
    {
        // Server packages omit the Fonts directory. Embed the two fonts used by the evacuation credits.
        var assembly = typeof(RoundEndDiscordManifest).Assembly;
        using var regular = assembly.GetManifestResourceStream("NotoSans-Regular.ttf")!;
        using var bold = assembly.GetManifestResourceStream("NotoSansDisplay-Bold.ttf")!;
        var fonts = new FontCollection();
        return (fonts.Add(regular), fonts.Add(bold));
    });

    private readonly Label[] _header;
    private readonly Label _resultTitle;
    private readonly Label[] _result;
    private readonly Label _antagTitle;
    private readonly Label[][] _antags;

    public int RoundId { get; }

    private RoundEndDiscordManifest(int roundId, Label[] header, Label resultTitle, Label[] result, Label antagTitle, Label[][] antags)
    {
        RoundId = roundId;
        _header = header;
        _resultTitle = resultTitle;
        _result = result;
        _antagTitle = antagTitle;
        _antags = antags;
    }

    public static RoundEndDiscordManifest Capture(RoundEndMessageEvent snapshot, IEnumerable<(string Text, bool IsOutcome)> report)
    {
        var mode = string.IsNullOrWhiteSpace(snapshot.GamemodeTitle)
            ? Loc.GetString("discord-round-notifications-unknown-mode")
            : snapshot.GamemodeTitle;
        Label[] header =
        [
            new(Loc.GetString("discord-round-credits-studio"), Dim, 14, Bold: true),
            new(Loc.GetString("discord-round-credits-round",
                ("id", snapshot.RoundId.ToString(CultureInfo.InvariantCulture)), ("gamemode", mode)), Text, 16),
        ];

        var reportLines = report
            .OrderByDescending(line => line.IsOutcome)
            .SelectMany(line => line.Text.ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(text => ReadReportLine(text, line.IsOutcome)))
            .Where(line => line.Text.Length > 0)
            .ToArray();

        var antags = snapshot.AllPlayersEndInfo
            .Where(player => player.ShowInAntagManifest)
            .OrderByDescending(player => player.ManifestKills)
            .ThenByDescending(player => player.ManifestAssists)
            .ThenBy(player => player.PlayerPseudonym ?? player.PlayerICName, StringComparer.CurrentCultureIgnoreCase)
            .Select(CaptureAntag)
            .ToArray();

        if (antags.Length == 0)
            antags = [[new(Loc.GetString("round-end-summary-window-antag-manifest-empty-title"), Dim, 16)]];

        var resultTitle = new Label(Loc.GetString("discord-round-credits-result").ToUpperInvariant(), Text, 24, Bold: true);
        var antagTitle = new Label(Loc.GetString("discord-round-credits-antagonists").ToUpperInvariant(), Blood, 24, Bold: true);
        return new RoundEndDiscordManifest(snapshot.RoundId, header, resultTitle, reportLines, antagTitle, antags);
    }

    private static Label ReadReportLine(string line, bool isOutcome)
    {
        var markup = FormattedMessage.FromMarkupPermissive(line);
        var color = markup.FirstOrDefault(node => node.Name == "color" && !node.Closing)?.Value.ColorValue;

        var ink = Text;
        if (color is { } tint)
        {
            if (tint.G > tint.R && tint.G > tint.B)
                ink = Victory;
            else if (tint.R > tint.G && tint.R > tint.B)
                ink = tint.G > 0.35f ? Partial : Blood;
        }

        return new Label(markup.ToString().Trim(), ink, isOutcome ? 30 : 15, Bold: isOutcome);
    }

    private static Label[] CaptureAntag(RoundEndMessageEvent.RoundEndPlayerInfo player)
    {
        var name = string.IsNullOrWhiteSpace(player.PlayerPseudonym) ? player.PlayerICName : player.PlayerPseudonym;
        if (string.IsNullOrWhiteSpace(name))
            name = Loc.GetString("generic-unknown-title");

        var roles = player.AntagRoleNames is { Length: > 0 }
            ? string.Join(", ", player.AntagRoleNames.Select(role => Loc.TryGetString(role, out var localized) ? localized : role))
            : Loc.GetString("game-ticker-unknown-role");

        var account = $"@{player.PlayerOOCName}";
        if (player.IsDead || player.InCustody)
        {
            var status = Loc.GetString(player.IsDead ? "discord-round-credits-dead" : "discord-round-credits-custody");
            account += $" · {status}";
        }

        var killsAndAssists = Loc.GetString("discord-round-credits-kills-assists",
            ("kills", player.ManifestKills), ("assists", player.ManifestAssists));

        return [new(name, Text, 20, Bold: true), new(roles, Blood, 14), new(account, Dim, 12), new(killsAndAssists, Dim, 12)];
    }

    public IEnumerable<WebhookAttachment> Render()
    {
        var header = Measure(_header, TextWidth);
        var resultTitle = Measure([_resultTitle], TextWidth, _resultTitle.Color);
        var antagTitle = Measure([_antagTitle], TextWidth, _antagTitle.Color);
        var page = new List<(Block Block, int X, int Y)>();
        var height = 0;
        var number = 1;

        StartPage();
        if (_result.Length > 0)
            AddSection(resultTitle);
        foreach (var label in _result)
        {
            var row = Measure([label], TextWidth);
            if (height + row.Height + 4 + Margin > MaxHeight)
            {
                yield return Draw(page, height + Margin, number++);
                StartPage();
                AddSection(resultTitle);
            }

            page.Add((row, Margin, height));
            height += row.Height + 4;
        }

        var needsSection = true;
        foreach (var row in _antags.Chunk(Columns))
        {
            var cells = row.Select(cell => Measure(cell, CellWidth)).ToArray();
            var rowHeight = cells.Max(cell => cell.Height);
            var sectionHeight = needsSection ? RowGap + antagTitle.Height + 12 : 0;
            if (height + sectionHeight + rowHeight + Margin > MaxHeight)
            {
                yield return Draw(page, height + Margin - (needsSection ? 0 : RowGap), number++);
                StartPage();
                needsSection = true;
            }

            if (needsSection)
            {
                AddSection(antagTitle);
                needsSection = false;
            }

            var left = (Width - cells.Length * CellWidth - (cells.Length - 1) * ColumnGap) / 2;
            foreach (var cell in cells)
            {
                page.Add((cell, left, height));
                left += CellWidth + ColumnGap;
            }

            height += rowHeight + RowGap;
        }

        yield return Draw(page, height + Margin - RowGap, number);

        void StartPage()
        {
            page.Clear();
            page.Add((header, Margin, Margin));
            height = Margin + header.Height;
        }

        void AddSection(Block title)
        {
            height += RowGap;
            page.Add((title, Margin, height));
            height += title.Height + 12;
        }
    }

    private static Block Measure(Label[] labels, int width, ImageColor? underline = null)
    {
        var lines = new Line[labels.Length];
        var height = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            var text = label.Text.ReplaceLineEndings(" ");
            // Bound custom text before shaping it so no single entry can exceed a full page.
            const int maxLength = 160;
            if (text.Length > maxLength)
                text = text[..(char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength)] + "…";

            var fonts = Fonts.Value;
            var font = label.Bold ? fonts.Bold.CreateFont(label.Size, FontStyle.Bold) : fonts.Regular.CreateFont(label.Size);
            var options = new RichTextOptions(font)
            {
                Dpi = 96,
                TextAlignment = TextAlignment.Center,
                WrappingLength = width,
                WordBreaking = WordBreaking.BreakWord,
            };
            var bounds = TextMeasurer.MeasureSize(text, options);
            while (bounds.Height > label.Size * 5f && text.Length > 2)
            {
                var half = text.Length / 2;
                text = text[..(char.IsHighSurrogate(text[half - 1]) ? half - 1 : half)] + "…";
                bounds = TextMeasurer.MeasureSize(text, options);
            }

            lines[i] = new Line(text, label.Color, options, height);
            height += (int) MathF.Ceiling(MathF.Max(bounds.Height, label.Size * 1.6f)) + 3;
        }

        return new Block(lines, height + (underline.HasValue ? 8 : 0), underline);
    }

    private WebhookAttachment Draw(List<(Block Block, int X, int Y)> page, int height, int number)
    {
        var configuration = Configuration.Default.Clone();
        configuration.MaxDegreeOfParallelism = 1;
        var image = new Image<Rgb24>(configuration, Width, height);
        try
        {
            var random = new Random(unchecked(RoundId * 31 + number));
            for (var i = 0; i < 180; i++)
            {
                var x = random.Next(Width - 1);
                var y = random.Next(height - 1);
                var luminance = random.Next(16, 111);
                var star = new Rgb24((byte) (luminance * 0.87), (byte) (luminance * 0.94), (byte) luminance);
                image[x, y] = star;
                if (i % 5 == 0)
                {
                    image[x + 1, y] = star;
                    image[x, y + 1] = star;
                }
            }

            image.Mutate(context =>
            {
                foreach (var (block, x, y) in page)
                {
                    foreach (var line in block.Lines)
                    {
                        line.Options.Origin = new PointF(x, y + line.Y);
                        context.DrawText(line.Options, line.Text, line.Color);
                    }

                    if (block.Underline is { } color)
                        context.Fill(color.WithAlpha(0.6f), new Rectangle((Width - 120) / 2, y + block.Height - 2, 120, 2));
                }
            });

            using var stream = new MemoryStream();
            image.SaveAsPng(stream, new PngEncoder { CompressionLevel = PngCompressionLevel.Level1 });
            return new WebhookAttachment($"round-{RoundId}-manifest-{number}.png", "image/png", stream.ToArray());
        }
        finally
        {
            image.Dispose();
        }
    }

    private readonly record struct Label(string Text, ImageColor Color, int Size, bool Bold = false);
    private readonly record struct Line(string Text, ImageColor Color, RichTextOptions Options, int Y);
    private readonly record struct Block(Line[] Lines, int Height, ImageColor? Underline);
}
