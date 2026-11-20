// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldBuyPremiumCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int TitleFontSize = 14;
    private const int MessageFontSize = 11;

    private Font _titleFont = default!;
    private Font _messageFont = default!;

    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _borderColor = Color.FromHex("#6d5a8a");
    private readonly Color _titleColor = Color.FromHex("#d4a574");
    private readonly Color _messageColor = Color.FromHex("#8d7aaa");

    private EmeraldButton _buyButton = default!;

    public event Action? OnBuyPressed;

    public EmeraldBuyPremiumCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _titleFont = new VectorFont(fontRes, TitleFontSize);
        _messageFont = new VectorFont(fontRes, MessageFontSize);

        BuildUI();
    }

    private void BuildUI()
    {
        _buyButton = new EmeraldButton
        {
            Text = "КУПИТЬ ПРЕМИУМ",
            MinSize = new Vector2(160, 0)
        };
        _buyButton.OnPressed += () => OnBuyPressed?.Invoke();
        AddChild(_buyButton);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 400 : availableSize.X;

        if (_buyButton != null)
        {
            _buyButton.Measure(availableSize);
        }

        return new Vector2(width, 130);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_buyButton != null)
        {
            var buttonSize = _buyButton.DesiredSize;
            var buttonX = (finalSize.X - buttonSize.X) / 2f;
            var buttonY = finalSize.Y - buttonSize.Y - 14f;
            _buyButton.Arrange(new UIBox2(buttonX, buttonY, buttonX + buttonSize.X, buttonY + buttonSize.Y));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.6f));
        DrawBorder(handle, rect, _borderColor);

        var padding = 14f * UIScale;
        var currentY = padding;

        var line1Text = "У ВАС НЕТ ПРЕМИУМА";
        var line1Width = GetTextWidth(line1Text, _titleFont);
        var line1X = (PixelSize.X - line1Width) / 2f;

        handle.DrawString(_titleFont, new Vector2(line1X, currentY), line1Text, UIScale, _titleColor);
        currentY += _titleFont.GetLineHeight(UIScale) + 4f * UIScale;

        var line2Text = "У ВАС НЕТУ БОНУСОВ =(";
        var line2Width = GetTextWidth(line2Text, _messageFont);
        var line2X = (PixelSize.X - line2Width) / 2f;

        handle.DrawString(_messageFont, new Vector2(line2X, currentY), line2Text, UIScale, _messageColor);
        currentY += _messageFont.GetLineHeight(UIScale) + 4f * UIScale;

        var line3Text = "ИСПРАВЬТЕ ЭТО!";
        var line3Width = GetTextWidth(line3Text, _messageFont);
        var line3X = (PixelSize.X - line3Width) / 2f;

        handle.DrawString(_messageFont, new Vector2(line3X, currentY), line3Text, UIScale, _messageColor);
    }

    private void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        var thickness = Math.Max(1f, 1f * UIScale);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + thickness), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - thickness, rect.Right, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + thickness, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Right - thickness, rect.Top, rect.Right, rect.Bottom), color);
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
