// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldCategoryTab : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int BaseFontSize = 9;

    private Font _font = default!;
    private string _text = "";
    private bool _isActive;

    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _normalColor = Color.FromHex("#8d7aaa");
    private readonly Color _hoverColor = Color.FromHex("#a589c9");
    private readonly Color _activeColor = Color.FromHex("#00FFAA");
    private readonly Color _activeBgColor = Color.FromHex("#1a2a2a");

    private bool _hovered;

    public event Action? OnPressed;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            InvalidateMeasure();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            InvalidateMeasure();
        }
    }

    public EmeraldCategoryTab()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(
            _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf"),
            BaseFontSize);
        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var textWidth = GetTextWidthLogical(_text);
        var width = Math.Max(60f, textWidth + 16f);
        return new Vector2(width, 28f);
    }

    private float GetTextWidthLogical(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var metrics = _font.GetCharMetrics(rune, 1f);
            if (metrics.HasValue)
                width += metrics.Value.Advance;
        }
        return width;
    }

    private float GetTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var metrics = _font.GetCharMetrics(rune, UIScale);
            if (metrics.HasValue)
                width += metrics.Value.Advance;
        }
        return width;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        var bgColor = _isActive ? _activeBgColor : _bgColor;
        handle.DrawRect(rect, bgColor.WithAlpha(0.6f));

        if (_isActive)
        {
            var lineHeight = 2f * UIScale;
            var bottomLine = new UIBox2(rect.Left, rect.Bottom - lineHeight, rect.Right, rect.Bottom);
            handle.DrawRect(bottomLine, _activeColor);
        }
        else
        {
            handle.DrawLine(rect.BottomLeft, rect.BottomRight, _borderColor);
        }

        var textColor = _isActive ? _activeColor : _hovered ? _hoverColor : _normalColor;
        var textWidth = GetTextWidth(_text);
        var textX = (PixelSize.X - textWidth) / 2f;
        var textY = (PixelSize.Y - _font.GetLineHeight(UIScale)) / 2f;

        handle.DrawString(_font, new Vector2(textX, textY), _text, UIScale, textColor);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
        InvalidateMeasure();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
        InvalidateMeasure();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            OnPressed?.Invoke();
            args.Handle();
        }
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
