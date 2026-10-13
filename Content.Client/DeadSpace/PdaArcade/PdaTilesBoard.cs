// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaTilesBoard : PdaArcadeBoard
{
    private readonly int[,] _grid = new int[4, 4];
    private readonly Random _random = new();

    public void Slide(int dx, int dy)
    {
        if (Over || (dx == 0 && dy == 0))
            return;

        var moved = false;
        for (var line = 0; line < 4; line++)
        {
            var values = new int[4];
            for (var i = 0; i < 4; i++)
            {
                var x = dx != 0 ? (dx > 0 ? 3 - i : i) : line;
                var y = dy != 0 ? (dy > 0 ? 3 - i : i) : line;
                values[i] = _grid[x, y];
            }

            var packed = Pack(values, out var gained);
            if (gained > 0)
                Gain(gained);
            for (var i = 0; i < 4; i++)
            {
                var x = dx != 0 ? (dx > 0 ? 3 - i : i) : line;
                var y = dy != 0 ? (dy > 0 ? 3 - i : i) : line;
                if (_grid[x, y] != packed[i])
                    moved = true;
                _grid[x, y] = packed[i];
            }
        }

        if (!moved)
        {
            if (!HasMove())
                End();
            return;
        }

        Spawn();
        if (!HasMove())
            End();
    }

    public override void Command(string command, bool down)
    {
        if (!down)
            return;

        if (command == "left")
            Slide(-1, 0);
        else if (command == "right")
            Slide(1, 0);
        else if (command == "up")
            Slide(0, -1);
        else if (command == "down")
            Slide(0, 1);
    }

    protected override void OnRestart()
    {
        for (var x = 0; x < 4; x++)
        for (var y = 0; y < 4; y++)
            _grid[x, y] = 0;
        Spawn();
        Spawn();
    }

    protected override void OnKey(GUIBoundKeyEventArgs args)
    {
        base.OnKey(args);
        if (Key(args, EngineKeyFunctions.TextCursorLeft) || Key(args, EngineKeyFunctions.MoveLeft))
            Slide(-1, 0);
        else if (Key(args, EngineKeyFunctions.TextCursorRight) || Key(args, EngineKeyFunctions.MoveRight))
            Slide(1, 0);
        else if (Key(args, EngineKeyFunctions.TextCursorUp) || Key(args, EngineKeyFunctions.MoveUp))
            Slide(0, -1);
        else if (Key(args, EngineKeyFunctions.TextCursorDown) || Key(args, EngineKeyFunctions.MoveDown))
            Slide(0, 1);
    }

    private void Spawn()
    {
        var free = new List<(int X, int Y)>();
        for (var x = 0; x < 4; x++)
        for (var y = 0; y < 4; y++)
        {
            if (_grid[x, y] == 0)
                free.Add((x, y));
        }

        if (free.Count == 0)
            return;

        var cell = free[_random.Next(free.Count)];
        _grid[cell.X, cell.Y] = _random.Next(10) == 0 ? 4 : 2;
    }

    private bool HasMove()
    {
        for (var x = 0; x < 4; x++)
        for (var y = 0; y < 4; y++)
        {
            if (_grid[x, y] == 0)
                return true;
            if (x < 3 && _grid[x, y] == _grid[x + 1, y])
                return true;
            if (y < 3 && _grid[x, y] == _grid[x, y + 1])
                return true;
        }

        return false;
    }

    private static int[] Pack(int[] source, out int gained)
    {
        gained = 0;
        var packed = new int[4];
        var count = 0;
        for (var i = 0; i < 4; i++)
        {
            if (source[i] != 0)
                packed[count++] = source[i];
        }

        var result = new int[4];
        var write = 0;
        for (var i = 0; i < count; i++)
        {
            if (i + 1 < count && packed[i] == packed[i + 1])
            {
                var value = packed[i] * 2;
                result[write++] = value;
                gained += value;
                i++;
            }
            else
            {
                result[write++] = packed[i];
            }
        }

        return result;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawFloor(handle);
        var inner = Inner(10);
        var cell = (int) Math.Min(inner.Width, inner.Height) / 4;
        if (cell < 8)
            return;

        var originX = (int) (inner.Left + (inner.Width - cell * 4) / 2);
        var originY = (int) (inner.Top + (inner.Height - cell * 4) / 2);
        var font = PdaArcadeArt.Font(Math.Max(10, cell / 3));
        for (var x = 0; x < 4; x++)
        for (var y = 0; y < 4; y++)
        {
            var rect = new UIBox2(originX + x * cell + 3, originY + y * cell + 3, originX + (x + 1) * cell - 3, originY + (y + 1) * cell - 3);
            var value = _grid[x, y];
            handle.DrawRect(rect, value == 0 ? Color.FromHex("#cdc1b4") : ColorFor(value));
            if (value == 0)
                continue;

            var text = value.ToString();
            var size = handle.GetDimensions(font, text, 1f);
            var pos = new System.Numerics.Vector2(rect.Left + (rect.Width - size.X) / 2f, rect.Top + (rect.Height - size.Y) / 2f);
            handle.DrawString(font, pos, text, value < 8 ? Color.FromHex("#776e65") : Color.White);
        }
    }

    private static Color ColorFor(int value)
    {
        return Color.FromHex(value switch
        {
            2 => "#d8e2dc",
            4 => "#ffe5d9",
            8 => "#ffcad4",
            16 => "#f4acb7",
            32 => "#9d8189",
            64 => "#6d6875",
            128 => "#b5838d",
            256 => "#e5989b",
            512 => "#ffb4a2",
            1024 => "#e07a5f",
            _ => "#3d405b",
        });
    }
}
