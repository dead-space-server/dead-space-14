// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared._Donate;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldShopItemCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    private const int BaseNameFontSize = 10;
    private const int BasePriceFontSize = 11;

    private Font _nameFont = default!;
    private Font _priceFont = default!;

    private string _itemName = "";
    private int _itemId;
    private string _protoId = "";
    private Dictionary<PurchasePeriod, float> _prices = new();
    private PurchasePeriod _selectedPeriod = PurchasePeriod.Month;
    private bool _owned;

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _nameColor = Color.FromHex("#c0b3da");
    private readonly Color _priceColor = Color.FromHex("#00FFAA");
    private readonly Color _ownedColor = Color.FromHex("#4CAF50");
    private readonly Color _spriteBgColor = Color.FromHex("#0f0a1e");
    private readonly Color _hoverGlowColor = Color.FromHex("#6d5a8a");

    private SpriteView? _spriteView;
    private TextureRect? _textureRect;
    private PanelContainer? _spriteContainer;
    private Texture? _fallbackTexture;

    private BoxContainer? _periodSelector;
    private EmeraldButton? _buyButton;

    public event Action<int, PurchasePeriod>? OnPurchaseRequest;

    public string ItemName
    {
        get => _itemName;
        set
        {
            _itemName = value;
            InvalidateMeasure();
        }
    }

    public int ItemId
    {
        get => _itemId;
        set
        {
            _itemId = value;
        }
    }

    public string ProtoId
    {
        get => _protoId;
        set
        {
            _protoId = value;
            UpdateSprite();
        }
    }

    public Dictionary<PurchasePeriod, float> Prices
    {
        get => _prices;
        set
        {
            _prices = value;
            if (_prices.Count > 0 && !_prices.ContainsKey(_selectedPeriod))
            {
                _selectedPeriod = _prices.Keys.First();
            }
            UpdatePeriodSelector();
            InvalidateMeasure();
        }
    }

    public bool Owned
    {
        get => _owned;
        set
        {
            _owned = value;
            UpdateBuyButton();
            InvalidateMeasure();
        }
    }

    public EmeraldShopItemCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _nameFont = new VectorFont(fontRes, BaseNameFontSize);
        _priceFont = new VectorFont(fontRes, BasePriceFontSize);

        BuildUI();
        MouseFilter = MouseFilterMode.Stop;
    }

    private void BuildUI()
    {
        _spriteContainer = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = _spriteBgColor.WithAlpha(0.4f)
            }
        };

        _spriteView = new SpriteView(_entMan)
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Stretch = SpriteView.StretchMode.Fit,
            Visible = false
        };

        _textureRect = new TextureRect
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Visible = false,
        };

        _spriteContainer.AddChild(_spriteView);
        _spriteContainer.AddChild(_textureRect);
        AddChild(_spriteContainer);

        _periodSelector = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 2
        };
        AddChild(_periodSelector);

        _buyButton = new EmeraldButton
        {
            Text = "КУПИТЬ",
            HorizontalExpand = true
        };
        _buyButton.OnPressed += () =>
        {
            if (!_owned && _itemId > 0)
            {
                OnPurchaseRequest?.Invoke(_itemId, _selectedPeriod);
            }
        };
        AddChild(_buyButton);
    }

    private void UpdateSprite()
    {
        if (_spriteView == null || _textureRect == null)
            return;

        if (!string.IsNullOrEmpty(_protoId) && _protoManager.HasIndex<EntityPrototype>(_protoId))
        {
            try
            {
                var spawned = _entMan.SpawnEntity(_protoId, MapCoordinates.Nullspace);
                _spriteView.SetEntity(spawned);
                _spriteView.Visible = true;
                _spriteView.Scale = new Vector2(2f, 2f);
                _textureRect.Visible = false;
                return;
            }
            catch
            {
                // ignore
            }
        }

        _spriteView.Visible = false;
        _textureRect.Visible = true;

        if (_fallbackTexture == null)
        {
            try
            {
                _fallbackTexture = _resourceCache.GetResource<TextureResource>("/Textures/Interface/fallback.png").Texture;
            }
            catch
            {
                _fallbackTexture = null;
            }
        }

        _textureRect.Texture = _fallbackTexture;
    }

    private void UpdatePeriodSelector()
    {
        if (_periodSelector == null)
            return;

        _periodSelector.RemoveAllChildren();

        foreach (var (period, _) in _prices.OrderBy(p => (int)p.Key))
        {
            var periodButton = new EmeraldPeriodButton
            {
                Period = period,
                IsSelected = period == _selectedPeriod,
                HorizontalExpand = true
            };
            periodButton.OnSelected += OnPeriodSelected;
            _periodSelector.AddChild(periodButton);
        }
    }

    private void OnPeriodSelected(PurchasePeriod period)
    {
        _selectedPeriod = period;

        if (_periodSelector == null)
            return;

        foreach (var child in _periodSelector.Children)
        {
            if (child is EmeraldPeriodButton btn)
            {
                btn.IsSelected = btn.Period == period;
            }
        }

        InvalidateMeasure();
    }

    private void UpdateBuyButton()
    {
        if (_buyButton == null)
            return;

        _buyButton.Disabled = _owned;
        _buyButton.Text = _owned ? "КУПЛЕНО" : "КУПИТЬ";
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(160, 240);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_spriteContainer != null)
        {
            var spriteBox = new UIBox2(2, 2, finalSize.X - 2, 80);
            _spriteContainer.Arrange(spriteBox);
        }

        if (_periodSelector != null)
        {
            var periodY = finalSize.Y - 70;
            var periodBox = new UIBox2(4, periodY, finalSize.X - 4, periodY + 28);
            _periodSelector.Arrange(periodBox);
        }

        if (_buyButton != null)
        {
            var buttonY = finalSize.Y - 36;
            var buttonBox = new UIBox2(4, buttonY, finalSize.X - 4, buttonY + 32);
            _buyButton.Arrange(buttonBox);
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.8f));

        if (!_owned)
        {
            var glowRect = new UIBox2(rect.Left - 1, rect.Top - 1, rect.Right + 1, rect.Bottom + 1);
            handle.DrawRect(glowRect, _hoverGlowColor.WithAlpha(0.3f));
        }

        var borderColor = _owned ? _ownedColor : _borderColor;
        handle.DrawLine(rect.TopLeft, rect.TopRight, borderColor);
        handle.DrawLine(rect.TopRight, rect.BottomRight, borderColor);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, borderColor);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, borderColor);

        var maxTextWidth = PixelSize.X - 8f;
        var lines = WrapText(_itemName, maxTextWidth, _nameFont, 2);
        var nameY = 85f * UIScale;
        var lineHeight = _nameFont.GetLineHeight(UIScale);

        var nameColor = _owned ? _ownedColor : _nameColor;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineWidth = GetTextWidth(line, _nameFont);
            var lineX = (PixelSize.X - lineWidth) / 2f;
            handle.DrawString(_nameFont, new Vector2(lineX, nameY + i * lineHeight), line, UIScale, nameColor);
        }

        var priceY = nameY + lines.Count * lineHeight + 6f * UIScale;

        if (_owned)
        {
            var ownedText = "КУПЛЕН";
            var ownedWidth = GetTextWidth(ownedText, _priceFont);
            var ownedX = (PixelSize.X - ownedWidth) / 2f;
            handle.DrawString(_priceFont, new Vector2(ownedX, priceY), ownedText, UIScale, _ownedColor);
        }
        else if (_prices.TryGetValue(_selectedPeriod, out var price))
        {
            var priceText = $"{price:F0} ЭНЕРГИИ";
            var priceWidth = GetTextWidth(priceText, _priceFont);
            var priceX = (PixelSize.X - priceWidth) / 2f;
            handle.DrawString(_priceFont, new Vector2(priceX, priceY), priceText, UIScale, _priceColor);
        }
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        InvalidateMeasure();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        InvalidateMeasure();
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

    private List<string> WrapText(string text, float maxWidth, Font font, int maxLines)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
            return lines;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var testWidth = GetTextWidth(testLine, font);

            if (testWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                if (lines.Count == maxLines - 1)
                {
                    var ellipsis = "..";
                    var availableWidth = maxWidth - GetTextWidth(ellipsis, font);
                    while (GetTextWidth(currentLine, font) > availableWidth && currentLine.Length > 0)
                    {
                        currentLine = currentLine.Substring(0, currentLine.Length - 1);
                    }
                    currentLine += ellipsis;
                    lines.Add(currentLine);
                    break;
                }

                lines.Add(currentLine);
                currentLine = word;

                if (lines.Count >= maxLines)
                    break;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine) && lines.Count < maxLines)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}

