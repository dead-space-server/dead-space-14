// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldCalendarHeader : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int TitleFontSize = 14;
    private const int SubtitleFontSize = 9;
    private const int DayFontSize = 10;

    private Font _titleFont = default!;
    private Font _subtitleFont = default!;
    private Font _dayFont = default!;

    private int _currentDay;
    private int _totalDays;
    private bool _isPremiumTrack;
    private string _calendarName = "";

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _titleColor = Color.FromHex("#c0b3da");
    private readonly Color _subtitleColor = Color.FromHex("#8d7aaa");
    private readonly Color _dayColor = Color.FromHex("#00FFAA");
    private readonly Color _premiumColor = Color.FromHex("#ffd700");
    private readonly Color _premiumBgColor = Color.FromHex("#2a1a3e");
    private readonly Color _calendarNameColor = Color.FromHex("#d4a574");

    public int CurrentDay
    {
        get => _currentDay;
        set
        {
            _currentDay = value;
            InvalidateMeasure();
        }
    }

    public int TotalDays
    {
        get => _totalDays;
        set
        {
            _totalDays = value;
            InvalidateMeasure();
        }
    }

    public bool IsPremiumTrack
    {
        get => _isPremiumTrack;
        set
        {
            _isPremiumTrack = value;
            InvalidateMeasure();
        }
    }

    public string CalendarName
    {
        get => _calendarName;
        set
        {
            _calendarName = value;
            InvalidateMeasure();
        }
    }

    public EmeraldCalendarHeader()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _titleFont = new VectorFont(fontRes, TitleFontSize);
        _subtitleFont = new VectorFont(fontRes, SubtitleFontSize);
        _dayFont = new VectorFont(fontRes, DayFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 400 : availableSize.X;
        return new Vector2(width, 55);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        var bgColor = _isPremiumTrack ? _premiumBgColor : _bgColor;
        handle.DrawRect(rect, bgColor.WithAlpha(0.9f));

        var borderColor = _isPremiumTrack ? _premiumColor : _borderColor;
        DrawBorder(handle, rect, borderColor);

        var padding = 12f * UIScale;
        var currentY = 8f * UIScale;

        var titleText = _isPremiumTrack ? "PREMIUM НАГРАДЫ" : "НАГРАДЫ";
        var titleColor = _isPremiumTrack ? _premiumColor : _titleColor;
        handle.DrawString(_titleFont, new Vector2(padding, currentY), titleText, UIScale, titleColor);

        if (!string.IsNullOrEmpty(_calendarName) && !_isPremiumTrack)
        {
            var nameWidth = GetTextWidth(_calendarName.ToUpper(), _subtitleFont);
            var nameX = (PixelSize.X - nameWidth) / 2f;
            handle.DrawString(_subtitleFont, new Vector2(nameX, currentY + 2f * UIScale), _calendarName.ToUpper(), UIScale, _calendarNameColor);
        }

        currentY += _titleFont.GetLineHeight(UIScale) + 4f * UIScale;

        var subtitleText = _isPremiumTrack
            ? "Эксклюзивные награды для Premium"
            : "Заходите каждый день";
        var subtitleWidth = GetTextWidth(subtitleText, _subtitleFont);
        handle.DrawString(_subtitleFont, new Vector2(padding, currentY), subtitleText, UIScale, _subtitleColor);
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

public sealed partial class EmeraldPremiumLockedCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int TitleFontSize = 12;
    private const int MessageFontSize = 10;

    private Font _titleFont = default!;
    private Font _messageFont = default!;

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#ffd700");
    private readonly Color _titleColor = Color.FromHex("#ffd700");
    private readonly Color _messageColor = Color.FromHex("#8d7aaa");
    private readonly Color _lockColor = Color.FromHex("#d4a574");

    private EmeraldButton _buyButton = default!;

    public event Action? OnBuyPremiumPressed;

    public EmeraldPremiumLockedCard()
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
            MinSize = new Vector2(180, 0)
        };
        _buyButton.OnPressed += () => OnBuyPremiumPressed?.Invoke();
        AddChild(_buyButton);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 400 : availableSize.X;
        _buyButton?.Measure(availableSize);
        return new Vector2(width, 120);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_buyButton != null)
        {
            var buttonSize = _buyButton.DesiredSize;
            var buttonX = (finalSize.X - buttonSize.X) / 2f;
            var buttonY = finalSize.Y - buttonSize.Y - 12f;
            _buyButton.Arrange(new UIBox2(buttonX, buttonY, buttonX + buttonSize.X, buttonY + buttonSize.Y));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.9f));
        DrawBorder(handle, rect, _borderColor);

        var padding = 16f * UIScale;
        var currentY = 12f * UIScale;

        var titleText = "PREMIUM НАГРАДЫ ЗАБЛОКИРОВАНЫ";
        var titleWidth = GetTextWidth(titleText, _titleFont);
        var titleX = (PixelSize.X - titleWidth) / 2f;
        handle.DrawString(_titleFont, new Vector2(titleX, currentY), titleText, UIScale, _titleColor);

        currentY += _titleFont.GetLineHeight(UIScale) + 4f * UIScale;

        var line1 = "Получите Premium для доступа";
        var line1Width = GetTextWidth(line1, _messageFont);
        var line1X = (PixelSize.X - line1Width) / 2f;
        handle.DrawString(_messageFont, new Vector2(line1X, currentY), line1, UIScale, _messageColor);
    }

    private void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        var thickness = Math.Max(1f, 2f * UIScale);
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

