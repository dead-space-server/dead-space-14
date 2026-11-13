// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Donate.Emerald;

[Virtual]
public partial class EmeraldDefaultWindow : EmeraldBaseWindow
{
    [Dependency] private IResourceCache _resourceCache = default!;

    public float WindowEdgeSeparation = 30;
    public float WindowEdgeBump = 50;
    public DirectionFlag AllowOffScreen = ~DirectionFlag.North;

    private const int DragMarginSize = 7;
    private const float BaseHeaderHeight = 30f;
    private const float BaseCloseButtonSize = 20f;
    private const float BaseContentPadding = 10f;
    private const int BaseTitleFontSize = 14;

    private Font _titleFont = default!;
    private string _title = "Window";
    private Control _contents;
    private bool _isHoveringClose;
    private bool _isPressingClose;

    private readonly Color _titleColor = Color.FromHex("#c0b3da");
    private readonly Color _closeNormalColor = Color.FromHex("#ff6b6b");
    private readonly Color _closeHoverColor = Color.FromHex("#ff8a8a");
    private readonly Color _closePressColor = Color.FromHex("#ffaaaa");
    private readonly Color _headerBgColor = Color.FromHex("#0f0a1e");
    private readonly Color _separatorColor = Color.FromHex("#4a3a6a");

    public bool TransparentHeader { get; set; } = true;

