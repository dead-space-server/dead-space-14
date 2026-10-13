// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaSnakeBoard : PdaArcadeBoard
{
    private const int Cells = 26;
    private readonly List<(int X, int Y)> _snake = new();
    private (int X, int Y) _food;
    private int _dirX = 1;
    private int _dirY;
    private int _nextX = 1;
    private int _nextY;
    private float _wait;
    private readonly Random _random = new();

    private int _fruit;
    private float _anim;

    public override void Command(string command, bool down)
    {
        if (!down)
            return;

        if (command == "left")
            Turn(-1, 0);
        else if (command == "right")
            Turn(1, 0);
        else if (command == "up")
            Turn(0, -1);
        else if (command == "down")
            Turn(0, 1);
    }

    public void Turn(int x, int y)
    {
        if (x == -_dirX && y == -_dirY)
            return;

        _nextX = x;
        _nextY = y;
    }

    protected override void OnRestart()
    {
        _snake.Clear();
        _snake.Add((12, 13));
        _snake.Add((11, 13));
        _snake.Add((10, 13));
        _dirX = 1;
        _dirY = 0;
        _nextX = 1;
        _nextY = 0;
        _wait = 0f;
        _fruit = _random.Next(PdaArcadeArt.Profile.Fruits.Count);
        PlaceFood();
    }

    protected override void OnKey(GUIBoundKeyEventArgs args)
    {
        base.OnKey(args);
        if (Key(args, EngineKeyFunctions.TextCursorUp) || Key(args, EngineKeyFunctions.MoveUp))
            Turn(0, -1);
        else if (Key(args, EngineKeyFunctions.TextCursorDown) || Key(args, EngineKeyFunctions.MoveDown))
            Turn(0, 1);
        else if (Key(args, EngineKeyFunctions.TextCursorLeft) || Key(args, EngineKeyFunctions.MoveLeft))
            Turn(-1, 0);
        else if (Key(args, EngineKeyFunctions.TextCursorRight) || Key(args, EngineKeyFunctions.MoveRight))
            Turn(1, 0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Over || IsPaused)
            return;

        _wait += args.DeltaSeconds;
        _anim += args.DeltaSeconds;
        var eaten = Math.Max(0, _snake.Count - 3);
        if (_wait < MathF.Max(0.08f, 0.16f - eaten * 0.006f))
            return;

        _wait = 0f;
        Step();
    }

    private void Step()
    {
        _dirX = _nextX;
        _dirY = _nextY;
        var head = _snake[0];
        var next = (X: head.X + _dirX, Y: head.Y + _dirY);
        if (next.X < 0 || next.Y < 0 || next.X >= Cells || next.Y >= Cells || _snake.Contains(next))
        {
            End();
            return;
        }

        _snake.Insert(0, next);
        if (next == _food)
        {
            Gain(10);
            _fruit = _random.Next(PdaArcadeArt.Profile.Fruits.Count);
            PlaceFood();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }
    }

    private void PlaceFood()
    {
        for (var i = 0; i < 40; i++)
        {
            var cell = (_random.Next(Cells), _random.Next(Cells));
            if (!_snake.Contains(cell))
            {
                _food = cell;
                return;
            }
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawFloor(handle);
        var inner = Inner(8);
        var cell = (int) Math.Min(inner.Width, inner.Height) / Cells;
        if (cell < 2)
            return;

        var boardW = cell * Cells;
        var boardH = cell * Cells;
        var originX = (int) (inner.Left + (inner.Width - boardW) / 2);
        var originY = (int) (inner.Top + (inner.Height - boardH) / 2);
        handle.DrawRect(new UIBox2(originX - 3, originY - 3, originX + boardW + 3, originY + boardH + 3), Color.FromHex("#0d2416"));
        for (var x = 0; x < Cells; x++)
        for (var y = 0; y < Cells; y++)
        {
            if ((x + y) % 2 == 0)
                handle.DrawRect(Cell(originX, originY, cell, x, y), Color.FromHex("#1e4a2c"));
        }

        var fruit = PdaArcadeArt.Texture(PdaArcadeArt.Profile.Fruits[_fruit]);
        var fruitRect = Cell(originX, originY, cell, _food.X, _food.Y);
        var grow = cell * 0.45f;
        handle.DrawTextureRect(fruit, new UIBox2(fruitRect.Left - grow, fruitRect.Top - grow, fruitRect.Right + grow, fruitRect.Bottom + grow));

        for (var i = _snake.Count - 1; i >= 0; i--)
        {
            var raw = Cell(originX, originY, cell, _snake[i].X, _snake[i].Y);
            var inset = i == 0 ? cell * 0.12f : cell * 0.28f;
            var rect = new UIBox2(raw.Left + inset, raw.Top + inset, raw.Right - inset, raw.Bottom - inset);
            handle.DrawRect(rect, i == 0 ? Color.FromHex("#7dffb3") : Color.FromHex("#2f9e62"));
            if (i != 0)
                continue;

            var eye = Math.Max(2, cell / 5);
            var (ex, ey) = (_dirX, _dirY);
            var cx = rect.Left + rect.Width / 2;
            var cy = rect.Top + rect.Height / 2;
            handle.DrawRect(new UIBox2(cx + ex * eye - eye, cy + ey * eye - eye, cx + ex * eye + eye, cy + ey * eye), Color.White);
            handle.DrawRect(new UIBox2(cx - ey * eye - eye, cy + ex * eye - eye, cx - ey * eye, cy + ex * eye), Color.White);
            var tongue = (int) (MathF.Sin(_anim * 8f) * cell / 3f);
            if (tongue > 1)
            {
                handle.DrawRect(new UIBox2(
                    cx + ex * cell / 2f,
                    cy + ey * cell / 2f,
                    cx + ex * (cell / 2f + tongue),
                    cy + ey * (cell / 2f + tongue) + Math.Max(1, eye / 2)),
                    Color.FromHex("#e24a4a"));
            }
        }
    }

    private static UIBox2 Cell(int originX, int originY, int cell, int x, int y)
    {
        return new UIBox2(originX + x * cell, originY + y * cell, originX + (x + 1) * cell - 1, originY + (y + 1) * cell - 1);
    }
}
