// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public sealed class UiConstructorSurface : Container
{
    public event Action? Changed;
    public event Action? SelectionChanged;
    public event Action<string>? Status;

    private readonly UiLayoutFactory _factory;
    private readonly GridOverlay _overlay;
    private readonly Dictionary<int, Control> _controls = new();
    private UiLayoutDocument _document;
    private bool _editing = true;
    private UiElementKind? _armed;
    private Drag _drag = Drag.None;
    private bool _pressed;
    private bool _moved;
    private bool _placeOnUp;
    private Vector2 _pressPosition;
    private int _grabColumn;
    private int _grabRow;
    private int _originColumn;
    private int _originRow;
    private int _originColumnSpan;
    private int _originRowSpan;
    private UIBox2? _hover;
    private bool _hoverValid = true;

    public UiConstructorSurface(UiLayoutDocument document, UiLayoutFactory factory, bool editing)
    {
        _document = document;
        _factory = factory;
        _editing = editing;
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        HorizontalExpand = true;
        VerticalExpand = true;
        _overlay = new GridOverlay(this)
        {
            Visible = editing,
            MouseFilter = editing ? MouseFilterMode.Stop : MouseFilterMode.Ignore,
        };
        AddChild(_overlay);
        Rebuild();
    }

    public UiLayoutDocument Document => _document;

    public UiLayoutElement? Selected { get; private set; }

    public UiElementKind? Armed
    {
        get => _armed;
        set
        {
            _armed = value;
            if (value == null)
                _hover = null;
        }
    }

    public bool Editing
    {
        get => _editing;
        set
        {
            _editing = value;
            _overlay.Visible = value;
            _overlay.MouseFilter = value ? MouseFilterMode.Stop : MouseFilterMode.Ignore;
            if (!value)
            {
                _drag = Drag.None;
                _pressed = false;
                _armed = null;
                _hover = null;
            }
        }
    }

    public void SetDocument(UiLayoutDocument document)
    {
        _document = document;
        Selected = null;
        _armed = null;
        _hover = null;
        Rebuild();
        SelectionChanged?.Invoke();
        Changed?.Invoke();
    }

    public bool TryDrop(UiElementKind kind, Vector2 global)
    {
        var local = global - GlobalPosition;
        if (local.X < 0 || local.Y < 0 || local.X > Size.X || local.Y > Size.Y)
            return false;

        return Place(kind, local);
    }

    public bool TrySetGrid(int column, int row, int columnSpan, int rowSpan)
    {
        var element = Selected;
        if (element == null)
            return false;

        columnSpan = Math.Clamp(columnSpan, 1, _document.Columns);
        rowSpan = Math.Clamp(rowSpan, 1, _document.Rows);
        column = Math.Clamp(column, 0, _document.Columns - columnSpan);
        row = Math.Clamp(row, 0, _document.Rows - rowSpan);
        if (UiLayoutMath.Overlaps(_document, element, column, row, columnSpan, rowSpan))
            return false;

        element.Column = column;
        element.Row = row;
        element.ColumnSpan = columnSpan;
        element.RowSpan = rowSpan;
        InvalidateArrange();
        Changed?.Invoke();
        return true;
    }

    public void Remount()
    {
        Rebuild();
    }

    public void SyncSelected()
    {
        if (Selected != null && _controls.TryGetValue(Selected.Id, out var control))
            _factory.Apply(control, Selected);

        InvalidateArrange();
    }

    public void DeleteSelected()
    {
        if (Selected == null)
            return;

        var removed = Selected;
        RemoveTree(removed);
        Selected = null;
        RaiseOverlay();
        SelectionChanged?.Invoke();
        Changed?.Invoke();
    }

    public void DuplicateSelected()
    {
        if (Selected == null)
            return;

        var copy = Selected.Clone();
        copy.Id = _document.NextId++;
        copy.Name += "Copy";
        var column = copy.Column;
        var row = copy.Row;
        if (copy.ParentId == 0 && !UiLayoutMath.TryFindFree(_document, copy.ColumnSpan, copy.RowSpan, out column, out row))
        {
            _document.NextId = copy.Id;
            Status?.Invoke("Свободной клетки под копию нет.");
            return;
        }

        if (copy.ParentId == 0)
        {
            copy.Column = column;
            copy.Row = row;
        }
        else
        {
            copy.Order++;
        }
        _document.Elements.Add(copy);
        AddControl(copy);
        OrderControls();
        Select(copy);
        Changed?.Invoke();
    }

    public void Relayout()
    {
        UiLayoutMath.ClampGrid(_document);
        InvalidateArrange();
        Changed?.Invoke();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = availableSize.X;
        var height = availableSize.Y;
        if (float.IsInfinity(width) || width < 1)
            width = 320;
        if (float.IsInfinity(height) || height < 1)
            height = 120;

        foreach (var child in Children)
            child.Measure(new Vector2(width, height));

        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var element in _document.Elements)
        {
            if (element.ParentId != 0)
                continue;

            if (!_controls.TryGetValue(element.Id, out var control))
                continue;

            control.Arrange(CellRect(element, finalSize));
        }

        _overlay.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(new UIBox2(0, 0, PixelWidth, PixelHeight), new Color(0.02f, 0.03f, 0.05f, 0.92f));
    }

    private void Rebuild()
    {
        foreach (var child in Children.ToArray())
        {
            if (child == _overlay)
                continue;

            RemoveChild(child);
        }

        _controls.Clear();
        foreach (var element in _document.Elements)
            _controls[element.Id] = _factory.Create(element);

        foreach (var element in _document.Elements)
        {
            if (element.ParentId != 0 || !UiElementKinds.IsBackdrop(element.Kind))
                continue;

            AddChild(_controls[element.Id]);
        }

        foreach (var element in _document.Elements)
        {
            if (element.ParentId != 0 || UiElementKinds.IsBackdrop(element.Kind))
                continue;

            AddChild(_controls[element.Id]);
        }

        foreach (var element in _document.Elements.OrderBy(element => element.Order).ThenBy(element => element.Id))
        {
            if (element.ParentId == 0 || !_controls.TryGetValue(element.ParentId, out var parent))
                continue;

            parent.AddChild(_controls[element.Id]);
        }

        RaiseOverlay();
        InvalidateArrange();
    }

    private void AddControl(UiLayoutElement element)
    {
        var control = _factory.Create(element);
        _controls[element.Id] = control;
        if (element.ParentId != 0 && _controls.TryGetValue(element.ParentId, out var parent))
            parent.AddChild(control);
        else
            AddChild(control);
        RaiseOverlay();
    }

    public void Reload()
    {
        Selected = null;
        _armed = null;
        _hover = null;
        Rebuild();
        SelectionChanged?.Invoke();
        Changed?.Invoke();
    }

    private void OrderControls()
    {
        foreach (var element in _document.Elements)
        {
            if (!UiElementKinds.IsBackdrop(element.Kind))
                continue;

            if (_controls.TryGetValue(element.Id, out var control) && control.Parent == this)
            {
                RemoveChild(control);
                AddChild(control);
            }
        }

        foreach (var element in _document.Elements)
        {
            if (UiElementKinds.IsBackdrop(element.Kind))
                continue;

            if (_controls.TryGetValue(element.Id, out var control) && control.Parent == this)
            {
                RemoveChild(control);
                AddChild(control);
            }
        }

        RaiseOverlay();
    }

    private void RaiseOverlay()
    {
        if (_overlay.Parent == this)
            RemoveChild(_overlay);

        AddChild(_overlay);
    }

    private bool Place(UiElementKind kind, Vector2 local)
    {
        if (Size.X < 1 || Size.Y < 1)
            return false;

        var occupied = Hit(local);
        var host = HostBox(occupied);
        if (host != null)
            return PlaceInside(kind, host);

        if (occupied != null && !UiElementKinds.IsBackdrop(occupied.Kind))
        {
            Status?.Invoke("Клетка занята.");
            return false;
        }

        var cell = CellAt(local);
        var element = UiLayoutMath.Create(_document, kind, cell.X, cell.Y);
        UiLayoutMath.ClampElement(_document, element);
        if (UiLayoutMath.Overlaps(_document, null, element.Column, element.Row, element.ColumnSpan, element.RowSpan))
        {
            _document.NextId = element.Id;
            Status?.Invoke("Клетка занята.");
            return false;
        }

        _document.Elements.Add(element);
        AddControl(element);
        OrderControls();
        Select(element);
        _armed = null;
        _hover = null;
        Changed?.Invoke();
        return true;
    }

    private bool PlaceInside(UiElementKind kind, UiLayoutElement host)
    {
        var element = UiLayoutMath.Create(_document, kind, host.Column, host.Row);
        element.ParentId = host.Id;
        if (host.Kind == UiElementKind.Grid)
            element.HorizontalExpand = true;
        foreach (var sibling in _document.Elements)
        {
            if (sibling.ParentId == host.Id)
                element.Order = Math.Max(element.Order, sibling.Order + 1);
        }

        _document.Elements.Add(element);
        AddControl(element);
        Select(element);
        _armed = null;
        _hover = null;
        Changed?.Invoke();
        return true;
    }

    private UiLayoutElement? HostBox(UiLayoutElement? element)
    {
        while (element != null)
        {
            if (element.Kind == UiElementKind.Box)
                return element;

            if (element.Kind is UiElementKind.Grid or UiElementKind.Panel or UiElementKind.ProgressBar)
                return element;

            if (element.ParentId == 0)
                return null;

            var parentId = element.ParentId;
            element = _document.Elements.FirstOrDefault(item => item.Id == parentId);
        }

        return null;
    }

    private void RemoveTree(UiLayoutElement element)
    {
        foreach (var child in _document.Elements.Where(item => item.ParentId == element.Id).ToArray())
            RemoveTree(child);

        _document.Elements.Remove(element);
        if (_controls.Remove(element.Id, out var control))
            control.Orphan();
    }

    public void SelectId(int id)
    {
        Select(_document.Elements.FirstOrDefault(item => item.Id == id));
    }

    private void TryAdopt(UiLayoutElement element, Vector2 local)
    {
        var host = HostBox(Hit(local));
        if (host != null && host.Id != element.Id && !IsInside(host, element.Id))
        {
            element.ParentId = host.Id;
            if (host.Kind == UiElementKind.Grid)
                element.HorizontalExpand = true;
            element.Order = 0;
            foreach (var sibling in _document.Elements)
            {
                if (sibling.ParentId == host.Id && sibling.Id != element.Id)
                    element.Order = Math.Max(element.Order, sibling.Order + 1);
            }

            Remount();
            Changed?.Invoke();
            SelectionChanged?.Invoke();
            return;
        }

        if (element.ParentId == 0 || host != null)
            return;

        var cell = CellAt(local);
        var previousColumn = element.Column;
        var previousRow = element.Row;
        var previousParent = element.ParentId;
        element.ParentId = 0;
        element.Column = Math.Clamp(cell.X, 0, _document.Columns - element.ColumnSpan);
        element.Row = Math.Clamp(cell.Y, 0, _document.Rows - element.RowSpan);
        if (UiLayoutMath.Overlaps(_document, element, element.Column, element.Row, element.ColumnSpan, element.RowSpan))
        {
            element.ParentId = previousParent;
            element.Column = previousColumn;
            element.Row = previousRow;
            return;
        }

        Remount();
        Changed?.Invoke();
        SelectionChanged?.Invoke();
    }

    private bool IsInside(UiLayoutElement element, int ancestorId)
    {
        var parentId = element.ParentId;
        while (parentId != 0)
        {
            if (parentId == ancestorId)
                return true;

            var parent = _document.Elements.FirstOrDefault(item => item.Id == parentId);
            if (parent == null)
                return false;

            parentId = parent.ParentId;
        }

        return false;
    }

    private void Select(UiLayoutElement? element)
    {
        if (ReferenceEquals(Selected, element))
            return;

        Selected = element;
        SelectionChanged?.Invoke();
    }

    private void OnGridDown(GUIBoundKeyEventArgs args)
    {
        if (!_editing || args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        _pressed = true;
        _moved = false;
        _placeOnUp = false;
        _drag = Drag.None;
        _pressPosition = args.RelativePosition;

        if (Hit(args.RelativePosition) is { } edge && TryHandle(args.RelativePosition, edge, out var resize))
        {
            _armed = null;
            _hover = null;
            Select(edge);
            BeginDrag(edge, resize, args.RelativePosition);
            return;
        }

        var hit = Hit(args.RelativePosition);
        if (hit != null && hit.ParentId != 0)
        {
            _armed = null;
            _hover = null;
            Select(hit);
            BeginDrag(hit, Drag.Move, args.RelativePosition);
            return;
        }

        if (hit != null && !UiElementKinds.IsBackdrop(hit.Kind))
        {
            _armed = null;
            _hover = null;
            Select(hit);
            BeginDrag(hit, Drag.Move, args.RelativePosition);
            return;
        }

        if (_armed != null)
        {
            _placeOnUp = true;
            return;
        }

        if (hit != null)
        {
            Select(hit);
            BeginDrag(hit, Drag.Move, args.RelativePosition);
            return;
        }

        Select(null);
    }

    private void OnGridUp(GUIBoundKeyEventArgs args)
    {
        if (!_editing || args.Function != EngineKeyFunctions.UIClick || !_pressed)
            return;

        args.Handle();
        if (_placeOnUp && !_moved && _armed != null)
            Place(_armed.Value, args.RelativePosition);
        else if (_moved && Selected != null && _drag == Drag.Move)
            TryAdopt(Selected, args.RelativePosition);

        _pressed = false;
        _drag = Drag.None;
        _placeOnUp = false;
        if (_armed == null)
            _hover = null;
    }

    private void OnGridMove(GUIMouseMoveEventArgs args)
    {
        if (!_editing)
            return;

        if (!_pressed)
        {
            if (_armed != null)
            {
                var host = HostBox(Hit(args.RelativePosition));
                if (host != null)
                    ShowHover(new Vector2i(host.Column, host.Row), new Vector2i(host.ColumnSpan, host.RowSpan), true);
                else
                {
                    var hoverCell = CellAt(args.RelativePosition);
                    var span = DefaultSpan(_armed.Value);
                    span = new Vector2i(
                        Math.Min(span.X, _document.Columns - hoverCell.X),
                        Math.Min(span.Y, _document.Rows - hoverCell.Y));
                    ShowHover(hoverCell, span, !UiLayoutMath.Overlaps(_document, null, hoverCell.X, hoverCell.Y, span.X, span.Y));
                }
            }
            return;
        }

        if ((args.RelativePosition - _pressPosition).Length() > 4)
            _moved = true;

        if (!_moved || Selected == null || _drag == Drag.None)
            return;

        var cell = CellAt(args.RelativePosition);
        if (_drag == Drag.Move)
            MoveTo(cell);
        else
            ResizeTo(cell);

        var dropHost = HostBox(Hit(args.RelativePosition));
        if (dropHost != null && Selected != null && dropHost.Id != Selected.Id)
            ShowHover(new Vector2i(dropHost.Column, dropHost.Row), new Vector2i(dropHost.ColumnSpan, dropHost.RowSpan), true);
    }

    private void BeginDrag(UiLayoutElement element, Drag drag, Vector2 local)
    {
        var cell = CellAt(local);
        _drag = drag;
        _grabColumn = cell.X;
        _grabRow = cell.Y;
        _originColumn = element.Column;
        _originRow = element.Row;
        _originColumnSpan = element.ColumnSpan;
        _originRowSpan = element.RowSpan;
    }

    private void MoveTo(Vector2i cell)
    {
        var element = Selected;
        if (element == null || element.ParentId != 0)
            return;

        var column = Math.Clamp(_originColumn + cell.X - _grabColumn, 0, _document.Columns - element.ColumnSpan);
        var row = Math.Clamp(_originRow + cell.Y - _grabRow, 0, _document.Rows - element.RowSpan);
        var free = !UiLayoutMath.Overlaps(_document, element, column, row, element.ColumnSpan, element.RowSpan);
        ShowHover(new Vector2i(column, row), new Vector2i(element.ColumnSpan, element.RowSpan), free);
        if (!free || column == element.Column && row == element.Row)
            return;

        element.Column = column;
        element.Row = row;
        InvalidateArrange();
        Changed?.Invoke();
    }

    private void ResizeTo(Vector2i cell)
    {
        var element = Selected;
        if (element == null)
            return;

        var columnSpan = _originColumnSpan;
        var rowSpan = _originRowSpan;
        if (_drag is Drag.ResizeE or Drag.ResizeSE)
            columnSpan = Math.Max(1, cell.X - element.Column + 1);
        if (_drag is Drag.ResizeS or Drag.ResizeSE)
            rowSpan = Math.Max(1, cell.Y - element.Row + 1);

        columnSpan = Math.Min(columnSpan, _document.Columns - element.Column);
        rowSpan = Math.Min(rowSpan, _document.Rows - element.Row);
        var free = !UiLayoutMath.Overlaps(_document, element, element.Column, element.Row, columnSpan, rowSpan);
        ShowHover(new Vector2i(element.Column, element.Row), new Vector2i(columnSpan, rowSpan), free);
        if (!free || columnSpan == element.ColumnSpan && rowSpan == element.RowSpan)
            return;

        element.ColumnSpan = columnSpan;
        element.RowSpan = rowSpan;
        InvalidateArrange();
        Changed?.Invoke();
    }

    private void ShowHover(Vector2i cell, Vector2i span, bool valid)
    {
        if (Size.X < 1 || Size.Y < 1)
            return;

        var cellW = Size.X / _document.Columns;
        var cellH = Size.Y / _document.Rows;
        _hover = new UIBox2(cell.X * cellW, cell.Y * cellH, (cell.X + span.X) * cellW, (cell.Y + span.Y) * cellH);
        _hoverValid = valid;
    }

    private void DrawOverlay(DrawingHandleScreen handle)
    {
        if (!_editing || _document.Columns < 1 || _document.Rows < 1)
            return;

        var scale = UIScale;
        var width = PixelWidth;
        var height = PixelHeight;
        var grid = new Color(1f, 1f, 1f, 0.16f);
        for (var column = 0; column <= _document.Columns; column++)
        {
            var x = column * width / (float)_document.Columns;
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, height), grid);
        }

        for (var row = 0; row <= _document.Rows; row++)
        {
            var y = row * height / (float)_document.Rows;
            handle.DrawLine(new Vector2(0, y), new Vector2(width, y), grid);
        }

        foreach (var element in _document.Elements)
        {
            if (element.ParentId != 0)
                continue;

            var pixel = ToPixel(CellRect(element, Size), scale);
            var selected = ReferenceEquals(element, Selected);
            handle.DrawRect(pixel, selected ? new Color(1f, 0.82f, 0.25f, 1f) : new Color(1f, 1f, 1f, 0.28f), false);
            if (!selected)
                continue;

            var handleColor = new Color(1f, 0.82f, 0.25f, 1f);
            handle.DrawRect(new UIBox2(pixel.Right - 8, (pixel.Top + pixel.Bottom) / 2 - 5, pixel.Right, (pixel.Top + pixel.Bottom) / 2 + 5), handleColor);
            handle.DrawRect(new UIBox2((pixel.Left + pixel.Right) / 2 - 5, pixel.Bottom - 8, (pixel.Left + pixel.Right) / 2 + 5, pixel.Bottom), handleColor);
        }

        if (Selected is { ParentId: > 0 } && _controls.TryGetValue(Selected.Id, out var nested))
        {
            var nestedRect = ToPixel(UIBox2.FromDimensions(nested.GlobalPosition - GlobalPosition, nested.Size), scale);
            handle.DrawRect(nestedRect, new Color(1f, 0.82f, 0.25f, 1f), false);
        }

        if (_hover == null)
            return;

        var hover = ToPixel(_hover.Value, scale);
        handle.DrawRect(hover, _hoverValid ? new Color(0.35f, 0.7f, 1f, 0.28f) : new Color(1f, 0.3f, 0.25f, 0.35f));
    }

    private UiLayoutElement? Hit(Vector2 local)
    {
        UiLayoutElement? nested = null;
        for (var i = _document.Elements.Count - 1; i >= 0; i--)
        {
            var element = _document.Elements[i];
            if (element.ParentId == 0 || !_controls.TryGetValue(element.Id, out var control))
                continue;

            var rect = UIBox2.FromDimensions(control.GlobalPosition - GlobalPosition, control.Size);
            if (Contains(rect, local))
                nested = element;
        }

        if (nested != null)
            return nested;

        UiLayoutElement? panel = null;
        for (var i = _document.Elements.Count - 1; i >= 0; i--)
        {
            var element = _document.Elements[i];
            if (!Contains(CellRect(element, Size), local))
                continue;

            if (UiElementKinds.IsBackdrop(element.Kind))
                panel ??= element;
            else
                return element;
        }

        return panel;
    }

    private bool TryHandle(Vector2 local, UiLayoutElement element, out Drag drag)
    {
        var rect = CellRect(element, Size);
        var nearRight = Math.Abs(local.X - rect.Right) <= 8 && local.Y >= rect.Top - 4 && local.Y <= rect.Bottom + 4;
        var nearBottom = Math.Abs(local.Y - rect.Bottom) <= 8 && local.X >= rect.Left - 4 && local.X <= rect.Right + 4;
        if (nearRight && nearBottom)
            drag = Drag.ResizeSE;
        else if (nearRight)
            drag = Drag.ResizeE;
        else if (nearBottom)
            drag = Drag.ResizeS;
        else
        {
            drag = Drag.None;
            return false;
        }

        return true;
    }

    private Vector2i CellAt(Vector2 local)
    {
        var column = (int)(local.X / Size.X * _document.Columns);
        var row = (int)(local.Y / Size.Y * _document.Rows);
        return new Vector2i(
            Math.Clamp(column, 0, _document.Columns - 1),
            Math.Clamp(row, 0, _document.Rows - 1));
    }

    private UIBox2 CellRect(UiLayoutElement element, Vector2 size)
    {
        var cellW = size.X / _document.Columns;
        var cellH = size.Y / _document.Rows;
        return new UIBox2(
            element.Column * cellW,
            element.Row * cellH,
            (element.Column + element.ColumnSpan) * cellW,
            (element.Row + element.RowSpan) * cellH);
    }

    private static UIBox2 ToPixel(UIBox2 rect, float scale)
    {
        return new UIBox2(rect.Left * scale, rect.Top * scale, rect.Right * scale, rect.Bottom * scale);
    }

    private static bool Contains(UIBox2 rect, Vector2 point)
    {
        return point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
    }

    private static Vector2i DefaultSpan(UiElementKind kind)
    {
        var spec = UiElementKinds.Get(kind);
        return new Vector2i(spec.ColumnSpan, spec.RowSpan);
    }

    private enum Drag
    {
        None,
        Move,
        ResizeE,
        ResizeS,
        ResizeSE,
    }

    private sealed class GridOverlay : Control
    {
        private readonly UiConstructorSurface _owner;

        public GridOverlay(UiConstructorSurface owner)
        {
            _owner = owner;
            MouseFilter = MouseFilterMode.Stop;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            _owner.DrawOverlay(handle);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            _owner.OnGridDown(args);
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);
            _owner.OnGridUp(args);
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);
            _owner.OnGridMove(args);
        }
    }
}
