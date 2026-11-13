// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

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

public sealed partial class EmeraldCalendarDayCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    private const int BaseDayFontSize = 12;
    private const int BaseNameFontSize = 7;
    private const int BaseStatusFontSize = 6;

    private Font _dayFont = default!;
    private Font _nameFont = default!;
    private Font _statusFont = default!;

    private int _day;
    private string _itemName = "";
    private string? _protoId;
    private int _rewardId;
    private CalendarRewardStatus _status = CalendarRewardStatus.Locked;
    private bool _isPremium;
    private bool _isCurrentDay;
    private bool _hovered;
    private bool _isHidden;
    private bool _isLootbox;

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _dayColor = Color.FromHex("#c0b3da");
    private readonly Color _nameColor = Color.FromHex("#8d7aaa");
    private readonly Color _lockedColor = Color.FromHex("#4a3a5a");
    private readonly Color _availableColor = Color.FromHex("#00FFAA");
    private readonly Color _claimedColor = Color.FromHex("#4CAF50");
    private readonly Color _premiumColor = Color.FromHex("#d4a574");
    private readonly Color _premiumBorderColor = Color.FromHex("#ffd700");
    private readonly Color _currentDayGlow = Color.FromHex("#00FFAA");
    private readonly Color _spriteBgColor = Color.FromHex("#0f0a1e");
    private readonly Color _hoverGlowColor = Color.FromHex("#6d5a8a");
    private readonly Color _lockedOverlayColor = Color.FromHex("#0f0a1e");
    private readonly Color _lootboxColor = Color.FromHex("#ffd700");

    private SpriteView? _spriteView;
    private TextureRect? _textureRect;
    private EmeraldPanel? _spriteContainer;
    private Texture? _fallbackTexture;
    private Texture? _lootboxTexture;

    public event Action<int, bool>? OnClaimRequest;

    public int Day
    {
        get => _day;
        set
        {
            _day = value;
            InvalidateMeasure();
        }
    }

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

    public int RewardId
    {
        get => _rewardId;
        set => _rewardId = value;
    }

    public CalendarRewardStatus Status
    {
        get => _status;
        set
        {
            _status = value;
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

    public bool IsCurrentDay
    {
        get => _isCurrentDay;
        set
        {
            _isCurrentDay = value;
            InvalidateMeasure();
        }
    }

    private bool _isLocked;

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            _isLocked = value;
            InvalidateMeasure();
        }
    }

    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            _isHidden = value;
            UpdateSprite();
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

    public EmeraldCalendarDayCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _dayFont = new VectorFont(fontRes, BaseDayFontSize);
        _nameFont = new VectorFont(fontRes, BaseNameFontSize);
        _statusFont = new VectorFont(fontRes, BaseStatusFontSize);

        BuildSprite();
        MouseFilter = MouseFilterMode.Stop;
    }

    private void BuildSprite()
    {
        _spriteContainer = new EmeraldPanel
        {
            MouseFilter = MouseFilterMode.Pass
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
    }

    private void UpdateSprite()
    {
        if (_spriteView == null || _textureRect == null)
            return;

        if (_isHidden)
        {
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
            return;
        }

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
                _spriteView.Scale = new Vector2(1.2f, 1.2f);
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
        return new Vector2(85, 105);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_spriteContainer != null)
        {
            var spriteY = 22f;
            var spriteHeight = 42f;
            var spriteBox = new UIBox2(3, spriteY, finalSize.X - 3, spriteY + spriteHeight);
            _spriteContainer.Arrange(spriteBox);
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        if (_isCurrentDay && _status == CalendarRewardStatus.Available)
        {
            var pulseAlpha = 0.4f + MathF.Sin((float)DateTime.Now.TimeOfDay.TotalSeconds * 3f) * 0.2f;
            var glowRect = new UIBox2(rect.Left - 2, rect.Top - 2, rect.Right + 2, rect.Bottom + 2);
            handle.DrawRect(glowRect, _currentDayGlow.WithAlpha(pulseAlpha));
        }

        if (_hovered && _status == CalendarRewardStatus.Available)
        {
            var glowOffset = 1f * UIScale;
            var glowRect = new UIBox2(rect.Left - glowOffset, rect.Top - glowOffset, rect.Right + glowOffset, rect.Bottom + glowOffset);
            handle.DrawRect(glowRect, _hoverGlowColor.WithAlpha(0.5f));
        }

        var borderColor = _isPremium ? _premiumBorderColor :
                         _status == CalendarRewardStatus.Available ? _availableColor :
                         _status == CalendarRewardStatus.Claimed ? _claimedColor :
                         _borderColor;

        var borderThickness = (_isCurrentDay || _isPremium || _isLootbox) ? 2f * UIScale : 1f * UIScale;
        DrawBorder(handle, rect, borderColor, borderThickness);

        var dayText = $"ДЕНЬ {_day}";
        var dayWidth = GetTextWidth(dayText, _dayFont);
        var dayX = (PixelSize.X - dayWidth) / 2f;
        var dayY = 4f * UIScale;

        var dayTextColor = _isLootbox ? _lootboxColor :
                          _isPremium ? _premiumColor :
                          _status == CalendarRewardStatus.Available ? _availableColor :
                          _status == CalendarRewardStatus.Claimed ? _claimedColor :
                          _dayColor;

        handle.DrawString(_dayFont, new Vector2(dayX, dayY), dayText, UIScale, dayTextColor);

        var nameY = 68f * UIScale;
        var maxNameWidth = PixelSize.X - 6f * UIScale;
        var displayName = _isHidden ? "???" : TruncateText(_itemName, maxNameWidth, _nameFont);
        var nameWidth = GetTextWidth(displayName, _nameFont);
        var nameX = (PixelSize.X - nameWidth) / 2f;

        var nameTextColor = _isLootbox ? _lootboxColor :
                           _status == CalendarRewardStatus.Locked ? _lockedColor : _nameColor;
        handle.DrawString(_nameFont, new Vector2(nameX, nameY), displayName, UIScale, nameTextColor);

        var statusY = nameY + _nameFont.GetLineHeight(UIScale) + 2f * UIScale;
        string statusText;
        Color statusColor;

        switch (_status)
        {
            case CalendarRewardStatus.Available:
                statusText = "ЗАБРАТЬ";
                statusColor = _availableColor;
                break;
            case CalendarRewardStatus.Claimed:
                statusText = "ПОЛУЧЕНО";
                statusColor = _claimedColor;
                break;
            case CalendarRewardStatus.Missed:
                statusText = "ПРОПУЩЕНО";
                statusColor = _lockedColor;
                break;
            default:
                statusText = "ЗАБЛОКИРОВАНО";
                statusColor = _lockedColor;
                break;
        }

        var statusWidth = GetTextWidth(statusText, _statusFont);
        var statusX = (PixelSize.X - statusWidth) / 2f;
        handle.DrawString(_statusFont, new Vector2(statusX, statusY), statusText, UIScale, statusColor);

        if (_isPremium)
        {
            var premiumText = "PREMIUM";
            var premiumWidth = GetTextWidth(premiumText, _statusFont);
            var premiumX = (PixelSize.X - premiumWidth) / 2f;
            var premiumY = statusY + _statusFont.GetLineHeight(UIScale) + 1f * UIScale;
            handle.DrawString(_statusFont, new Vector2(premiumX, premiumY), premiumText, UIScale, _premiumColor);
        }

        if (_isLocked && _isPremium)
        {
            var overlayRect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
            handle.DrawRect(overlayRect, _lockedOverlayColor.WithAlpha(0.5f));
        }
    }

    private void DrawBorder(DrawingHandleScreen handle, UIBox2 rect, Color color, float thickness)
    {
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + thickness), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - thickness, rect.Right, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + thickness, rect.Bottom), color);
        handle.DrawRect(new UIBox2(rect.Right - thickness, rect.Top, rect.Right, rect.Bottom), color);
    }

    private string TruncateText(string text, float maxWidth, Font font)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (GetTextWidth(text, font) <= maxWidth)
            return text;

        var ellipsis = "..";
        var ellipsisWidth = GetTextWidth(ellipsis, font);
        var availableWidth = maxWidth - ellipsisWidth;

        var result = "";
        foreach (var rune in text.EnumerateRunes())
        {
            var testResult = result + rune.ToString();
            if (GetTextWidth(testResult, font) > availableWidth)
                break;
            result = testResult;
        }

        return result + ellipsis;
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

    protected override void MouseEntered()
    {
        base.MouseEntered();
        if (_status == CalendarRewardStatus.Available && !_isLocked)
        {
            _hovered = true;
            UserInterfaceManager.HoverSound();
        }
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_status == CalendarRewardStatus.Available && !_isLocked)
        {
            UserInterfaceManager.ClickSound();
            OnClaimRequest?.Invoke(_rewardId, _isPremium);
            args.Handle();
        }
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
