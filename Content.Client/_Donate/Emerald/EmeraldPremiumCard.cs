// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldPremiumCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int TitleFontSize = 11;
    private const int NameFontSize = 14;
    private const int InfoFontSize = 10;
    private const int BonusFontSize = 11;

    private Font _titleFont = default!;
    private Font _nameFont = default!;
    private Font _infoFont = default!;
    private Font _bonusFont = default!;

    private bool _isActive;
    private int _level;
    private string _premName = "";
    private string _description = "";
    private float _bonusXp;
    private float _bonusEnergy;
    private int _bonusSlots;
    private int _expiresIn;

    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _activeBgColor = Color.FromHex("#1a0f2e");
    private readonly Color _activeAccentColor = Color.FromHex("#d4a574");
    private readonly Color _inactiveColor = Color.FromHex("#6d5a8a");
    private readonly Color _nameActiveColor = Color.FromHex("#d4c5e8");
    private readonly Color _infoColor = Color.FromHex("#8d7aaa");
    private readonly Color _bonusColor = Color.FromHex("#c0b3da");
    private readonly Color _levelColor = Color.FromHex("#d4a574");

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            InvalidateMeasure();
        }
    }

    public int Level
    {
        get => _level;
        set
        {
            _level = value;
            InvalidateMeasure();
        }
    }

    public string PremName
    {
        get => _premName;
        set
        {
            _premName = value;
            InvalidateMeasure();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            InvalidateMeasure();
        }
    }

    public float BonusXp
    {
        get => _bonusXp;
        set
        {
            _bonusXp = value;
            InvalidateMeasure();
        }
    }

    public float BonusEnergy
    {
        get => _bonusEnergy;
        set
        {
            _bonusEnergy = value;
            InvalidateMeasure();
        }
    }

    public int BonusSlots
    {
        get => _bonusSlots;
        set
        {
            _bonusSlots = value;
            InvalidateMeasure();
        }
    }

    public int ExpiresIn
    {
        get => _expiresIn;
        set
        {
            _expiresIn = value;
            InvalidateMeasure();
        }
    }

    public EmeraldPremiumCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _titleFont = new VectorFont(fontRes, TitleFontSize);
        _nameFont = new VectorFont(fontRes, NameFontSize);
        _infoFont = new VectorFont(fontRes, InfoFontSize);
        _bonusFont = new VectorFont(fontRes, BonusFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 400 : availableSize.X;
        return new Vector2(width, 110);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        var bgColor = _isActive ? _activeBgColor : _bgColor;
        handle.DrawRect(rect, bgColor.WithAlpha(0.9f));

        if (_isActive)
        {
            var accentHeight = 2f * UIScale;
            var accentLine = new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + accentHeight);
            handle.DrawRect(accentLine, _activeAccentColor.WithAlpha(0.8f));
        }

        var borderColor = _isActive ? _activeAccentColor : _inactiveColor;
        DrawBorder(handle, rect, borderColor);

        var padding = 14f * UIScale;
        var currentY = padding;

        var statusText = _isActive ? "АКТИВЕН" : "НЕАКТИВЕН";
        var statusColor = _isActive ? _activeAccentColor : _inactiveColor;
        var statusWidth = GetTextWidth(statusText, _titleFont);
        var statusX = PixelSize.X - padding - statusWidth;

        handle.DrawString(_titleFont, new Vector2(statusX, currentY), statusText, UIScale, statusColor);

        var levelText = $"УРОВЕНЬ {_level}";
        handle.DrawString(_titleFont, new Vector2(padding, currentY), levelText, UIScale, _levelColor);

        currentY += _titleFont.GetLineHeight(UIScale) + 8f * UIScale;

        var nameColor = _isActive ? _nameActiveColor : _inactiveColor;
        handle.DrawString(_nameFont, new Vector2(padding, currentY), _premName, UIScale, nameColor);

        currentY += _nameFont.GetLineHeight(UIScale) + 2f * UIScale;

        if (_isActive && _expiresIn > 0)
        {
            var expiresText = $"Истекает через {_expiresIn} дней";
            var expiresColor = _expiresIn < 7 ? _activeAccentColor : _infoColor;
            handle.DrawString(_infoFont, new Vector2(padding, currentY), expiresText, UIScale, expiresColor);
        }

        currentY += _infoFont.GetLineHeight(UIScale) + 8f * UIScale;

        var bonusesX = padding;
        var bonusSpacing = Math.Min(160f * UIScale, (PixelSize.X - padding * 2) / 3f);

        if (_bonusXp > 0)
        {
            var bonusXpPercent = (_bonusXp - 1f) * 100f;
            var xpText = $"+{bonusXpPercent:F0}% ОПЫТА";
            handle.DrawString(_bonusFont, new Vector2(bonusesX, currentY), xpText, UIScale, _bonusColor);
            bonusesX += bonusSpacing;
        }

        if (_bonusEnergy > 0)
        {
            var bonusEnergyPercent = (_bonusEnergy - 1f) * 100f;
            var energyText = $"+{bonusEnergyPercent:F0}% ЭНЕРГИИ";
            handle.DrawString(_bonusFont, new Vector2(bonusesX, currentY), energyText, UIScale, _bonusColor);
            bonusesX += bonusSpacing;
        }

        if (_bonusSlots > 0)
        {
            var slotsText = $"+{_bonusSlots} СЛОТОВ";
            handle.DrawString(_bonusFont, new Vector2(bonusesX, currentY), slotsText, UIScale, _bonusColor);
        }
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