    private float HeaderHeight => BaseHeaderHeight * UIScale;
    private float CloseButtonSize => BaseCloseButtonSize * UIScale;
    private float ContentPadding => BaseContentPadding * UIScale;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            InvalidateMeasure();
        }
    }

    public Control Contents => _contents;

    protected virtual Vector2 ContentsMinimumSize => new Vector2(100, 50);

    public EmeraldDefaultWindow()
    {
        IoCManager.InjectDependencies(this);

        MinSize = new Vector2(150, 80);

        _contents = new Control
        {
            Margin = new Thickness(BaseContentPadding),
            RectClipContent = true
        };

        AddChild(_contents);

        _titleFont = new VectorFont(
            _resourceCache.GetResource<FontResource>("/Fonts/Bedstead/Bedstead.otf"),
            BaseTitleFontSize);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var contentMin = ContentsMinimumSize;
        var minWidth = Math.Max(200f, contentMin.X + ContentPadding * 2);
        var minHeight = HeaderHeight + contentMin.Y + ContentPadding * 2;

        return Vector2.Max(
            new Vector2(minWidth, minHeight),
            base.MeasureOverride(Vector2.Max(availableSize, new Vector2(minWidth, minHeight))));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var contentBox = new UIBox2(
            ContentPadding,
            HeaderHeight + ContentPadding,
            finalSize.X - ContentPadding,
            finalSize.Y - ContentPadding);

        _contents.Arrange(contentBox);

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var pixelSize = PixelSize;
        var rect = new UIBox2(0, 0, pixelSize.X, pixelSize.Y);

        DrawWindowBorder(handle, rect, false, TransparentHeader);

        DrawHeader(handle, pixelSize);

        DrawCloseButton(handle, pixelSize);
    }

    private void DrawHeader(DrawingHandleScreen handle, Vector2 pixelSize)
    {
        var headerRect = new UIBox2(1, 1, pixelSize.X - 1, HeaderHeight);
        var headerBgColor = TransparentHeader ? _headerBgColor.WithAlpha(0.7f) : _headerBgColor;
        handle.DrawRect(headerRect, headerBgColor);

        var separatorY = HeaderHeight;
        handle.DrawLine(
            new Vector2(1, separatorY),
            new Vector2(pixelSize.X - 1, separatorY),
            _separatorColor);

        var titleWidth = GetTextWidth(_title);
        var maxTitleWidth = pixelSize.X - CloseButtonSize - 20f * UIScale;
        var displayTitle = _title;

        if (titleWidth > maxTitleWidth)
        {
            displayTitle = TruncateText(_title, maxTitleWidth);
        }

        var titleX = 10f * UIScale;
        var titleY = (HeaderHeight - _titleFont.GetLineHeight(UIScale)) / 2f;

        handle.DrawString(_titleFont, new Vector2(titleX, titleY), displayTitle, UIScale, _titleColor);
    }

    private void DrawCloseButton(DrawingHandleScreen handle, Vector2 pixelSize)
    {
        var buttonSize = CloseButtonSize;
        var buttonX = pixelSize.X - buttonSize - 5f * UIScale;
        var buttonY = (HeaderHeight - buttonSize) / 2f;
        var buttonRect = new UIBox2(buttonX, buttonY, buttonX + buttonSize, buttonY + buttonSize);

        var bgColor = _isPressingClose ? _closePressColor.WithAlpha(0.2f) :
                      _isHoveringClose ? _closeHoverColor.WithAlpha(0.15f) :
                      Color.Transparent;

        if (bgColor.A > 0)
        {
            handle.DrawRect(buttonRect, bgColor);
        }

        var xColor = _isPressingClose ? _closePressColor :
                     _isHoveringClose ? _closeHoverColor :
                     _closeNormalColor;

        var centerX = buttonX + buttonSize / 2f;
        var centerY = buttonY + buttonSize / 2f;
        var crossSize = buttonSize * 0.4f;

        handle.DrawLine(
            new Vector2(centerX - crossSize, centerY - crossSize),
            new Vector2(centerX + crossSize, centerY + crossSize),
            xColor);

        handle.DrawLine(
            new Vector2(centerX + crossSize, centerY - crossSize),
            new Vector2(centerX - crossSize, centerY + crossSize),
            xColor);
    }

    private string TruncateText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var ellipsis = "...";
        var ellipsisWidth = GetTextWidth(ellipsis);

        if (maxWidth <= ellipsisWidth)
            return ellipsis;

        var truncated = "";
        var currentWidth = 0f;

        foreach (var rune in text.EnumerateRunes())
        {
            var metrics = _titleFont.GetCharMetrics(rune, UIScale);
            if (metrics.HasValue)
            {
                if (currentWidth + metrics.Value.Advance + ellipsisWidth > maxWidth)
                    break;
                currentWidth += metrics.Value.Advance;
            }
            truncated += rune.ToString();
        }

        return truncated + ellipsis;
    }

    private float GetTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            var metrics = _titleFont.GetCharMetrics(rune, UIScale);
            if (metrics.HasValue)
                width += metrics.Value.Advance;
        }
        return width;
    }

    private bool IsOverCloseButton(Vector2 relativePos)
    {
        var buttonX = Size.X - CloseButtonSize / UIScale - 5f;
        var buttonY = (BaseHeaderHeight - BaseCloseButtonSize) / 2f;

        return relativePos.X >= buttonX &&
               relativePos.X <= buttonX + BaseCloseButtonSize &&
               relativePos.Y >= buttonY &&
               relativePos.Y <= buttonY + BaseCloseButtonSize;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        var wasHovering = _isHoveringClose;
        _isHoveringClose = IsOverCloseButton(args.RelativePosition);

        if (_isHoveringClose && !wasHovering)
        {
            UserInterfaceManager.HoverSound();
        }
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _isHoveringClose = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (IsOverCloseButton(args.RelativePosition))
            {
                _isPressingClose = true;
                args.Handle();
                return;
            }
        }

        base.KeyBindDown(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (_isPressingClose)
            {
                _isPressingClose = false;

                if (IsOverCloseButton(args.RelativePosition))
                {
                    UserInterfaceManager.ClickSound();
                    Close();
                    args.Handle();
                    return;
                }
            }
        }

        base.KeyBindUp(args);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (IsOverCloseButton(relativeMousePos))
            return DragMode.None;

        var mode = DragMode.None;

        if (Resizable)
        {
            if (relativeMousePos.Y < DragMarginSize)
            {
                mode = DragMode.Top;
            }
            else if (relativeMousePos.Y > Size.Y - DragMarginSize)
            {
                mode = DragMode.Bottom;
            }

            if (relativeMousePos.X < DragMarginSize)
            {
                mode |= DragMode.Left;
            }
            else if (relativeMousePos.X > Size.X - DragMarginSize)
            {
                mode |= DragMode.Right;
            }
        }

        if (mode == DragMode.None && relativeMousePos.Y < BaseHeaderHeight)
        {
            mode = DragMode.Move;
        }

        return mode;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Parent == null)
            return;

        var (spaceX, spaceY) = Parent.Size;

        var maxX = spaceX - ((AllowOffScreen & DirectionFlag.West) == 0 ? Size.X : WindowEdgeSeparation);
        var maxY = spaceY - ((AllowOffScreen & DirectionFlag.South) == 0 ? Size.Y : WindowEdgeSeparation);

        if (Position.X > spaceX)
            maxX -= WindowEdgeBump;

        if (Position.Y > spaceY)
            maxY -= WindowEdgeBump;

        var pos = Vector2.Min(Position, new Vector2(maxX, maxY));

        var minX = (AllowOffScreen & DirectionFlag.East) == 0 ? 0 : WindowEdgeSeparation - Size.X;
        var minY = (AllowOffScreen & DirectionFlag.North) == 0 ? 0 : WindowEdgeSeparation - Size.Y;

        pos = Vector2.Max(pos, new Vector2(minX, minY));

        if (Position != pos)
            LayoutContainer.SetPosition(this, pos);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        InvalidateMeasure();
    }

    protected override void Resized()
    {
        base.Resized();
        InvalidateArrange();
    }

    public void AddContent(Control control)
    {
        _contents.AddChild(control);
    }

    public void RemoveContent(Control control)
    {
        _contents.RemoveChild(control);
    }

    public void ClearContents()
    {
        _contents.RemoveAllChildren();
    }
}
