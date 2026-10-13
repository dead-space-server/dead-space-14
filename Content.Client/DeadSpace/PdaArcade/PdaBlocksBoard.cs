// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaBlocksBoard : PdaArcadeBoard
{
    private const int Columns = 10;
    private const int Rows = 20;
    private readonly bool[,] _well = new bool[Columns, Rows];
    private readonly (int X, int Y)[][] _shapes;
    private (int X, int Y)[] _piece = Array.Empty<(int, int)>();
    private int _px;
    private int _py;
    private float _drop;
    private int _locked;
    private int _next;
    private int _kind;
    private readonly Random _random = new();

    public PdaBlocksBoard()
    {
        _shapes = new (int X, int Y)[][]
        {
            new[] { (-1, 0), (0, 0), (1, 0), (2, 0) },
            new[] { (0, 0), (1, 0), (0, 1), (1, 1) },
            new[] { (-1, 0), (0, 0), (1, 0), (0, 1) },
            new[] { (0, 0), (1, 0), (-1, 1), (0, 1) },
            new[] { (-1, 0), (0, 0), (0, 1), (1, 1) },
            new[] { (-1, 0), (0, 0), (1, 0), (-1, 1) },
            new[] { (-1, 0), (0, 0), (1, 0), (1, 1) },
        };
    }

    public void Nudge(int dx)
    {
        if (Over)
            return;

        if (Fits(_piece, _px + dx, _py))
            _px += dx;
    }

    public void RotatePiece()
    {
        if (Over)
            return;

        var turned = new (int X, int Y)[_piece.Length];
        for (var i = 0; i < _piece.Length; i++)
            turned[i] = (-_piece[i].Y, _piece[i].X);

        if (Fits(turned, _px, _py))
            _piece = turned;
    }

    public void SoftDrop()
    {
        if (Over)
            return;

        if (!Fall())
            Lock();
    }

    protected override void OnRestart()
    {
        for (var x = 0; x < Columns; x++)
        for (var y = 0; y < Rows; y++)
            _well[x, y] = false;
        _drop = 0f;
        _locked = 0;
        _next = _random.Next(_shapes.Length);
        Spawn();
    }

    public override void Command(string command, bool down)
    {
        if (!down)
            return;

        if (command == "left")
            Nudge(-1);
        else if (command == "right")
            Nudge(1);
        else if (command == "down")
            SoftDrop();
        else if (command == "up" || command == "act")
            RotatePiece();
    }

    protected override void OnKey(GUIBoundKeyEventArgs args)
    {
        base.OnKey(args);
        if (Key(args, EngineKeyFunctions.TextCursorLeft) || Key(args, EngineKeyFunctions.MoveLeft))
            Nudge(-1);
        else if (Key(args, EngineKeyFunctions.TextCursorRight) || Key(args, EngineKeyFunctions.MoveRight))
            Nudge(1);
        else if (Key(args, EngineKeyFunctions.TextCursorDown) || Key(args, EngineKeyFunctions.MoveDown))
            SoftDrop();
        else if (Key(args, EngineKeyFunctions.TextCursorUp) || Key(args, EngineKeyFunctions.MoveUp))
            RotatePiece();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Over || IsPaused)
            return;

        _drop += args.DeltaSeconds;
        if (_drop < MathF.Max(0.12f, 0.45f - _locked * 0.015f))
            return;

        _drop = 0f;
        SoftDrop();
    }

    private void Spawn()
    {
        _kind = _next;
        _piece = ((int X, int Y)[]) _shapes[_kind].Clone();
        _next = _random.Next(_shapes.Length);
        _px = Columns / 2;
        _py = 1;
        if (!Fits(_piece, _px, _py))
            End();
    }

    private bool Fall()
    {
        if (!Fits(_piece, _px, _py + 1))
            return false;

        _py++;
        return true;
    }

    private void Lock()
    {
        foreach (var cell in _piece)
        {
            var x = _px + cell.X;
            var y = _py + cell.Y;
            if (x >= 0 && y >= 0 && x < Columns && y < Rows)
                _well[x, y] = true;
        }

        var cleared = 0;
        for (var y = Rows - 1; y >= 0; y--)
        {
            var full = true;
            for (var x = 0; x < Columns; x++)
            {
                if (!_well[x, y])
                    full = false;
            }

            if (!full)
                continue;

            cleared++;
            for (var up = y; up > 0; up--)
            for (var x = 0; x < Columns; x++)
                _well[x, up] = _well[x, up - 1];
            for (var x = 0; x < Columns; x++)
                _well[x, 0] = false;
            y++;
        }

        if (cleared > 0)
            Gain(100 * cleared * cleared);

        _locked++;
        Spawn();
    }

    private bool Fits((int X, int Y)[] piece, int px, int py)
    {
        foreach (var cell in piece)
        {
            var x = px + cell.X;
            var y = py + cell.Y;
            if (x < 0 || x >= Columns || y >= Rows)
                return false;
            if (y >= 0 && _well[x, y])
                return false;
        }

        return true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Visual.Well is { } backdrop)
            FillBackground(handle, backdrop);

        var inner = Inner(8);
        var preview = 52f;
        var well = new UIBox2(inner.Left, inner.Top, Math.Max(inner.Left + 20, inner.Right - preview), inner.Bottom);
        var cell = (int) Math.Min(well.Width / Columns, well.Height / Rows);
        if (cell < 2)
            return;

        var originX = (int) (well.Left + (well.Width - cell * Columns) / 2);
        var originY = (int) (well.Top + (well.Height - cell * Rows) / 2);
        var settled = Visual.Settled ?? Color.White;
        handle.DrawRect(new UIBox2(originX - 2, originY - 2, originX + cell * Columns + 2, originY + cell * Rows + 2), settled.WithAlpha(0.35f));
        for (var x = 0; x < Columns; x++)
        for (var y = 0; y < Rows; y++)
        {
            if (_well[x, y])
                Paint(handle, originX, originY, cell, x, y, settled);
        }

        var flying = PieceColor(_kind);
        foreach (var part in _piece)
            Paint(handle, originX, originY, cell, _px + part.X, _py + part.Y, flying);

        var box = new UIBox2(well.Right + 6, inner.Top, inner.Right, inner.Top + 56);
        handle.DrawRect(box, settled.WithAlpha(0.35f));
        var next = _shapes[_next];
        var nextColor = PieceColor(_next);
        foreach (var part in next)
        {
            var px = box.Left + 18 + part.X * 8;
            var py = box.Top + 22 + part.Y * 8;
            handle.DrawRect(new UIBox2(px, py, px + 7, py + 7), nextColor);
        }
    }

    private Color PieceColor(int index)
    {
        var colors = Visual.Colors;
        if (colors.Count == 0)
            return Visual.Settled ?? Color.White;

        return colors[index % colors.Count];
    }

    private static void Paint(DrawingHandleScreen handle, int originX, int originY, int cell, int x, int y, Color color)
    {
        if (y < 0)
            return;

        handle.DrawRect(new UIBox2(originX + x * cell, originY + y * cell, originX + (x + 1) * cell - 1, originY + (y + 1) * cell - 1), color);
    }
}
