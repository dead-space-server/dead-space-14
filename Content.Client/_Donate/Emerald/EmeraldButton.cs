// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Donate.Emerald;

[Virtual]
public partial class EmeraldButton : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int BaseFontSize = 12;

    private Font _font = default!;
    private string _text = "";
    private bool _hovered;
    private bool _pressed;
    private bool _disabled;
    private bool _isActive;

    private readonly Color _normalColor = Color.FromHex("#a589c9");
    private readonly Color _hoverColor = Color.FromHex("#c0b3da");
    private readonly Color _pressColor = Color.FromHex("#e8e8e8");
    private readonly Color _disabledColor = Color.FromHex("#4a3a5a");
    private readonly Color _glowColor = Color.FromHex("#d4c5e8");
    private readonly Color _activeColor = Color.FromHex("#00FFAA");
    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#6d5a8a");

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

    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
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

    public EmeraldButton()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;

        _font = new VectorFont(
            _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf"),
            BaseFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var textWidth = GetTextWidthLogical(_text.ToUpper());
        var paddingX = 16f;
        var paddingY = 10f;
        return new Vector2(textWidth + paddingX * 2, _font.GetLineHeight(1f) + paddingY);
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

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        var bgAlpha = _isActive ? 0.7f : 0.5f;
        handle.DrawRect(rect, _bgColor.WithAlpha(bgAlpha));

        var borderColor = _isActive ? _activeColor :
                         _hovered && !_disabled ? _hoverColor :
                         _borderColor;
        DrawBorder(handle, rect, borderColor);

        if ((_hovered && !_disabled) || _isActive)
        {
            var glowOffset = 1f * UIScale;
            var glowRect = new UIBox2(rect.Left - glowOffset, rect.Top - glowOffset, rect.Right + glowOffset, rect.Bottom + glowOffset);
            var glowColor = _isActive ? _activeColor.WithAlpha(0.5f) : _glowColor;
            DrawBorder(handle, glowRect, glowColor);
        }

        var color = _disabled ? _disabledColor :
                    _isActive ? _activeColor :
                    _pressed ? _pressColor :
                    _hovered ? _hoverColor :
                    _normalColor;

        var displayText = _text.ToUpper();
        var textWidth = GetTextWidth(displayText);
        var textX = (PixelSize.X - textWidth) / 2f;
        var textY = (PixelSize.Y - _font.GetLineHeight(UIScale)) / 2f;

        handle.DrawString(_font, new Vector2(textX, textY), displayText, UIScale, color);
    }

    private void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        handle.DrawLine(rect.TopLeft, rect.TopRight, color);
        handle.DrawLine(rect.TopRight, rect.BottomRight, color);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, color);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, color);
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

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
        if (!_disabled)
            UserInterfaceManager.HoverSound();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
        _pressed = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || _disabled)
            return;

        _pressed = true;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || _disabled)
            return;

        if (_pressed && _hovered)
        {
            UserInterfaceManager.ClickSound();
            OnPressed?.Invoke();
        }

        _pressed = false;
        args.Handle();
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
