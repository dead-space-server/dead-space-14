// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldItemCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    private const int BaseNameFontSize = 10;
    private const int BaseStatusFontSize = 9;
    private const int BaseSourceFontSize = 8;

    private Font _nameFont = default!;
    private Font _statusFont = default!;
    private Font _sourceFont = default!;

    private string _itemName = "";
    private string _protoId = "";
    private string? _timeFinish;
    private bool _timeAllways;
    private bool _isActive;
    private bool _isSpawned;
    private bool _isTimeUp;
    private string? _sourceSubscription;
    private bool _isLootbox;
    private int _userItemId;
    private bool _stelsHidden;

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _nameColor = Color.FromHex("#c0b3da");
    private readonly Color _timeColor = Color.FromHex("#a589c9");
    private readonly Color _timeExpiringColor = Color.FromHex("#d4a574");
    private readonly Color _inactiveColor = Color.FromHex("#6d5a8a");
    private readonly Color _spriteBgColor = Color.FromHex("#0f0a1e");
    private readonly Color _hoverGlowColor = Color.FromHex("#6d5a8a");
    private readonly Color _spawnedColor = Color.FromHex("#4CAF50");
    private readonly Color _subscriptionColor = Color.FromHex("#d4a574");
    private readonly Color _purchasedColor = Color.FromHex("#8d7aaa");
    private readonly Color _adminColor = Color.FromHex("#FF0000");
    private readonly Color _lootboxColor = Color.FromHex("#ffd700");

    private SpriteView? _spriteView;
    private TextureRect? _textureRect;
    private PanelContainer? _spriteContainer;
    private Texture? _fallbackTexture;
    private Texture? _lootboxTexture;
    private bool _hovered;
    private EmeraldButton? _openButton;

    public event Action<string>? OnSpawnRequest;
    public event Action<string, int, bool>? OnOpenLootboxRequest;

    public string ItemName
    {
        get => _itemName;
        set
        {
            _itemName = value;
            InvalidateMeasure();
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

    public string? TimeFinish
    {
        get => _timeFinish;
        set
        {
            _timeFinish = value;
            InvalidateMeasure();
        }
    }

    public bool TimeAllways
    {
        get => _timeAllways;
        set
        {
            _timeAllways = value;
            InvalidateMeasure();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            UpdateOpenButton();
            InvalidateMeasure();
        }
    }

    public bool IsSpawned
    {
        get => _isSpawned;
        set
        {
            _isSpawned = value;
            InvalidateMeasure();
        }
    }

    public bool IsTimeUp
    {
        get => _isTimeUp;
        set
        {
            _isTimeUp = value;
            InvalidateMeasure();
        }
    }

    public string? SourceSubscription
    {
        get => _sourceSubscription;
        set
        {
            _sourceSubscription = value;
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
            UpdateOpenButton();
            InvalidateMeasure();
        }
    }

    public int UserItemId
    {
        get => _userItemId;
        set => _userItemId = value;
    }

    public bool StelsHidden
    {
        get => _stelsHidden;
        set => _stelsHidden = value;
    }

    public bool IsFromSubscription => SourceSubscription != null;

    public EmeraldItemCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _nameFont = new VectorFont(fontRes, BaseNameFontSize);
        _statusFont = new VectorFont(fontRes, BaseStatusFontSize);
        _sourceFont = new VectorFont(fontRes, BaseSourceFontSize);

        BuildSprite();
        MouseFilter = MouseFilterMode.Stop;
    }

    private void BuildSprite()
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

        _openButton = new EmeraldButton
        {
            Text = "ОТКРЫТЬ",
            Visible = false,
            MinSize = new Vector2(80, 24)
        };
        _openButton.OnPressed += () =>
        {
            if (_isLootbox && !_isSpawned)
            {
                OnOpenLootboxRequest?.Invoke(_itemName, _userItemId, _stelsHidden);
            }
        };
        AddChild(_openButton);
    }

    private void UpdateOpenButton()
    {
        if (_openButton == null)
            return;

        _openButton.Visible = _isLootbox && !_isSpawned && !_isTimeUp;
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
                UpdateSpriteScale();
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
                _fallbackTexture = _resourceCache.GetResource<TextureResource>("/Textures/Interface/fallback.png").Texture;
            }
            catch
            {
                _fallbackTexture = null;
            }
        }

        _textureRect.Texture = _fallbackTexture;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(145, 220);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_spriteContainer != null)
        {
            var spriteBox = new UIBox2(2, 2, finalSize.X - 2, 95);
            _spriteContainer.Arrange(spriteBox);
        }

        if (_openButton != null && _openButton.Visible)
        {
            var buttonWidth = 100f;
            var buttonHeight = 26f;
            var buttonX = (finalSize.X - buttonWidth) / 2f;
            var buttonY = finalSize.Y - buttonHeight - 6f;
            _openButton.Arrange(new UIBox2(buttonX, buttonY, buttonX + buttonWidth, buttonY + buttonHeight));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        var bgAlpha = _isActive && !_isSpawned ? 0.8f : 0.5f;
        handle.DrawRect(rect, _bgColor.WithAlpha(bgAlpha));

        if (_hovered && _isActive && !_isSpawned && !string.IsNullOrEmpty(_protoId) && !_isLootbox)
        {
            var glowOffset = 1f * UIScale;
            var glowRect = new UIBox2(rect.Left - glowOffset, rect.Top - glowOffset, rect.Right + glowOffset, rect.Bottom + glowOffset);
            handle.DrawRect(glowRect, _hoverGlowColor.WithAlpha(0.3f));
        }

        var isAdmin = IsFromSubscription && (_sourceSubscription?.StartsWith("[ADMIN]") ?? false);

        var borderColor = _isLootbox ? _lootboxColor :
            _isActive ? (isAdmin ? _adminColor : IsFromSubscription ? _subscriptionColor : _borderColor) :
            _isSpawned ? _spawnedColor :
            _inactiveColor;

        handle.DrawLine(rect.TopLeft, rect.TopRight, borderColor);
        handle.DrawLine(rect.TopRight, rect.BottomRight, borderColor);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, borderColor);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, borderColor);

        var maxTextWidth = PixelSize.X - 8f * UIScale;
        var displayName = (_isLootbox && _stelsHidden) ? "???" : _itemName;
        var lines = WrapText(displayName, maxTextWidth, _nameFont, 3);
        var nameY = 99f * UIScale;
        var lineHeight = _nameFont.GetLineHeight(UIScale);

        var nameColor = _isLootbox ? _lootboxColor :
            _isActive ? (isAdmin ? _adminColor : IsFromSubscription ? _subscriptionColor : _nameColor) :
            _isSpawned ? _spawnedColor :
            _inactiveColor;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineWidth = GetTextWidth(line, _nameFont);
            var lineX = (PixelSize.X - lineWidth) / 2f;
            handle.DrawString(_nameFont, new Vector2(lineX, nameY + i * lineHeight), line, UIScale, nameColor);
        }

        var statusY = nameY + lines.Count * lineHeight + 4f * UIScale;

        if (_isLootbox)
        {
            var lootboxText = "ЛУТБОКС";
            var lootboxWidth = GetTextWidth(lootboxText, _sourceFont);
            var lootboxX = (PixelSize.X - lootboxWidth) / 2f;
            handle.DrawString(_sourceFont, new Vector2(lootboxX, statusY), lootboxText, UIScale, _lootboxColor);
            statusY += _sourceFont.GetLineHeight(UIScale) + 2f * UIScale;

            if (_openButton != null && _openButton.Visible)
                return;
        }
        else if (IsFromSubscription)
        {
            var sourceText = _sourceSubscription!.ToUpper();
            var sourceWidth = GetTextWidth(sourceText, _sourceFont);
            var sourceX = (PixelSize.X - sourceWidth) / 2f;
            handle.DrawString(_sourceFont, new Vector2(sourceX, statusY), sourceText, UIScale, _subscriptionColor);
            statusY += _sourceFont.GetLineHeight(UIScale) + 2f * UIScale;
        }
        else
        {
            var purchasedText = "КУПЛЕНО";
            var purchasedWidth = GetTextWidth(purchasedText, _sourceFont);
            var purchasedX = (PixelSize.X - purchasedWidth) / 2f;
            handle.DrawString(_sourceFont, new Vector2(purchasedX, statusY), purchasedText, UIScale, _purchasedColor);
            statusY += _sourceFont.GetLineHeight(UIScale) + 2f * UIScale;
        }

        if (!_isActive)
        {
            var inactiveText = "НЕАКТИВЕН";
            var inactiveWidth = GetTextWidth(inactiveText, _statusFont);
            var inactiveX = (PixelSize.X - inactiveWidth) / 2f;
            handle.DrawString(_statusFont, new Vector2(inactiveX, statusY), inactiveText, UIScale, _inactiveColor);
            return;
        }

        if (_isSpawned)
        {
            var spawnedText = "ЗАСПАВНЕНО";
            var spawnedWidth = GetTextWidth(spawnedText, _statusFont);
            var spawnedX = (PixelSize.X - spawnedWidth) / 2f;
            handle.DrawString(_statusFont, new Vector2(spawnedX, statusY), spawnedText, UIScale, _spawnedColor);
            return;
        }

        if (_isTimeUp)
        {
            var timeUpText = "ВРЕМЯ ИСТЕКЛО";
            var timeUpWidth = GetTextWidth(timeUpText, _statusFont);
            var timeUpX = (PixelSize.X - timeUpWidth) / 2f;
            handle.DrawString(_statusFont, new Vector2(timeUpX, statusY), timeUpText, UIScale, _timeExpiringColor);
            return;
        }

        if (_isLootbox)
            return;

        string timeText;
        Color timeTextColor;

        if (_timeAllways)
        {
            timeText = "НАВСЕГДА";
            timeTextColor = _timeColor;
        }
        else if (!string.IsNullOrEmpty(_timeFinish))
        {
            if (DateTime.TryParse(_timeFinish, out var finishDate))
            {
                var now = DateTime.UtcNow;
                var timeLeft = finishDate - now;

                if (timeLeft.TotalDays < 7)
                {
                    timeTextColor = _timeExpiringColor;
                }
                else
                {
                    timeTextColor = _timeColor;
                }

                timeText = "до " + finishDate.ToString("dd.MM.yyyy");
            }
            else
            {
                timeText = "до " + _timeFinish;
                timeTextColor = _timeColor;
            }
        }
        else
        {
            return;
        }

        var timeWidth = GetTextWidth(timeText, _statusFont);
        var timeX = (PixelSize.X - timeWidth) / 2f;
        handle.DrawString(_statusFont, new Vector2(timeX, statusY), timeText, UIScale, timeTextColor);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        if (!string.IsNullOrEmpty(_protoId) && _protoManager.HasIndex<EntityPrototype>(_protoId) && !_isLootbox)
        {
            _hovered = true;
        }
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

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_isLootbox)
            return;

        if (_isActive && !_isSpawned && !string.IsNullOrEmpty(_protoId) && _protoManager.HasIndex<EntityPrototype>(_protoId))
        {
            OnSpawnRequest?.Invoke(_protoId);
            args.Handle();
        }
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
            var wordWidth = GetTextWidth(word, font);

            if (wordWidth > maxWidth)
            {
                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (lines.Count >= maxLines - 1)
                    {
                        currentLine = TruncateWithEllipsis(currentLine, maxWidth, font);
                        lines.Add(currentLine);
                        return lines;
                    }
                    lines.Add(currentLine);
                    currentLine = "";
                }

                var charLine = "";
                foreach (var rune in word.EnumerateRunes())
                {
                    var charStr = rune.ToString();
                    var testWidth = GetTextWidth(charLine + charStr, font);

                    if (testWidth > maxWidth && !string.IsNullOrEmpty(charLine))
                    {
                        if (lines.Count >= maxLines - 1)
                        {
                            charLine = TruncateWithEllipsis(charLine, maxWidth, font);
                            lines.Add(charLine);
                            return lines;
                        }
                        lines.Add(charLine);
                        charLine = charStr;

                        if (lines.Count >= maxLines)
                            return lines;
                    }
                    else
                    {
                        charLine += charStr;
                    }
                }

                currentLine = charLine;
                continue;
            }

            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var testWidth2 = GetTextWidth(testLine, font);

            if (testWidth2 > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                if (lines.Count >= maxLines - 1)
                {
                    currentLine = TruncateWithEllipsis(currentLine, maxWidth, font);
                    lines.Add(currentLine);
                    return lines;
                }

                lines.Add(currentLine);
                currentLine = word;

                if (lines.Count >= maxLines)
                    return lines;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine) && lines.Count < maxLines)
        {
            if (GetTextWidth(currentLine, font) > maxWidth)
            {
                currentLine = TruncateWithEllipsis(currentLine, maxWidth, font);
            }
            lines.Add(currentLine);
        }

        return lines;
    }

    private string TruncateWithEllipsis(string text, float maxWidth, Font font)
    {
        var ellipsis = "..";
        var ellipsisWidth = GetTextWidth(ellipsis, font);
        var availableWidth = maxWidth - ellipsisWidth;

        if (availableWidth <= 0)
            return ellipsis;

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

    private void UpdateSpriteScale()
    {
        if (_spriteView == null)
            return;

        _spriteView.Scale = new Vector2(2f, 2f);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
