// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldSubscriptionCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int BaseNameFontSize = 11;
    private const int BaseInfoFontSize = 9;

    private Font _nameFont = default!;
    private Font _infoFont = default!;

    private string _nameSub = "";
    private string _price = "";
    private string _dates = "";
    private int _itemCount;

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _nameColor = Color.FromHex("#c0b3da");
    private readonly Color _dateColor = Color.FromHex("#8975b5");
    private readonly Color _itemColor = Color.FromHex("#6d5a8a");

    private bool _isAdmin;
    private readonly Color _adminColor = Color.FromHex("#ff0000");
    private readonly Color _adminBorderColor = Color.FromHex("#d12e2e");

    public string NameSub
    {
        get => _nameSub;
        set
        {
            _nameSub = value;
            InvalidateMeasure();
        }
    }

    public string Price
    {
        get => _price;
        set
        {
            _price = value;
            InvalidateMeasure();
        }
    }

    public string Dates
    {
        get => _dates;
        set
        {
            _dates = value;
            InvalidateMeasure();
        }
    }

    public int ItemCount
    {
        get => _itemCount;
        set
        {
            _itemCount = value;
            InvalidateMeasure();
        }
    }

    public bool IsAdmin
    {
        get => _isAdmin;
        set
        {
            _isAdmin = value;
            InvalidateMeasure();
        }
    }

    public EmeraldSubscriptionCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _nameFont = new VectorFont(fontRes, BaseNameFontSize);
        _infoFont = new VectorFont(fontRes, BaseInfoFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 300 : availableSize.X;
        return new Vector2(width, 70);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.8f));

        var borderColor = _isAdmin ? _adminBorderColor : _borderColor;
        DrawBorder(handle, rect, borderColor);

        var y = 8f * UIScale;
        var x = 10f * UIScale;

        var nameColor = _isAdmin ? _adminColor : _nameColor;
        handle.DrawString(_nameFont, new Vector2(x, y), _nameSub, UIScale, nameColor);

        y += _nameFont.GetLineHeight(UIScale) + 4f * UIScale;

        var infoText = $"{_price}  •  {_dates}";
        handle.DrawString(_infoFont, new Vector2(x, y), infoText, UIScale, _dateColor);
        y += _infoFont.GetLineHeight(UIScale) + 4f * UIScale;

        var itemText = $"{_itemCount} предметов подписки";
        handle.DrawString(_infoFont, new Vector2(x, y), itemText, UIScale, _itemColor);
    }

    private void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        var thickness = Math.Max(1f, 1f * UIScale);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + thickness), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - thickness, rect.Right, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + thickness, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Right - thickness, rect.Top, rect.Right, rect.Bottom), color);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
