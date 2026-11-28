// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared.DeadSpace.PlanetSurvey;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.PlanetSurvey;

public sealed class SurveyObjectivesOverlay : Overlay
{
    private const float Width = 300f;
    private const float MinWidth = 170f;
    private const float CompactBelow = 900f;
    private const float Pad = 6f;
    private const float Top = 210f;

    private static readonly Color Panel = Color.FromHex("#0c1220").WithAlpha(0.6f);
    private static readonly Color Edge = Color.FromHex("#3d6fae");
    private static readonly Color TitleColor = Color.FromHex("#9cc8ff");
    private static readonly Color OpenColor = Color.FromHex("#e6e6e6");
    private static readonly Color DoneColor = Color.FromHex("#7fcf8f");
    private static readonly Color ThreatColor = Color.FromHex("#ffb066");

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly Font _title;
    private readonly Font _text;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public SurveyObjectivesOverlay(IEntityManager entMan, IPlayerManager player, IResourceCache cache)
    {
        _entMan = entMan;
        _player = player;
        _title = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 12);
        _text = cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 11);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } local && _entMan.HasComponent<SurveyObjectivesComponent>(local);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent<SurveyObjectivesComponent>(local, out var objectives))
        {
            return;
        }

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var compact = bounds.Width < CompactBelow;
        var width = Math.Clamp(bounds.Width * 0.24f, MinWidth, Width);
        var x = bounds.Right - width - 8f;
        var top = bounds.Top + Math.Min(Top, bounds.Height * 0.16f);
        var titleHeight = _title.GetLineHeight(1f);
        var lineHeight = _text.GetLineHeight(1f);

        var wrapped = new List<(string Text, Color Color)>();
        foreach (var line in objectives.Lines)
        {
            if (compact && line.Done)
                continue;

            var color = line.Done ? DoneColor : OpenColor;
            var prefix = line.Done ? "[+] " : "[ ] ";
            var parts = Wrap(prefix + line.Text, width - Pad * 2).ToList();
            if (compact && parts.Count > 2)
                parts = new List<string> { parts[0], parts[1].TrimEnd() + "…" };

            foreach (var part in parts)
            {
                wrapped.Add((part, color));
            }
        }

        var maxLines = (int) ((bounds.Height * 0.5f - titleHeight * 2) / lineHeight);
        if (wrapped.Count > maxLines && maxLines > 0)
            wrapped = wrapped.Take(maxLines).ToList();

        var status = Wrap(objectives.Threat, width - Pad * 2).ToList();
        var height = Pad * 2 + titleHeight + status.Count * lineHeight + 4f + wrapped.Count * lineHeight;
        var box = new UIBox2(x, top, x + width, top + height);
        handle.DrawRect(box, Panel);
        handle.DrawRect(box, Edge, false);

        var pos = new Vector2(x + Pad, box.Top + Pad);
        handle.DrawString(_title, pos, objectives.Title, TitleColor);
        pos.Y += titleHeight;
        foreach (var line in status)
        {
            handle.DrawString(_text, pos, line, ThreatColor);
            pos.Y += lineHeight;
        }

        pos.Y += 4f;

        foreach (var (text, color) in wrapped)
        {
            handle.DrawString(_text, pos, text, color);
            pos.Y += lineHeight;
        }
    }

    private IEnumerable<string> Wrap(string text, float width)
    {
        var words = text.Split(' ');
        var line = string.Empty;
        foreach (var word in words)
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(next) > width)
            {
                yield return line;
                line = "    " + word;
                continue;
            }

            line = next;
        }

        if (line.Length > 0)
            yield return line;
    }

    private float Measure(string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (_text.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }
}
