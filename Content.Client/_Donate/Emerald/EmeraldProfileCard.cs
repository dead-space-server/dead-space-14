// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Donate.Emerald;

public sealed partial class EmeraldProfileCard : Control
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private const int BaseNameFontSize = 12;
    private const int BaseIdFontSize = 9;

    private Font _nameFont = default!;
    private Font _idFont = default!;

    private string _playerName = "";
    private string _playerId = "";

    private readonly Color _bgColor = Color.FromHex("#1a0f2e");
    private readonly Color _borderColor = Color.FromHex("#4a3a6a");
    private readonly Color _nameColor = Color.FromHex("#c0b3da");
    private readonly Color _idColor = Color.FromHex("#8975b5");

    public string PlayerName
    {
        get => _playerName;
        set
        {
            _playerName = value;
            InvalidateMeasure();
        }
    }

    public string PlayerId
    {
        get => _playerId;
        set
        {
            _playerId = value;
            InvalidateMeasure();
        }
    }

    public EmeraldProfileCard()
    {
        IoCManager.InjectDependencies(this);

        var fontRes = _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf");
        _nameFont = new VectorFont(fontRes, BaseNameFontSize);
        _idFont = new VectorFont(fontRes, BaseIdFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsPositiveInfinity(availableSize.X) ? 300 : availableSize.X;
        return new Vector2(width, 50);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var rect = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);

        handle.DrawRect(rect, _bgColor.WithAlpha(0.8f));

        handle.DrawLine(rect.TopLeft, rect.TopRight, _borderColor);
        handle.DrawLine(rect.TopRight, rect.BottomRight, _borderColor);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, _borderColor);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, _borderColor);

        var padding = 10f * UIScale;
        var nameY = 10f * UIScale;
        handle.DrawString(_nameFont, new Vector2(padding, nameY), _playerName, UIScale, _nameColor);

        var idY = nameY + _nameFont.GetLineHeight(UIScale) + 4f * UIScale;
        handle.DrawString(_idFont, new Vector2(padding, idY), _playerId, UIScale, _idColor);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }
}