public sealed partial class EmeraldPeriodButton : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int BaseFontSize = 7;

    private Font _font = default!;
    private PurchasePeriod _period;
    private bool _isSelected;
    private bool _hovered;

    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _selectedBgColor = Color.FromHex("#1a2a2a");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _selectedColor = Color.FromHex("#00FFAA");
    private readonly Color _normalColor = Color.FromHex("#6d5a8a");
    private readonly Color _hoverColor = Color.FromHex("#a589c9");

    public event Action<PurchasePeriod>? OnSelected;

    public PurchasePeriod Period
    {
        get => _period;
        set
        {
            _period = value;
            InvalidateMeasure();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            InvalidateMeasure();
        }
    }

    public EmeraldPeriodButton()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(
            _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf"),
            BaseFontSize);
        MouseFilter = MouseFilterMode.Stop;
    }

    private string GetPeriodText()
    {
        return _period switch
        {
            PurchasePeriod.Week => "НЕД",
            PurchasePeriod.Month => "МЕС",
            PurchasePeriod.ThreeMonth => "3М",
            PurchasePeriod.SixMonth => "6М",
            PurchasePeriod.Year => "ГОД",
            PurchasePeriod.Always => "∞",
            _ => "?"
        };
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(28, 24);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        var bgColor = _isSelected ? _selectedBgColor : _bgColor;
        handle.DrawRect(rect, bgColor.WithAlpha(0.8f));

        var borderColor = _isSelected ? _selectedColor : _borderColor;
        handle.DrawLine(rect.TopLeft, rect.TopRight, borderColor);
        handle.DrawLine(rect.TopRight, rect.BottomRight, borderColor);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, borderColor);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, borderColor);

        var text = GetPeriodText();
        var textWidth = GetTextWidth(text);
        var lineHeight = _font.GetLineHeight(UIScale);
        var textX = (PixelSize.X - textWidth) / 2f;
        var textY = (PixelSize.Y - lineHeight) / 2f;

        var textColor = _isSelected ? _selectedColor : _hovered ? _hoverColor : _normalColor;
        handle.DrawString(_font, new Vector2(textX, textY), text, UIScale, textColor);
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
        UserInterfaceManager.HoverSound();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            UserInterfaceManager.ClickSound();
            OnSelected?.Invoke(_period);
            args.Handle();
        }
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
