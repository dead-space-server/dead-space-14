// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using System.Numerics;
using Content.Shared.DeadSpace.PdaArcade;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaTableBoard : Control
{
    public event Action<string>? Moved;
    public event Action? FocusReady;

    public string Game = "";
    public string Board = "";
    public bool Yours;
    public bool MineBottom;
    private bool _rolling;
    private float _arm;

    public bool Rolling
    {
        get => _rolling;
        set
        {
            if (value && !_rolling)
                _arm = 0.7f;
            _rolling = value;
        }
    }
    public int YourDie;
    public int TheirDie;
    private int _from = -1;
    private int _ship;
    private bool _across = true;

    public PdaTableBoard()
    {
        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = false;
        HorizontalExpand = true;
        VerticalExpand = true;
        MinSize = new Vector2(220, 220);
        OnKeyBindDown += args =>
        {
            FocusReady?.Invoke();
            OnPress(args);
        };
    }

    public void Steer(string command, bool down)
    {
        if (Game != PdaArcadeGame.Pong || !Yours)
            return;

        if (command == "up")
            Moved?.Invoke(down ? "up" : "stop");
        else if (command == "down")
            Moved?.Invoke(down ? "down" : "stop");
    }

    private void OnPress(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick || !Yours)
            return;

        if (Rolling)
        {
            args.Handle();
            if (_arm > 0f || YourDie != 0)
                return;

            var die = new UIBox2(24, 36, 80, 110);
            if (die.Contains(args.RelativePixelPosition))
                Moved?.Invoke("roll");
            return;
        }

        args.Handle();
        var point = args.RelativePixelPosition;
        if (Game == PdaArcadeGame.Cross)
            Cell(point, 3, 3, out var index);
        else if (Game == PdaArcadeGame.Ships)
            ShipsClick(point);
        else if (Game == PdaArcadeGame.Chess || Game == PdaArcadeGame.Checks)
            Pick(point);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
        handle.DrawRect(box, Color.FromHex("#12161e"));
        if (Rolling)
        {
            DrawRoll(handle);
            return;
        }
        if (Game == PdaArcadeGame.Cross)
            DrawCross(handle);
        else if (Game == PdaArcadeGame.Ships)
            DrawShips(handle);
        else if (Game == PdaArcadeGame.Chess)
            DrawChess(handle);
        else if (Game == PdaArcadeGame.Checks)
            DrawChecks(handle);
        else if (Game == PdaArcadeGame.Pong)
            DrawPong(handle);
    }

    private void DrawCross(DrawingHandleScreen handle)
    {
        var origin = GridOrigin(3, 3, out var cell);
        for (var i = 0; i < 9 && i < Board.Length; i++)
        {
            var rect = CellRect(origin, cell, i % 3, i / 3);
            handle.DrawRect(rect, Color.FromHex("#243044"));
            var mark = Board[i];
            if (mark == 'x')
                Cross(handle, rect, Color.FromHex("#e25b5b"));
            else if (mark == 'o')
                Ring(handle, rect, Color.FromHex("#7ee7ff"));
        }
    }

    private void DrawShips(DrawingHandleScreen handle)
    {
        var parts = Board.Split('|');
        if (parts.Length < 4 || parts[3].Length < 72)
            return;

        ShipLayout(out var cell, out var ownOrigin, out var foeOrigin);
        handle.DrawString(PdaArcadeArt.Font(12), new Vector2(ownOrigin.X, 2), Loc.GetString("pda-arcade-your-field"), Color.White);
        handle.DrawString(PdaArcadeArt.Font(12), new Vector2(foeOrigin.X, foeOrigin.Y - 16), Loc.GetString("pda-arcade-their-field"), Color.White);
        PaintGrid(handle, parts[3][..36], ownOrigin, cell, true);
        PaintGrid(handle, parts[3][36..], foeOrigin, cell, false);
        var fleet = parts[1];
        for (var i = 0; i < fleet.Length; i++)
        {
            var size = fleet[i] - '0';
            var y = 28 + i * 22;
            var selected = i == _ship;
            if (selected)
                handle.DrawRect(new UIBox2(2, y - 2, 64, y + 16), Color.FromHex("#f2d36b"));

            var length = _across || !selected ? size : 1;
            var tall = _across || !selected ? 1 : size;
            for (var n = 0; n < size; n++)
            {
                var x = 6 + (_across || !selected ? n : 0) * 10;
                var yy = y + (!_across && selected ? n : 0) * 10;
                handle.DrawRect(new UIBox2(x, yy, x + 8, yy + 8), Color.FromHex("#d7dde8"));
                _ = length;
                _ = tall;
            }
        }
    }

    private void PaintGrid(DrawingHandleScreen handle, string cells, Vector2 origin, float cell, bool own)
    {
        for (var i = 0; i < 36 && i < cells.Length; i++)
        {
            var rect = CellRect(origin, cell, i % 6, i / 6);
            var mark = cells[i];
            var color = mark switch
            {
                '1' => Color.FromHex("#c5cedb"),
                '2' => Color.FromHex("#10283d"),
                '3' => Color.FromHex("#e25b5b"),
                _ => Color.FromHex("#1d4e7a"),
            };
            if (!own && mark == '1')
                color = Color.FromHex("#1d4e7a");

            handle.DrawRect(rect, color);
            if (mark == '3')
                Cross(handle, rect, Color.White);
        }
    }

    private void DrawChess(DrawingHandleScreen handle)
    {
        var cells = Board.Length >= 64 ? Board : "";
        var origin = GridOrigin(8, 8, out var cell);
        for (var i = 0; i < 64; i++)
        {
            var row = i / 8;
            var col = i % 8;
            var rect = CellRect(origin, cell, col, Shown(row));
            var dark = (row + col) % 2 == 1;
            handle.DrawRect(rect, dark ? Color.FromHex("#8d5a32") : Color.FromHex("#f0d2a0"));
            if (i == _from)
                handle.DrawRect(rect, Color.FromHex("#f2d36baa"));

            if (i < cells.Length && cells[i] != '.')
                Piece(handle, rect, cells[i]);
        }
    }

    private void DrawChecks(DrawingHandleScreen handle)
    {
        var cells = Board.Length >= 64 ? Board : "";
        var origin = GridOrigin(8, 8, out var cell);
        for (var i = 0; i < 64; i++)
        {
            var row = i / 8;
            var col = i % 8;
            var rect = CellRect(origin, cell, col, Shown(row));
            var dark = (row + col) % 2 == 1;
            handle.DrawRect(rect, dark ? Color.FromHex("#6b3a22") : Color.FromHex("#e6c39a"));
            if (i == _from)
                handle.DrawRect(rect, Color.FromHex("#f2d36baa"));
            if (i >= cells.Length || cells[i] == '.')
                continue;

            var white = char.ToLowerInvariant(cells[i]) == 'w';
            var color = white ? Color.White : Color.FromHex("#d23b3b");
            var center = (rect.TopLeft + rect.BottomRight) / 2f;
            handle.DrawCircle(center, Math.Min(rect.Width, rect.Height) * 0.34f, color, true);
            if (char.IsUpper(cells[i]))
                handle.DrawCircle(center, Math.Min(rect.Width, rect.Height) * 0.14f, Color.FromHex("#f2d36b"), true);
        }
    }

    private void DrawPong(DrawingHandleScreen handle)
    {
        var parts = Board.Split(' ');
        if (parts.Length < 8)
            return;

        var width = PixelSize.X;
        var height = PixelSize.Y;
        var padA = Parse(parts[0]);
        var padB = Parse(parts[1]);
        var x = Parse(parts[2]);
        var y = Parse(parts[3]);
        var padH = height * 0.22f;
        handle.DrawRect(new UIBox2(16, padA * height - padH / 2f, 28, padA * height + padH / 2f), Color.FromHex("#f2d36b"));
        handle.DrawRect(new UIBox2(width - 28, padB * height - padH / 2f, width - 16, padB * height + padH / 2f), Color.FromHex("#7ee7ff"));
        handle.DrawRect(new UIBox2(x * width - 6, y * height - 6, x * width + 6, y * height + 6), Color.White);
        handle.DrawString(PdaArcadeArt.Font(16), new Vector2(12, 8), parts[6] + " : " + parts[7], Color.White);
    }


    private void Piece(DrawingHandleScreen handle, UIBox2 rect, char piece)
    {
        var white = char.IsUpper(piece);
        var kind = char.ToLowerInvariant(piece);
        var key = kind switch
        {
            'k' => "king",
            'q' => "queen",
            'r' => "rook",
            'b' => "bishop",
            'n' => "knight",
            _ => "pawn",
        };
        if (!PdaArcadeArt.TryGame(PdaArcadeGame.Chess, out var game) || !game.Sprites.TryGetValue(key, out var specifier))
            return;

        handle.DrawTextureRect(
            PdaArcadeArt.Texture(specifier),
            rect,
            white ? Color.White : Color.FromHex("#6d8cff"));
    }

    private int Shown(int row)
    {
        return MineBottom ? 7 - row : row;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_arm > 0f)
            _arm -= args.DeltaSeconds;
    }

    private void ShipsClick(Vector2 point)
    {
        var parts = Board.Split('|');
        if (parts.Length < 4)
            return;

        ShipLayout(out var cell, out var ownOrigin, out var foeOrigin);
        if (point.X < 66f && parts[0] == "Y")
        {
            var index = (int)((point.Y - 28f) / 22f);
            if (index < 0 || index >= parts[1].Length)
                return;

            if (index == _ship)
                _across = !_across;
            else
                _ship = index;

            return;
        }

        var placing = parts[0] == "Y";
        var origin = placing ? ownOrigin : foeOrigin;
        if (!Hit(point, origin, cell, 6, 6, out var col, out var row))
            return;

        if (placing)
        {
            if (_ship < 0 || _ship >= parts[1].Length)
                return;

            var size = (parts[1][_ship] - '0').ToString(CultureInfo.InvariantCulture);
            Moved?.Invoke(size + "," + row.ToString(CultureInfo.InvariantCulture) + "," + col.ToString(CultureInfo.InvariantCulture) + "," + (_across ? "0" : "1"));
            return;
        }

        if (parts[0] == "S")
            Moved?.Invoke(row.ToString(CultureInfo.InvariantCulture) + "," + col.ToString(CultureInfo.InvariantCulture));
    }

    private void ShipLayout(out float cell, out Vector2 ownOrigin, out Vector2 foeOrigin)
    {
        cell = Math.Min((PixelSize.X - 74f) / 6f, (PixelSize.Y - 40f) / 12f);
        cell = Math.Max(8f, cell);
        ownOrigin = new Vector2(70, 18);
        foeOrigin = new Vector2(70, 40 + cell * 6f);
    }

    private void Pick(Vector2 point)
    {
        var origin = GridOrigin(8, 8, out var cell);
        if (!Hit(point, origin, cell, 8, 8, out var col, out var row))
            return;

        var boardRow = Shown(row);
        var index = boardRow * 8 + col;
        if (_from < 0)
        {
            _from = index;
            return;
        }

        Moved?.Invoke(_from.ToString(CultureInfo.InvariantCulture) + "-" + index.ToString(CultureInfo.InvariantCulture));
        _from = -1;
    }

    private void Cell(Vector2 point, int columns, int rows, out int index)
    {
        index = -1;
        var origin = GridOrigin(columns, rows, out var cell);
        if (!Hit(point, origin, cell, columns, rows, out var col, out var row))
            return;

        Moved?.Invoke((row * columns + col).ToString(CultureInfo.InvariantCulture));
    }

    private Vector2 GridOrigin(int columns, int rows, out float cell)
    {
        cell = MathF.Min(PixelSize.X / columns, PixelSize.Y / rows);
        var width = cell * columns;
        var height = cell * rows;
        return new Vector2((PixelSize.X - width) / 2f, (PixelSize.Y - height) / 2f);
    }

    private static UIBox2 CellRect(Vector2 origin, float cell, int col, int row)
    {
        return new UIBox2(origin.X + col * cell + 1, origin.Y + row * cell + 1, origin.X + (col + 1) * cell - 1, origin.Y + (row + 1) * cell - 1);
    }

    private static bool Hit(Vector2 point, Vector2 origin, float cell, int columns, int rows, out int col, out int row)
    {
        col = (int)((point.X - origin.X) / cell);
        row = (int)((point.Y - origin.Y) / cell);
        return col >= 0 && row >= 0 && col < columns && row < rows;
    }

    private static UIBox2 Inset(UIBox2 rect, float margin)
    {
        return new UIBox2(rect.Left + margin, rect.Top + margin, rect.Right - margin, rect.Bottom - margin);
    }

    private static void Cross(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        handle.DrawLine(rect.TopLeft, rect.BottomRight, color);
        handle.DrawLine(rect.TopRight, rect.BottomLeft, color);
    }

    private static void Ring(DrawingHandleScreen handle, UIBox2 rect, Color color)
    {
        var center = (rect.TopLeft + rect.BottomRight) / 2f;
        var radius = Math.Min(rect.Width, rect.Height) * 0.32f;
        const int steps = 16;
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= steps; i++)
        {
            var angle = i / (float)steps * MathF.Tau;
            var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            handle.DrawLine(previous, next, color);
            previous = next;
        }
    }

    private void DrawRoll(DrawingHandleScreen handle)
    {
        handle.DrawString(PdaArcadeArt.Font(14), new Vector2(12, 8), Loc.GetString("pda-arcade-roll-sides"), Color.White);
        Die(handle, new Vector2(24, 40), YourDie, Loc.GetString("pda-arcade-your-die"));
        Die(handle, new Vector2(120, 40), TheirDie, Loc.GetString("pda-arcade-their-die"));
    }

    private static void Die(DrawingHandleScreen handle, Vector2 at, int value, string caption)
    {
        handle.DrawString(PdaArcadeArt.Font(12), at, caption, Color.White);
        var rect = new UIBox2(at.X, at.Y + 18, at.X + 48, at.Y + 66);
        handle.DrawRect(rect, Color.FromHex("#f4f4f4"));
        if (value <= 0)
        {
            handle.DrawString(PdaArcadeArt.Font(14), new Vector2(rect.Left + 16, rect.Top + 16), "?", Color.FromHex("#333333"));
            return;
        }

        var spots = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.75f), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.75f) };
        var show = value switch
        {
            1 => new[] { 0 },
            2 => new[] { 1, 2 },
            3 => new[] { 0, 1, 2 },
            4 => new[] { 1, 2, 3, 4 },
            5 => new[] { 0, 1, 2, 3, 4 },
            _ => new[] { 1, 2, 3, 4, 5, 6 },
        };
        foreach (var index in show)
        {
            var spot = spots[index];
            var x = rect.Left + rect.Width * spot.X;
            var y = rect.Top + rect.Height * spot.Y;
            handle.DrawCircle(new Vector2(x, y), 3f, Color.FromHex("#1a1a1a"), true);
        }
    }

    private static float Parse(string text)
    {
        return float.Parse(text, CultureInfo.InvariantCulture);
    }
}
