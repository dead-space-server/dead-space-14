// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared._Donate;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldLootboxOpener : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int TitleFontSize = 14;
    private const int NameFontSize = 12;
    private const int RarityFontSize = 10;

    private const float CardWidth = 110f;
    private const float CardHeight = 140f;
    private const float CardSpacing = 6f;
    private const float StripHeight = 170f;

    private const int VisibleCards = 60;
    private const int WinningCardPosition = 45;

    private const float SlowScrollDuration = 5.5f;
    private const float RevealDuration = 0.5f;

    private Font _titleFont = default!;
    private Font _nameFont = default!;
    private Font _rarityFont = default!;

    private readonly Color _bgColor = Color.FromHex("#0f0a1e");
    private readonly Color _stripBgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _indicatorColor = Color.FromHex("#00FFAA");
    private readonly Color _textColor = Color.FromHex("#c0b3da");

    private readonly Color _commonColor = Color.FromHex("#9e9e9e");
    private readonly Color _commonBgColor = Color.FromHex("#2a2a2a");
    private readonly Color _epicColor = Color.FromHex("#9c27b0");
    private readonly Color _epicBgColor = Color.FromHex("#2a1a3a");
    private readonly Color _mythicColor = Color.FromHex("#e91e63");
    private readonly Color _mythicBgColor = Color.FromHex("#3a1a2a");
    private readonly Color _legendaryColor = Color.FromHex("#ffd700");
    private readonly Color _legendaryBgColor = Color.FromHex("#3a2a1a");
    private readonly Color _currencyColor = Color.FromHex("#00FFAA");

    private OpenerState _state = OpenerState.Initial;
    private string _lootboxName = "";
    private int _userItemId;
    private bool _stelsHidden;

    private LootboxOpenResult? _result;
    private List<LootboxRarity> _scrollCards = new();

    private float _scrollOffset;
    private float _targetScrollOffset;
    private float _animationTime;
    private float _revealScale = 1f;

    private EmeraldButton _openButton = default!;
    private EmeraldButton _closeButton = default!;
    private EmeraldLabel _statusLabel = default!;
    private EmeraldLabel _titleLabel = default!;
    private EmeraldLabel _resultLabel = default!;
    private EmeraldLabel _rewardTypeLabel = default!;

    private Random _random = new();

    public event Action<int, bool>? OnOpenRequested;
    public event Action? OnCloseRequested;

    private enum OpenerState
    {
        Initial,
        WaitingResult,
        Scrolling,
        Revealing,
        Complete
    }

    public EmeraldLootboxOpener()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _titleFont = new VectorFont(fontRes, TitleFontSize);
        _nameFont = new VectorFont(fontRes, NameFontSize);
        _rarityFont = new VectorFont(fontRes, RarityFontSize);

        BuildUI();
    }

    private void BuildUI()
    {
        _titleLabel = new EmeraldLabel
        {
            Text = "ОТКРЫТИЕ ЛУТБОКСА",
            Alignment = EmeraldLabel.TextAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            TextColor = _legendaryColor
        };
        AddChild(_titleLabel);

        _statusLabel = new EmeraldLabel
        {
            Text = "",
            Alignment = EmeraldLabel.TextAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            TextColor = _textColor
        };
        AddChild(_statusLabel);

        _rewardTypeLabel = new EmeraldLabel
        {
            Text = "",
            Alignment = EmeraldLabel.TextAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            TextColor = _currencyColor,
            Visible = false
        };
        AddChild(_rewardTypeLabel);

        _resultLabel = new EmeraldLabel
        {
            Text = "",
            Alignment = EmeraldLabel.TextAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            TextColor = _indicatorColor,
            Visible = false
        };
        AddChild(_resultLabel);

        _openButton = new EmeraldButton
        {
            Text = "ОТКРЫТЬ",
            MinSize = new Vector2(140, 36)
        };
        _openButton.OnPressed += OnOpenPressed;
        AddChild(_openButton);

        _closeButton = new EmeraldButton
        {
            Text = "ЗАБРАТЬ",
            MinSize = new Vector2(140, 36),
            Visible = false
        };
        _closeButton.OnPressed += () => OnCloseRequested?.Invoke();
        AddChild(_closeButton);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _titleLabel.Measure(availableSize);
        _statusLabel.Measure(availableSize);
        _rewardTypeLabel.Measure(availableSize);
        _resultLabel.Measure(availableSize);
        _openButton.Measure(availableSize);
        _closeButton.Measure(availableSize);

        return new Vector2(
            float.IsPositiveInfinity(availableSize.X) ? 750 : availableSize.X,
            420
        );
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var titleY = 10f;
        _titleLabel.Arrange(new UIBox2(0, titleY, finalSize.X, titleY + _titleLabel.DesiredSize.Y));

        var stripY = 50f;
        var stripBottom = stripY + StripHeight;

        var statusY = stripBottom + 20f;
        _statusLabel.Arrange(new UIBox2(0, statusY, finalSize.X, statusY + _statusLabel.DesiredSize.Y));

        var rewardTypeY = statusY + 28f;
        _rewardTypeLabel.Arrange(new UIBox2(0, rewardTypeY, finalSize.X, rewardTypeY + _rewardTypeLabel.DesiredSize.Y));

        var resultY = rewardTypeY + 28f;
        _resultLabel.Arrange(new UIBox2(0, resultY, finalSize.X, resultY + _resultLabel.DesiredSize.Y));

        var buttonY = finalSize.Y - 55f;
        var buttonX = (finalSize.X - _openButton.DesiredSize.X) / 2f;
        _openButton.Arrange(new UIBox2(buttonX, buttonY, buttonX + _openButton.DesiredSize.X, buttonY + _openButton.DesiredSize.Y));

        var closeX = (finalSize.X - _closeButton.DesiredSize.X) / 2f;
        _closeButton.Arrange(new UIBox2(closeX, buttonY, closeX + _closeButton.DesiredSize.X, buttonY + _closeButton.DesiredSize.Y));

        return finalSize;
    }

    public void SetLootbox(string name, int userItemId, bool stelsHidden)
    {
        _lootboxName = name;
        _userItemId = userItemId;
        _stelsHidden = stelsHidden;
        _state = OpenerState.Initial;
        _result = null;
        _scrollCards.Clear();
        _scrollOffset = 0;
        _animationTime = 0;
        _random = new Random();

        var displayName = stelsHidden ? "???" : name;
        _titleLabel.Text = $"ОТКРЫТИЕ: {displayName.ToUpper()}";
        _statusLabel.Text = "Нажмите ОТКРЫТЬ чтобы испытать удачу";
        _statusLabel.TextColor = _textColor;
        _rewardTypeLabel.Visible = false;
        _rewardTypeLabel.Text = "";
        _resultLabel.Visible = false;
        _resultLabel.Text = "";

        _openButton.Visible = true;
        _openButton.Disabled = false;
        _closeButton.Visible = false;

        GeneratePreviewCards();
    }

    private void GeneratePreviewCards()
    {
        _scrollCards.Clear();

        for (var i = 0; i < VisibleCards; i++)
        {
            _scrollCards.Add(GetRandomRarity());
        }
    }

    private LootboxRarity GetRandomRarity()
    {
        var r = _random.NextSingle();
        if (r < 0.55f)
            return LootboxRarity.Common;
        if (r < 0.80f)
            return LootboxRarity.Epic;
        if (r < 0.95f)
            return LootboxRarity.Mythic;
        return LootboxRarity.Legendary;
    }

    public void OnOpenPressed()
    {
        if (_state != OpenerState.Initial)
            return;

        _state = OpenerState.WaitingResult;
        _openButton.Disabled = true;
        _openButton.Visible = false;
        _statusLabel.Text = "Открываем...";

        OnOpenRequested?.Invoke(_userItemId, _stelsHidden);
    }

    public void HandleOpenResult(LootboxOpenResult result)
    {
        _result = result;

        if (!result.Success)
        {
            _state = OpenerState.Complete;
            _statusLabel.Text = result.Message;
            _statusLabel.TextColor = Color.FromHex("#ff6b6b");
            _openButton.Visible = false;
            _closeButton.Visible = true;
            return;
        }

        if (result.StelsOpen)
        {
            var winRarity = result.Item?.Rarity ?? LootboxRarity.Common;
            GenerateScrollCardsWithWinner(winRarity, result.Sequence);
            StartScrollAnimation();
        }
        else
        {
            ShowImmediateResult();
        }
    }

    private void ShowImmediateResult()
    {
        _state = OpenerState.Complete;
        ShowFinalResult();
    }

    private void GenerateScrollCardsWithWinner(LootboxRarity winRarity, List<LootboxRarity>? sequence)
    {
        _scrollCards.Clear();

        if (sequence != null && sequence.Count > 0)
        {
            foreach (var rarity in sequence)
            {
                _scrollCards.Add(rarity);
            }
        }

        while (_scrollCards.Count < VisibleCards)
        {
            _scrollCards.Add(GetRandomRarity());
        }

        if (WinningCardPosition < _scrollCards.Count)
        {
            _scrollCards[WinningCardPosition] = winRarity;
        }
    }

    private void StartScrollAnimation()
    {
        _state = OpenerState.Scrolling;
        _animationTime = 0;
        _scrollOffset = 0;

        var cardFullWidth = (CardWidth + CardSpacing) * UIScale;
        var randomOffset = (_random.NextSingle() - 0.5f) * cardFullWidth * 0.6f;

        _targetScrollOffset = WinningCardPosition * cardFullWidth + randomOffset;

        _statusLabel.Text = "";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        switch (_state)
        {
            case OpenerState.Scrolling:
                UpdateScrollAnimation(args.DeltaSeconds);
                break;

            case OpenerState.Revealing:
                UpdateRevealAnimation(args.DeltaSeconds);
                break;
        }
    }

    private void UpdateScrollAnimation(float delta)
    {
        _animationTime += delta;

        var t = Math.Min(1f, _animationTime / SlowScrollDuration);

        var eased = EaseOutQuint(t);

        _scrollOffset = eased * _targetScrollOffset;

        if (t >= 1f)
        {
            _scrollOffset = _targetScrollOffset;
            FinishScrollAnimation();
        }
    }

    private float EaseOutQuint(float t)
    {
        return 1f - MathF.Pow(1f - t, 5f);
    }

    private void FinishScrollAnimation()
    {
        _state = OpenerState.Revealing;
        _animationTime = 0;
        _revealScale = 1f;
    }

    private void UpdateRevealAnimation(float delta)
    {
        _animationTime += delta;

        var t = Math.Min(1f, _animationTime / RevealDuration);
        _revealScale = 1f + MathF.Sin(t * MathF.PI) * 0.15f;

        if (t >= 1f)
        {
            _state = OpenerState.Complete;
            _revealScale = 1.1f;
            ShowFinalResult();
        }
    }

    private void ShowFinalResult()
    {
        if (_result == null)
            return;

        _statusLabel.Text = "ПОЗДРАВЛЯЕМ!";
        _statusLabel.TextColor = _indicatorColor;

        if (_result.RewardType == LootboxRewardType.Currency && _result.Currency != null)
        {
            var currencyName = GetCurrencyName(_result.Currency.CurrencyType);
            _rewardTypeLabel.Visible = true;
            _rewardTypeLabel.Text = "НАГРАДА: ВАЛЮТА";
            _rewardTypeLabel.TextColor = _currencyColor;

            _resultLabel.Visible = true;
            _resultLabel.Text = $"+{_result.Currency.Amount:F0} {currencyName.ToUpper()}";
            _resultLabel.TextColor = _currencyColor;
        }
        else if (_result.Item != null)
        {
            var rarityName = GetRarityName(_result.Item.Rarity);
            var rarityColor = GetRarityColor(_result.Item.Rarity);

            _rewardTypeLabel.Visible = true;
            _rewardTypeLabel.Text = $"РЕДКОСТЬ: {rarityName}";
            _rewardTypeLabel.TextColor = rarityColor;

            _resultLabel.Visible = true;
            _resultLabel.Text = _result.Item.Name.ToUpper();
            _resultLabel.TextColor = rarityColor;
        }
        else
        {
            _resultLabel.Visible = true;
            _resultLabel.Text = "Награда получена!";
            _resultLabel.TextColor = _indicatorColor;
        }

        _openButton.Visible = false;
        _closeButton.Visible = true;
    }

    private string GetCurrencyName(string currencyType)
    {
        return currencyType.ToLower() switch
        {
            "energy" => "Энергии",
            "crystals" => "Кристаллов",
            _ => currencyType
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (!_result?.StelsOpen ?? true)
        {
            if (_state == OpenerState.Complete && _result != null)
            {
                DrawResultCard(handle);
                return;
            }
        }

        if (!_stelsHidden)
        {
            return;
        }

        var stripY = 50f * UIScale;
        var stripHeight = StripHeight * UIScale;
        var stripRect = new UIBox2(0, stripY, PixelSize.X, stripY + stripHeight);

        handle.DrawRect(stripRect, _stripBgColor);

        var borderThickness = 2f * UIScale;
        handle.DrawRect(new UIBox2(stripRect.Left, stripRect.Top, stripRect.Right, stripRect.Top + borderThickness), _borderColor);
        handle.DrawRect(new UIBox2(stripRect.Left, stripRect.Bottom - borderThickness, stripRect.Right, stripRect.Bottom), _borderColor);

        var cardWidth = CardWidth * UIScale;
        var cardHeight = CardHeight * UIScale;
        var spacing = CardSpacing * UIScale;
        var cardFullWidth = cardWidth + spacing;

        var centerX = PixelSize.X / 2f;
        var cardY = stripY + (stripHeight - cardHeight) / 2f;

        if (_scrollCards.Count > 0)
        {
            var firstCardX = centerX - cardWidth / 2f - _scrollOffset;

            for (var i = 0; i < _scrollCards.Count; i++)
            {
                var cardX = firstCardX + i * cardFullWidth;

                if (cardX + cardWidth < -cardWidth || cardX > PixelSize.X + cardWidth)
                    continue;

                var distanceFromCenter = Math.Abs(cardX + cardWidth / 2f - centerX);
                var maxDistance = PixelSize.X / 2f;
                var alpha = 1f - Math.Min(0.7f, (distanceFromCenter / maxDistance) * 0.7f);

                var isWinningCard = i == WinningCardPosition && (_state == OpenerState.Complete || _state == OpenerState.Revealing);
                var scale = isWinningCard ? _revealScale : 1f;
                var cardAlpha = isWinningCard ? 1f : alpha;

                DrawCardAtPosition(handle, cardX, cardY, cardWidth, cardHeight, _scrollCards[i], cardAlpha, scale, isWinningCard);
            }
        }

        DrawIndicator(handle, centerX, stripRect);
    }

    private void DrawResultCard(DrawingHandleScreen handle)
    {
        var stripY = 50f * UIScale;
        var stripHeight = StripHeight * UIScale;
        var stripRect = new UIBox2(0, stripY, PixelSize.X, stripY + stripHeight);

        handle.DrawRect(stripRect, _stripBgColor);

        var borderThickness = 2f * UIScale;
        handle.DrawRect(new UIBox2(stripRect.Left, stripRect.Top, stripRect.Right, stripRect.Top + borderThickness), _borderColor);
        handle.DrawRect(new UIBox2(stripRect.Left, stripRect.Bottom - borderThickness, stripRect.Right, stripRect.Bottom), _borderColor);

        var cardWidth = CardWidth * UIScale * 1.5f;
        var cardHeight = CardHeight * UIScale * 1.2f;
        var centerX = PixelSize.X / 2f;
        var cardX = centerX - cardWidth / 2f;
        var cardY = stripY + (stripHeight - cardHeight) / 2f;

        if (_result?.RewardType == LootboxRewardType.Currency)
        {
            DrawCurrencyCard(handle, cardX, cardY, cardWidth, cardHeight);
        }
        else if (_result?.Item != null)
        {
            DrawCardAtPosition(handle, cardX, cardY, cardWidth, cardHeight, _result.Item.Rarity, 1f, 1f, true);
        }
    }

    private void DrawCurrencyCard(DrawingHandleScreen handle, float x, float y, float width, float height)
    {
        var rect = new UIBox2(x, y, x + width, y + height);

        handle.DrawRect(rect, Color.FromHex("#1a2a2a").WithAlpha(0.95f));

        var borderThickness = 3f * UIScale;
        var bc = _currencyColor;

        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + borderThickness), bc);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - borderThickness, rect.Right, rect.Bottom), bc);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + borderThickness, rect.Bottom), bc);
        handle.DrawRect(new UIBox2(rect.Right - borderThickness, rect.Top, rect.Right, rect.Bottom), bc);

        var glowOffset = 5f * UIScale;
        var glowRect = new UIBox2(rect.Left - glowOffset, rect.Top - glowOffset, rect.Right + glowOffset, rect.Bottom + glowOffset);
        var glowThickness = 2f * UIScale;
        var gc = _currencyColor.WithAlpha(0.5f);

        handle.DrawRect(new UIBox2(glowRect.Left, glowRect.Top, glowRect.Right, glowRect.Top + glowThickness), gc);
        handle.DrawRect(new UIBox2(glowRect.Left, glowRect.Bottom - glowThickness, glowRect.Right, glowRect.Bottom), gc);
        handle.DrawRect(new UIBox2(glowRect.Left, glowRect.Top, glowRect.Left + glowThickness, glowRect.Bottom), gc);
        handle.DrawRect(new UIBox2(glowRect.Right - glowThickness, glowRect.Top, glowRect.Right, glowRect.Bottom), gc);

        var currencyText = "ВАЛЮТА";
        var textWidth = GetTextWidth(currencyText, _rarityFont);
        var textX = rect.Left + (width - textWidth) / 2f;
        var textY = rect.Bottom - 35f * UIScale;

        handle.DrawString(_rarityFont, new Vector2(textX, textY), currencyText, UIScale, _currencyColor);
    }

    private void DrawCardAtPosition(DrawingHandleScreen handle, float x, float y, float width, float height, LootboxRarity rarity, float alpha, float scale, bool highlight)
    {
        var centerX = x + width / 2f;
        var centerY = y + height / 2f;

        var scaledWidth = width * scale;
        var scaledHeight = height * scale;
        var scaledX = centerX - scaledWidth / 2f;
        var scaledY = centerY - scaledHeight / 2f;

        var (bgColor, borderColor) = GetRarityColors(rarity);
        var rect = new UIBox2(scaledX, scaledY, scaledX + scaledWidth, scaledY + scaledHeight);

        handle.DrawRect(rect, bgColor.WithAlpha(alpha * 0.95f));

        var borderThickness = highlight ? 3f * UIScale : 2f * UIScale;
        var bc = borderColor.WithAlpha(alpha);

        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, rect.Top + borderThickness), bc);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - borderThickness, rect.Right, rect.Bottom), bc);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Left + borderThickness, rect.Bottom), bc);
        handle.DrawRect(new UIBox2(rect.Right - borderThickness, rect.Top, rect.Right, rect.Bottom), bc);

        if (highlight)
        {
            var glowOffset = 5f * UIScale;
            var glowRect = new UIBox2(rect.Left - glowOffset, rect.Top - glowOffset, rect.Right + glowOffset, rect.Bottom + glowOffset);
            var glowThickness = 2f * UIScale;
            var gc = borderColor.WithAlpha(alpha * 0.5f);

            handle.DrawRect(new UIBox2(glowRect.Left, glowRect.Top, glowRect.Right, glowRect.Top + glowThickness), gc);
            handle.DrawRect(new UIBox2(glowRect.Left, glowRect.Bottom - glowThickness, glowRect.Right, glowRect.Bottom), gc);
            handle.DrawRect(new UIBox2(glowRect.Left, glowRect.Top, glowRect.Left + glowThickness, glowRect.Bottom), gc);
            handle.DrawRect(new UIBox2(glowRect.Right - glowThickness, glowRect.Top, glowRect.Right, glowRect.Bottom), gc);
        }

        var gemSize = 28f * UIScale * scale;
        var gemCenterX = rect.Left + scaledWidth / 2f;
        var gemCenterY = rect.Top + scaledHeight * 0.35f;
        var gemColor = borderColor.WithAlpha(alpha);

        handle.DrawLine(new Vector2(gemCenterX, gemCenterY - gemSize / 2f), new Vector2(gemCenterX + gemSize / 2f, gemCenterY), gemColor);
        handle.DrawLine(new Vector2(gemCenterX + gemSize / 2f, gemCenterY), new Vector2(gemCenterX, gemCenterY + gemSize / 2f), gemColor);
        handle.DrawLine(new Vector2(gemCenterX, gemCenterY + gemSize / 2f), new Vector2(gemCenterX - gemSize / 2f, gemCenterY), gemColor);
        handle.DrawLine(new Vector2(gemCenterX - gemSize / 2f, gemCenterY), new Vector2(gemCenterX, gemCenterY - gemSize / 2f), gemColor);
        handle.DrawLine(new Vector2(gemCenterX - gemSize / 2f, gemCenterY), new Vector2(gemCenterX + gemSize / 2f, gemCenterY), gemColor);
        handle.DrawLine(new Vector2(gemCenterX, gemCenterY - gemSize / 2f), new Vector2(gemCenterX, gemCenterY + gemSize / 2f), gemColor);

        var rarityName = GetRarityName(rarity);
        var textWidth = GetTextWidth(rarityName, _rarityFont);
        var textX = rect.Left + (scaledWidth - textWidth) / 2f;
        var textY = rect.Bottom - 35f * UIScale * scale;

        handle.DrawString(_rarityFont, new Vector2(textX, textY), rarityName, UIScale * scale, borderColor.WithAlpha(alpha));
    }

    private void DrawIndicator(DrawingHandleScreen handle, float centerX, UIBox2 stripRect)
    {
        var indicatorWidth = 3f * UIScale;
        var indicatorX = centerX - indicatorWidth / 2f;

        handle.DrawRect(new UIBox2(indicatorX, stripRect.Top, indicatorX + indicatorWidth, stripRect.Bottom), _indicatorColor);

        var triangleSize = 14f * UIScale;

        var topTriangle = new Vector2[]
        {
            new(centerX, stripRect.Top + triangleSize + 2f * UIScale),
            new(centerX - triangleSize / 2f, stripRect.Top + 2f * UIScale),
            new(centerX + triangleSize / 2f, stripRect.Top + 2f * UIScale)
        };

        var bottomTriangle = new Vector2[]
        {
            new(centerX, stripRect.Bottom - triangleSize - 2f * UIScale),
            new(centerX - triangleSize / 2f, stripRect.Bottom - 2f * UIScale),
            new(centerX + triangleSize / 2f, stripRect.Bottom - 2f * UIScale)
        };

        DrawFilledTriangle(handle, topTriangle, _indicatorColor);
        DrawFilledTriangle(handle, bottomTriangle, _indicatorColor);
    }

    private void DrawFilledTriangle(DrawingHandleScreen handle, Vector2[] points, Color color)
    {
        if (points.Length < 3)
            return;

        var minY = MathF.Min(points[0].Y, MathF.Min(points[1].Y, points[2].Y));
        var maxY = MathF.Max(points[0].Y, MathF.Max(points[1].Y, points[2].Y));

        for (var y = minY; y <= maxY; y += 1f)
        {
            var intersections = new List<float>();

            for (var i = 0; i < 3; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % 3];

                if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                {
                    var t = (y - p1.Y) / (p2.Y - p1.Y);
                    intersections.Add(p1.X + t * (p2.X - p1.X));
                }
            }

            if (intersections.Count >= 2)
            {
                intersections.Sort();
                handle.DrawRect(new UIBox2(intersections[0], y, intersections[1], y + 1f), color);
            }
        }
    }

    private (Color bg, Color border) GetRarityColors(LootboxRarity rarity)
    {
        return rarity switch
        {
            LootboxRarity.Common => (_commonBgColor, _commonColor),
            LootboxRarity.Epic => (_epicBgColor, _epicColor),
            LootboxRarity.Mythic => (_mythicBgColor, _mythicColor),
            LootboxRarity.Legendary => (_legendaryBgColor, _legendaryColor),
            _ => (_commonBgColor, _commonColor)
        };
    }

    private string GetRarityName(LootboxRarity rarity)
    {
        return rarity switch
        {
            LootboxRarity.Common => "РЕДКИЙ",
            LootboxRarity.Epic => "ЭПИЧЕСКИЙ",
            LootboxRarity.Mythic => "МИФИЧЕСКИЙ",
            LootboxRarity.Legendary => "ЛЕГЕНДА",
            _ => "РЕДКИЙ"
        };
    }

    private Color GetRarityColor(LootboxRarity rarity)
    {
        return rarity switch
        {
            LootboxRarity.Common => _commonColor,
            LootboxRarity.Epic => _epicColor,
            LootboxRarity.Mythic => _mythicColor,
            LootboxRarity.Legendary => _legendaryColor,
            _ => _commonColor
        };
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
}