public sealed partial class EmeraldTodayRewardCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int TitleFontSize = 10;
    private const int ItemFontSize = 9;
    private const int StatusFontSize = 8;

    private Font _titleFont = default!;
    private Font _itemFont = default!;
    private Font _statusFont = default!;

    private string _itemName = "";
    private string _statusText = "";
    private bool _isAvailable;
    private bool _isPremium;

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _titleColor = Color.FromHex("#c0b3da");
    private readonly Color _itemColor = Color.FromHex("#8d7aaa");
    private readonly Color _availableColor = Color.FromHex("#00FFAA");
    private readonly Color _claimedColor = Color.FromHex("#4CAF50");
    private readonly Color _premiumColor = Color.FromHex("#ffd700");

    public string ItemName
    {
        get => _itemName;
        set
        {
            _itemName = value;
            InvalidateMeasure();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            InvalidateMeasure();
        }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            _isAvailable = value;
            InvalidateMeasure();
        }
    }

    public bool IsPremium
    {
        get => _isPremium;
        set
        {
            _isPremium = value;
            InvalidateMeasure();
        }
    }

    public EmeraldTodayRewardCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _titleFont = new VectorFont(fontRes, TitleFontSize);
        _itemFont = new VectorFont(fontRes, ItemFontSize);
        _statusFont = new VectorFont(fontRes, StatusFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 200 : availableSize.X;
        return new Vector2(width, 55);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.8f));

        var borderColor = _isPremium ? _premiumColor : _isAvailable ? _availableColor : _borderColor;
        DrawBorder(handle, rect, borderColor);

        var padding = 8f * UIScale;
        var currentY = 6f * UIScale;

        var titleText = _isPremium ? "СЕГОДНЯ (PREMIUM)" : "СЕГОДНЯ";
        var titleColor = _isPremium ? _premiumColor : _titleColor;
        handle.DrawString(_titleFont, new Vector2(padding, currentY), titleText, UIScale, titleColor);

        currentY += _titleFont.GetLineHeight(UIScale) + 2f * UIScale;

        handle.DrawString(_itemFont, new Vector2(padding, currentY), _itemName, UIScale, _itemColor);

        currentY += _itemFont.GetLineHeight(UIScale) + 2f * UIScale;

        var statusColor = _isAvailable ? _availableColor : _claimedColor;
        handle.DrawString(_statusFont, new Vector2(padding, currentY), _statusText, UIScale, statusColor);
    }

    private void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        handle.DrawLine(rect.TopLeft, rect.TopRight, color);
        handle.DrawLine(rect.TopRight, rect.BottomRight, color);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, color);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, color);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
