// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Donate.Emerald;

public abstract class EmeraldBaseWindow : Control
{
    [Flags]
    protected enum DragMode : byte
    {
        None = 0,
        Move = 1,
        Top = 1 << 1,
        Bottom = 1 << 2,
        Left = 1 << 3,
        Right = 1 << 4,
    }

    private DragMode _currentDrag = DragMode.None;
    private Vector2 _dragOffsetTopLeft;
    private Vector2 _dragOffsetBottomRight;
    private bool _resizable = true;

    protected readonly Color WindowBgColor = Color.FromHex("#0f0a1e");
    protected readonly Color BorderColor = Color.FromHex("#6d5a8a");
    protected readonly Color BorderHoverColor = Color.FromHex("#a589c9");
    protected readonly Color GlowColor = Color.FromHex("#c0b3da");
    protected readonly Color AccentColor = Color.FromHex("#a589c9");

    public bool Resizable
    {
        get => _resizable;
        set => _resizable = value;
    }

    public bool IsOpen => Parent != null;

    public event Action? OnClose;
    public event Action? OnOpen;

    protected EmeraldBaseWindow()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    public virtual void Close()
    {
        if (Parent == null)
            return;

        Parent.RemoveChild(this);
        OnClose?.Invoke();
    }

    public void Open()
    {
        if (!Visible)
            Visible = true;

        if (!IsOpen)
            UserInterfaceManager.WindowRoot.AddChild(this);

        Opened();
        OnOpen?.Invoke();
    }

    public void OpenCentered() => OpenCenteredAt(new Vector2(0.5f, 0.5f));

    public void OpenToLeft() => OpenCenteredAt(new Vector2(0, 0.5f));

    public void OpenCenteredLeft() => OpenCenteredAt(new Vector2(0.25f, 0.5f));

    public void OpenToRight() => OpenCenteredAt(new Vector2(1, 0.5f));

    public void OpenCenteredRight() => OpenCenteredAt(new Vector2(0.75f, 0.5f));

    public void OpenCenteredAt(Vector2 relativePosition)
    {
        Measure(Vector2Helpers.Infinity);
        Open();
        RecenterWindow(relativePosition);
    }

    public void RecenterWindow(Vector2 relativePosition)
    {
        if (Parent == null)
            return;

        var corner = Parent!.Size * Vector2.Clamp(relativePosition, Vector2.Zero, Vector2.One) - DesiredSize / 2;
        var pos = Vector2.Clamp(corner, Vector2.Zero, Parent.Size - DesiredSize);
        LayoutContainer.SetPosition(this, pos);
    }

    protected virtual void Opened()
    {
    }

    public void MoveToFront()
    {
        if (Parent == null)
            throw new InvalidOperationException("This window is not currently open.");

        SetPositionLast();
    }

    public bool IsAtFront()
    {
        if (Parent == null)
            throw new InvalidOperationException("This window is not currently open");

        var siblingCount = Parent.ChildCount;
        var ourPos = GetPositionInParent();

        for (var i = ourPos + 1; i < siblingCount; i++)
        {
            if (Parent.GetChild(i).Visible)
                return false;
        }

        return true;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _currentDrag = GetDragModeFor(args.RelativePosition);

        if (_currentDrag != DragMode.None)
        {
            _dragOffsetTopLeft = args.PointerLocation.Position / UIScale - Position;
            _dragOffsetBottomRight = Position + Size - args.PointerLocation.Position / UIScale;
        }

        MoveToFront();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragOffsetTopLeft = _dragOffsetBottomRight = Vector2.Zero;
        _currentDrag = DragMode.None;

        UserInterfaceManager.KeyboardFocused?.ReleaseKeyboardFocus();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (Parent == null)
            return;

        if (_currentDrag == DragMode.Move)
        {
            var globalPos = args.GlobalPosition;
            globalPos = Vector2.Clamp(globalPos, Vector2.Zero, Parent.Size);
            LayoutContainer.SetPosition(this, globalPos - _dragOffsetTopLeft);
            return;
        }

        if (!Resizable)
            return;

        if (_currentDrag == DragMode.None)
        {
            UpdateResizeCursor(args.RelativePosition);
        }
        else
        {
            PerformResize(args);
        }
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        if (Resizable && _currentDrag == DragMode.None)
            DefaultCursorShape = CursorShape.Arrow;
    }

    private void UpdateResizeCursor(Vector2 relativePosition)
    {
        var cursor = CursorShape.Arrow;
        var previewDragMode = GetDragModeFor(relativePosition);

        switch (previewDragMode)
        {
            case DragMode.Top:
            case DragMode.Bottom:
                cursor = CursorShape.VResize;
                break;

            case DragMode.Left:
            case DragMode.Right:
                cursor = CursorShape.HResize;
                break;

            case DragMode.Bottom | DragMode.Left:
            case DragMode.Top | DragMode.Right:
            case DragMode.Bottom | DragMode.Right:
            case DragMode.Top | DragMode.Left:
                cursor = CursorShape.Crosshair;
                break;
        }

        DefaultCursorShape = cursor;
    }

    private void PerformResize(GUIMouseMoveEventArgs args)
    {
        var (left, top) = Position;
        var (right, bottom) = Position + SetSize;

        if (float.IsNaN(SetSize.X))
            right = Position.X + Size.X;
        if (float.IsNaN(SetSize.Y))
            bottom = Position.Y + Size.Y;

        if ((_currentDrag & DragMode.Top) == DragMode.Top)
            top = Math.Min(args.GlobalPosition.Y - _dragOffsetTopLeft.Y, Math.Min(bottom, bottom - MinSize.Y));
        else if ((_currentDrag & DragMode.Bottom) == DragMode.Bottom)
            bottom = Math.Max(args.GlobalPosition.Y + _dragOffsetBottomRight.Y, Math.Max(top, top + MinSize.Y));

        if ((_currentDrag & DragMode.Left) == DragMode.Left)
            left = Math.Min(args.GlobalPosition.X - _dragOffsetTopLeft.X, Math.Min(right, right - MinSize.X));
        else if ((_currentDrag & DragMode.Right) == DragMode.Right)
            right = Math.Max(args.GlobalPosition.X + _dragOffsetBottomRight.X, Math.Max(left, left + MinSize.X));

        var rect = new UIBox2(left, top, right, bottom);
        LayoutContainer.SetPosition(this, rect.TopLeft);
        SetSize = rect.Size;
    }

    protected virtual DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.None;
    }

    protected void DrawWindowBorder(DrawingHandleScreen handle, UIBox2 rect, bool focused = false, bool transparent = true)
    {
        var bgColor = transparent ? WindowBgColor.WithAlpha(0.95f) : WindowBgColor;
        handle.DrawRect(rect, bgColor);

        var borderColor = focused ? BorderHoverColor : BorderColor;
        DrawBorderLines(handle, rect, borderColor);

        if (focused)
        {
            var glowRect = new UIBox2(rect.Left - 1, rect.Top - 1, rect.Right + 1, rect.Bottom + 1);
            DrawBorderLines(handle, glowRect, GlowColor.WithAlpha(0.3f));
        }
    }

    protected void DrawBorderLines(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        handle.DrawLine(rect.TopLeft, rect.TopRight, color);
        handle.DrawLine(rect.TopRight, rect.BottomRight, color);
        handle.DrawLine(rect.BottomRight, rect.BottomLeft, color);
        handle.DrawLine(rect.BottomLeft, rect.TopLeft, color);
    }
}
