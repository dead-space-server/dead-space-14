// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldLevelBar : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int BaseFontSize = 11;
    private const int SmallFontSize = 9;
    private const float BaseBarHeight = 22f;
    private const float BasePadding = 8f;

    private Font _font = default!;
    private Font _smallFont = default!;

    private int _level;
    private int _experience;
    private int _requiredExp;
    private int _toNextLevel;
    private float _progress;

    private readonly Color _textColor = Color.FromHex("#c0b3da");
    private readonly Color _levelColor = Color.FromHex("#00FFAA");
    private readonly Color _fillColor = Color.FromHex("#a589c9");
    private readonly Color _fillGlowColor = Color.FromHex("#d4c5e8");
    private readonly Color _emptyColor = Color.FromHex("#1a0f2e");
    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _borderColor = Color.FromHex("#6d5a8a");

    public int Level
    {
        get => _level;
        set
        {
            _level = value;
            InvalidateMeasure();
        }
    }

    public int Experience
    {
        get => _experience;
        set
        {
            _experience = value;
            InvalidateMeasure();
        }
    }

    public int RequiredExp
    {
        get => _requiredExp;
        set
        {
            _requiredExp = value;
            InvalidateMeasure();
        }
    }

    public int ToNextLevel
    {
        get => _toNextLevel;
        set
        {
            _toNextLevel = value;
            InvalidateMeasure();
        }
    }

    public float Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0f, 1f);
            InvalidateMeasure();
        }
    }

    public EmeraldLevelBar()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _font = new VectorFont(fontRes, BaseFontSize);
        _smallFont = new VectorFont(fontRes, SmallFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 200 : availableSize.X;
        var height = BaseBarHeight + BasePadding * 2;
        return new Vector2(Math.Max(80, width), height);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.8f));

        var padding = BasePadding * UIScale;
        var barHeight = BaseBarHeight * UIScale;
        var barX = padding;
        var barY = padding;
        var barWidth = PixelSize.X - padding * 2;
        var barRect = new UIBox2(barX, barY, barX + barWidth, barY + barHeight);

        handle.DrawRect(barRect, _emptyColor.WithAlpha(0.4f));

        if (_progress > 0)
        {
            var fillWidth = barWidth * _progress;
            var fillRect = new UIBox2(barX, barY, barX + fillWidth, barY + barHeight);

            var glowOffset = 1f * UIScale;
            var glowRect = new UIBox2(barX - glowOffset, barY - glowOffset, barX + fillWidth + glowOffset, barY + barHeight + glowOffset);
            handle.DrawRect(glowRect, _fillGlowColor.WithAlpha(0.2f));

            handle.DrawRect(fillRect, _fillColor.WithAlpha(0.6f));

            var edgeWidth = 2f * UIScale;
            var edgeRect = new UIBox2(barX + fillWidth - edgeWidth, barY, barX + fillWidth, barY + barHeight);
            handle.DrawRect(edgeRect, _fillColor.WithAlpha(1f));
        }

        var levelText = $"УР {_level}";
        var levelX = barX + 8f * UIScale;
        var levelY = barY + (barHeight - _font.GetLineHeight(UIScale)) / 2f;

        handle.DrawString(_font, new Vector2(levelX, levelY), levelText, UIScale, _levelColor);

        var expText = $"{_experience} / {_requiredExp}";
        var expWidth = GetTextWidth(expText, _smallFont);
        var expX = barX + barWidth - expWidth - 8f * UIScale;
        var expY = barY + (barHeight - _smallFont.GetLineHeight(UIScale)) / 2f;

        handle.DrawString(_smallFont, new Vector2(expX, expY), expText, UIScale, _textColor);
    }

    private float GetTextWidth(string text, Font font)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var metrics = font.GetCharMetrics(rune, UIScale);
            if (metrics.HasValue)
                width += metrics.Value.Advance;
        }
        return width;
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
