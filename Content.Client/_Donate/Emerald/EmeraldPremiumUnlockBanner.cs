using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldPremiumUnlockBanner : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int TitleFontSize = 11;
    private const int MessageFontSize = 9;

    private Font _titleFont = default!;
    private Font _messageFont = default!;

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#ffd700");
    private readonly Color _titleColor = Color.FromHex("#ffd700");
    private readonly Color _messageColor = Color.FromHex("#c0b3da");
    private readonly Color _accentColor = Color.FromHex("#d4a574");

    private EmeraldButton _buyButton = default!;

    public event Action? OnBuyPremiumPressed;

    public EmeraldPremiumUnlockBanner()
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
            Text = "ПОЛУЧИТЬ PREMIUM",
            MinSize = new Vector2(160, 0)
        };
        _buyButton.OnPressed += () => OnBuyPremiumPressed?.Invoke();
        AddChild(_buyButton);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 400 : availableSize.X;
        _buyButton?.Measure(availableSize);
        return new Vector2(width, 70);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_buyButton != null)
        {
            var buttonSize = _buyButton.DesiredSize;
            var buttonX = finalSize.X - buttonSize.X - 12f;
            var buttonY = (finalSize.Y - buttonSize.Y) / 2f;
            _buyButton.Arrange(new UIBox2(buttonX, buttonY, buttonX + buttonSize.X, buttonY + buttonSize.Y));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.95f));

        var thickness = 2f * UIScale;
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + thickness), _borderColor);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - thickness, rect.Right, rect.Bottom), _borderColor);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + thickness, rect.Bottom), _borderColor);
        handle.DrawRect(new UIBox2(rect.Right - thickness, rect.Top, rect.Right, rect.Bottom), _borderColor);

        var padding = 12f * UIScale;
        var currentY = 12f * UIScale;

        var titleText = "ХОТИТЕ БОЛЬШЕ НАГРАД?";
        handle.DrawString(_titleFont, new Vector2(padding, currentY), titleText, UIScale, _titleColor);

        currentY += _titleFont.GetLineHeight(UIScale) + 4f * UIScale;

        var messageText = "Получите Premium и забирайте эксклюзивные награды каждый день!";
        handle.DrawString(_messageFont, new Vector2(padding, currentY), messageText, UIScale, _messageColor);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
