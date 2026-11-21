// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

[Virtual]
public partial class EmeraldLabel : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int BaseFontSize = 12;

    private Font _font = default!;
    private string _text = "";
    private Color _color = Color.FromHex("#c0b3da");
    private TextAlignment _alignment = TextAlignment.Left;

    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            InvalidateMeasure();
        }
    }

    public Color TextColor
    {
        get => _color;
        set
        {
            _color = value;
            InvalidateMeasure();
        }
    }

    public TextAlignment Alignment
    {
        get => _alignment;
        set
        {
            _alignment = value;
            InvalidateMeasure();
        }
    }

    public EmeraldLabel()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(
            _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf"),
            BaseFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var textWidth = GetTextWidth(_text);
        var lineHeight = _font.GetLineHeight(UIScale);
        return new Vector2(textWidth / UIScale, lineHeight / UIScale);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var textWidth = GetTextWidth(_text);
        var textX = _alignment switch
        {
            TextAlignment.Center => (PixelSize.X - textWidth) / 2f,
            TextAlignment.Right => PixelSize.X - textWidth,
            _ => 0f
        };

        var textY = 0f;

        handle.DrawString(_font, new Vector2(textX, textY), _text, UIScale, _color);
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

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
