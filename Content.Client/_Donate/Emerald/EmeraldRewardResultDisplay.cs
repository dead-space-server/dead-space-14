// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldRewardResultDisplay : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    private const int TitleFontSize = 16;
    private const int NameFontSize = 12;
    private const int MessageFontSize = 10;

    private Font _titleFont = default!;
    private Font _nameFont = default!;
    private Font _messageFont = default!;

    private string _itemName = "";
    private string? _protoId;
    private bool _isPremium;
    private bool _isSuccess;
    private string _message = "";
    private bool _isLootbox;

    private float _animationProgress;
    private bool _animationComplete;

    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _successColor = Color.FromHex("#00FFAA");
    private readonly Color _errorColor = Color.FromHex("#ff6b6b");
    private readonly Color _premiumColor = Color.FromHex("#ffd700");
    private readonly Color _textColor = Color.FromHex("#c0b3da");
    private readonly Color _lootboxColor = Color.FromHex("#ffd700");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");

    private SpriteView? _spriteView;
    private TextureRect? _textureRect;
    private PanelContainer? _spriteContainer;
    private Texture? _fallbackTexture;
    private Texture? _lootboxTexture;
    private EmeraldButton? _closeButton;

    public event Action? OnClosePressed;

    public string ItemName
    {
        get => _itemName;
        set
        {
            _itemName = value;
            InvalidateMeasure();
        }
    }

    public string? ProtoId
    {
        get => _protoId;
        set
        {
            _protoId = value;
            UpdateSprite();
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

    public bool IsSuccess
    {
        get => _isSuccess;
        set
        {
            _isSuccess = value;
            InvalidateMeasure();
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            InvalidateMeasure();
        }
    }

    public bool IsLootbox
    {
        get => _isLootbox;
        set
        {
            _isLootbox = value;
            UpdateSprite();
            InvalidateMeasure();
        }
    }

    public EmeraldRewardResultDisplay()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _titleFont = new VectorFont(fontRes, TitleFontSize);
        _nameFont = new VectorFont(fontRes, NameFontSize);
        _messageFont = new VectorFont(fontRes, MessageFontSize);

        MouseFilter = MouseFilterMode.Stop;

        BuildUI();
    }

    private void BuildUI()
    {
        _spriteContainer = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent }
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

        _closeButton = new EmeraldButton
        {
            Text = "ЗАКРЫТЬ",
            MinSize = new Vector2(140, 36)
        };
        _closeButton.OnPressed += () => OnClosePressed?.Invoke();
        AddChild(_closeButton);
    }

    private void UpdateSprite()
    {
        if (_spriteView == null || _textureRect == null)
            return;

        if (_isLootbox)
        {
            _spriteView.Visible = false;
            _textureRect.Visible = true;

            if (_lootboxTexture == null)
            {
                try
                {
                    _lootboxTexture = _resourceCache.GetResource<TextureResource>("/Textures/Interface/lootbox.png").Texture;
                }
                catch
                {
                    try
                    {
                        _lootboxTexture = _resourceCache.GetResource<TextureResource>("/Textures/Interface/giftbox.png").Texture;
                    }
                    catch
                    {
                        _lootboxTexture = null;
                    }
                }
            }

            _textureRect.Texture = _lootboxTexture;
            return;
        }

        if (!string.IsNullOrEmpty(_protoId) && _protoManager.HasIndex<EntityPrototype>(_protoId))
        {
            try
            {
                var spawned = _entMan.SpawnEntity(_protoId, MapCoordinates.Nullspace);
                _spriteView.SetEntity(spawned);
                _spriteView.Visible = true;
                _spriteView.Scale = new Vector2(2.5f, 2.5f);
                _textureRect.Visible = false;
                return;
            }
            catch
            {
            }
        }

        _spriteView.Visible = false;
        _textureRect.Visible = true;

        if (_fallbackTexture == null)
        {
            try
            {
                _fallbackTexture = _resourceCache.GetResource<TextureResource>("/Textures/Interface/giftbox.png").Texture;
            }
            catch
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
        }

        _textureRect.Texture = _fallbackTexture;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(300, 340);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_spriteContainer != null)
        {
            var size = 80f;
            var x = (finalSize.X - size) / 2f;
            var y = 80f;
            _spriteContainer.Arrange(new UIBox2(x, y, x + size, y + size));
        }

        if (_closeButton != null)
        {
            var s = _closeButton.DesiredSize;
            var x = (finalSize.X - s.X) / 2f;
            var y = finalSize.Y - s.Y - 40f;
            _closeButton.Arrange(new UIBox2(x, y, x + s.X, y + s.Y));
        }

        return finalSize;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_animationComplete)
        {
            _animationProgress = Math.Min(1f, _animationProgress + (float)args.DeltaSeconds * 2f);
            if (_animationProgress >= 1f)
                _animationComplete = true;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
        handle.DrawRect(rect, _bgColor.WithAlpha(0.95f));

        var borderThickness = 2f * UIScale;
        var bc = _isLootbox ? _lootboxColor : _isPremium ? _premiumColor : _isSuccess ? _successColor : _errorColor;
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + borderThickness), bc);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - borderThickness, rect.Right, rect.Bottom), bc);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + borderThickness, rect.Bottom), bc);
        handle.DrawRect(new UIBox2(rect.Right - borderThickness, rect.Top, rect.Right, rect.Bottom), bc);

        var accent = _isLootbox ? _lootboxColor : _isPremium ? _premiumColor : _isSuccess ? _successColor : _errorColor;

        var scale = 0.5f + _animationProgress * 0.5f;
        var title = _isSuccess ? "НАГРАДА ПОЛУЧЕНА!" : "ОШИБКА";
        var titleWidth = GetTextWidth(title, _titleFont);
        var titleX = (PixelSize.X - titleWidth * scale) / 2f;
        var titleY = 24f * UIScale;

        handle.DrawString(_titleFont, new Vector2(titleX, titleY), title, UIScale * scale, accent);

        var name = _itemName;
        if (_isLootbox)
        {
            name = "???";
        }

        var nameY = 175f * UIScale;
        var nameWidth = GetTextWidth(_itemName, _nameFont);
        var nameColor = _isLootbox ? _lootboxColor : _textColor;
        handle.DrawString(_nameFont,
            new Vector2((PixelSize.X - nameWidth) / 2f, nameY),
            name, UIScale, nameColor);
    }

    private float GetTextWidth(string text, Font font)
    {
        var w = 0f;
        foreach (var r in text.EnumerateRunes())
        {
            var m = font.GetCharMetrics(r, UIScale);
            if (m.HasValue)
                w += m.Value.Advance;
        }
        return w;
    }
}
