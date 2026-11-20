// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.Audio.NowPlaying;

public sealed class VinylNowPlayingControl : Control
{
    private const float LabelDiameterRatio = 0.461f;
    private const float HoleDiameterRatio = 0.445f;

    private const float HoleRadiusRatio = 0.015f;

    private static readonly Color TitleColor = Color.FromHex("#F8F1E4");
    private static readonly Color ArtistColor = Color.FromHex("#E7C279");
    private static readonly Color CaptionShadow = new(0f, 0f, 0f, 0.85f);

    private readonly Font _titleFont;
    private readonly Font _artistFont;

    public Texture? DiscTexture { get; set; }

    public Texture? LabelTexture { get; set; }

    public Texture? CoverTexture { get; set; }

    public float DiscAngle { get; set; }

    public string TrackTitle { get; set; } = string.Empty;

    public string TrackAuthor { get; set; } = string.Empty;

    public VinylNowPlayingControl(Font titleFont, Font artistFont)
    {
        _titleFont = titleFont;
        _artistFont = artistFont;
        MouseFilter = MouseFilterMode.Ignore;
        Visible = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var width = PixelSize.X;
        var height = PixelSize.Y;
        if (width < 1 || height < 1)
            return;

        var size = new Vector2(width, height);
        var center = size / 2f;

        var labelSize = size.X * LabelDiameterRatio;
        var labelHalf = new Vector2(labelSize / 2f);

        var transform = handle.GetTransform();
        var rotated = Matrix3x2.CreateRotation(DiscAngle)
            * Matrix3x2.CreateTranslation(center)
            * transform;

        if (CoverTexture is { } cover)
            handle.DrawTextureRect(cover, new UIBox2(center - labelHalf, center + labelHalf));

        if (DiscTexture is { } disc)
        {
            handle.SetTransform(rotated);
            handle.DrawTextureRect(disc, UIBox2.FromDimensions(-center, size));
            handle.SetTransform(transform);
        }

        if (CoverTexture != null)
            handle.DrawCircle(center, size.X * HoleDiameterRatio / 2f, new Color(0f, 0f, 0f, 0.22f));

        if (CoverTexture == null && LabelTexture is { } label)
        {
            handle.SetTransform(rotated);
            handle.DrawTextureRect(label, new UIBox2(-labelHalf, labelHalf));
            handle.SetTransform(transform);
        }

        DrawSpindle(handle, center, size);
        DrawCaption(handle, size, center);
    }

    private static void DrawSpindle(DrawingHandleScreen handle, Vector2 center, Vector2 size)
    {
        var radius = size.X * HoleRadiusRatio;

        handle.DrawCircle(center, radius, new Color(0.02f, 0.02f, 0.03f));
        handle.DrawCircle(center, radius + 0.5f, new Color(0.80f, 0.80f, 0.86f, 0.35f), false);
    }

    private void DrawCaption(DrawingHandleScreen handle, Vector2 size, Vector2 center)
    {
        if (string.IsNullOrEmpty(TrackTitle) && string.IsNullOrEmpty(TrackAuthor))
            return;

        var hasCover = CoverTexture != null;
        var scale = UIScale;
        var maxWidth = size.X * (hasCover ? 0.60f : 0.62f);

        var titleLines = WrapText(handle, _titleFont, TrackTitle, maxWidth, scale, hasCover ? 2 : 3);
        var authorLines = WrapText(handle, _artistFont, TrackAuthor, maxWidth, scale, hasCover ? 1 : 2);

        var titleLineHeight = _titleFont.GetLineHeight(scale);
        var authorLineHeight = _artistFont.GetLineHeight(scale);
        var gap = authorLines.Count > 0 ? authorLineHeight * 0.2f : 0f;

        var height = titleLines.Count * titleLineHeight
            + (authorLines.Count > 0 ? gap + authorLines.Count * authorLineHeight : 0f);

        var centerOffset = hasCover ? size.Y * 0.055f : -size.Y * 0.02f;
        var y = center.Y - height / 2f + centerOffset;

        foreach (var line in titleLines)
        {
            DrawCenteredLine(handle, _titleFont, line, center.X, y, scale, TitleColor);
            y += titleLineHeight;
        }

        if (authorLines.Count == 0)
            return;

        y += gap;

        foreach (var line in authorLines)
        {
            DrawCenteredLine(handle, _artistFont, line, center.X, y, scale, ArtistColor);
            y += authorLineHeight;
        }
    }

    private static void DrawCenteredLine(
        DrawingHandleScreen handle,
        Font font,
        string line,
        float centerX,
        float top,
        float scale,
        Color color)
    {
        var width = handle.GetDimensions(font, line, scale).X;
        var position = new Vector2(centerX - width / 2f, top);

        handle.DrawString(font, position + new Vector2(scale, scale), line, scale, CaptionShadow);
        handle.DrawString(font, position, line, scale, color);
    }

    private static List<string> WrapText(
        DrawingHandleScreen handle,
        Font font,
        string text,
        float maxWidth,
        float scale,
        int maxLines)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
            return lines;

        var words = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && handle.GetDimensions(font, candidate, scale).X > maxWidth)
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
            else
            {
                current.Clear();
                current.Append(candidate);
            }
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        if (lines.Count <= maxLines)
            return lines;

        lines.RemoveRange(maxLines, lines.Count - maxLines);

        var last = lines[^1];
        while (last.Length > 0 && handle.GetDimensions(font, last + "…", scale).X > maxWidth)
            last = last[..^1];

        lines[^1] = $"{last.TrimEnd()}…";
        return lines;
    }
}
