// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Vending;

/// <summary>
/// Lays its children out as equal cards in rows. The number of columns follows the width the grid is given, so the
/// grid never forces its window wider than one card: a narrow window shows one column, a wide one up to
/// <see cref="MaxColumns"/>. Each row is as tall as its tallest card; hidden children are skipped.
/// </summary>
public sealed class HologramCardGrid : Container
{
    private float _minCardWidth = 128f;
    private int _maxColumns = 6;
    private float _separation = 6f;

    /// <summary>The narrowest a card may get before the grid drops a column.</summary>
    public float MinCardWidth
    {
        get => _minCardWidth;
        set
        {
            _minCardWidth = MathF.Max(1f, value);
            InvalidateMeasure();
        }
    }

    public int MaxColumns
    {
        get => _maxColumns;
        set
        {
            _maxColumns = Math.Max(1, value);
            InvalidateMeasure();
        }
    }

    public float Separation
    {
        get => _separation;
        set
        {
            _separation = MathF.Max(0f, value);
            InvalidateMeasure();
        }
    }

    public int GetColumns(float width)
    {
        if (!float.IsFinite(width))
            return Math.Min(_maxColumns, 4);

        var fit = (int) MathF.Floor((width + _separation) / (_minCardWidth + _separation));
        return Math.Clamp(fit, 1, _maxColumns);
    }

    private float CellWidth(float width, int columns)
    {
        if (!float.IsFinite(width))
            return _minCardWidth;

        return MathF.Max(_minCardWidth, (width - _separation * (columns - 1)) / columns);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var columns = GetColumns(availableSize.X);
        var cell = new Vector2(CellWidth(availableSize.X, columns), float.PositiveInfinity);

        var height = 0f;
        var rowHeight = 0f;
        var column = 0;
        var rows = 0;

        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            child.Measure(cell);
            rowHeight = MathF.Max(rowHeight, child.DesiredSize.Y);

            if (++column < columns)
                continue;

            height += rowHeight;
            rows++;
            rowHeight = 0f;
            column = 0;
        }

        if (column > 0)
        {
            height += rowHeight;
            rows++;
        }

        if (rows > 1)
            height += _separation * (rows - 1);

        // Only one card's width is required: the rest is laid out from whatever width the parent hands out.
        return new Vector2(_minCardWidth, height);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var columns = GetColumns(finalSize.X);
        var cellWidth = (finalSize.X - _separation * (columns - 1)) / columns;
        cellWidth = MathF.Max(1f, cellWidth);

        var row = new List<Control>(columns);
        var y = 0f;

        foreach (var child in Children)
        {
            if (!child.Visible)
                continue;

            row.Add(child);
            if (row.Count < columns)
                continue;

            y = ArrangeRow(row, y, cellWidth);
            row.Clear();
        }

        if (row.Count > 0)
            ArrangeRow(row, y, cellWidth);

        return finalSize;
    }

    private float ArrangeRow(List<Control> row, float y, float cellWidth)
    {
        var height = 0f;
        foreach (var child in row)
        {
            height = MathF.Max(height, child.DesiredSize.Y);
        }

        var x = 0f;
        foreach (var child in row)
        {
            child.Arrange(UIBox2.FromDimensions(new Vector2(x, y), new Vector2(cellWidth, height)));
            x += cellWidth + _separation;
        }

        return y + height + _separation;
    }
}
