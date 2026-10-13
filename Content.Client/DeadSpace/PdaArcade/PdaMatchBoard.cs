// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaMatchBoard : PdaArcadeBoard
{
    private const int Cells = 8;
    private int Kinds => PdaArcadeArt.Profile.Fruits.Count;
    private readonly int[,] _grid = new int[Cells, Cells];
    private int _cx;
    private int _cy;
    private int _sx = -1;
    private int _sy = -1;
    private readonly Random _random = new();

    public override void Command(string command, bool down)
    {
        if (!down || Over || _clearing || _pulse > 0f)
            return;

        if (command == "left")
            Move(-1, 0);
        else if (command == "right")
            Move(1, 0);
        else if (command == "up")
            Move(0, -1);
        else if (command == "down")
            Move(0, 1);
        else if (command == "act")
            Choose();
    }

    protected override void OnRestart()
    {
        _cx = 0;
        _cy = 0;
        _sx = -1;
        _sy = -1;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            for (var x = 0; x < Cells; x++)
            for (var y = 0; y < Cells; y++)
                _grid[x, y] = _random.Next(Kinds);

            if (Find().Count == 0 && HasMove())
                return;
        }

        while (ClearMatches())
        {
        }
    }

    private void Move(int dx, int dy)
    {
        _cx = Math.Clamp(_cx + dx, 0, Cells - 1);
        _cy = Math.Clamp(_cy + dy, 0, Cells - 1);
    }

    private void Choose()
    {
        if (_sx < 0)
        {
            _sx = _cx;
            _sy = _cy;
            return;
        }

        if (_sx == _cx && _sy == _cy)
        {
            _sx = -1;
            return;
        }

        if (Math.Abs(_sx - _cx) + Math.Abs(_sy - _cy) != 1)
        {
            _sx = _cx;
            _sy = _cy;
            return;
        }

        Swap(_sx, _sy, _cx, _cy);
        if (Find().Count == 0)
        {
            Swap(_sx, _sy, _cx, _cy);
            _sx = -1;
            return;
        }

        _sx = -1;
        BeginClear();
    }

    private bool _clearing;
    private float _pulse;
    private readonly bool[,] _pop = new bool[Cells, Cells];
    private readonly float[,] _offset = new float[Cells, Cells];

    private void BeginClear()
    {
        var matched = Find();
        if (matched.Count == 0)
        {
            if (!HasMove())
                End();
            return;
        }

        foreach (var cell in matched)
            _pop[cell.X, cell.Y] = true;

        _clearing = true;
        _pulse = 0.16f;
        Gain(matched.Count * 10);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Over)
            return;

        if (_pulse > 0f)
        {
            _pulse -= args.DeltaSeconds;
            if (_pulse <= 0f)
                Collapse();
            return;
        }

        var moving = false;
        for (var x = 0; x < Cells; x++)
        for (var y = 0; y < Cells; y++)
        {
            if (_offset[x, y] >= 0f)
                continue;

            moving = true;
            _offset[x, y] = MathF.Min(0f, _offset[x, y] + args.DeltaSeconds * 12f);
        }

        if (!moving && !_clearing)
        {
            if (Find().Count > 0)
                BeginClear();
            else if (!HasMove())
                End();
        }
    }

    private void Collapse()
    {
        for (var x = 0; x < Cells; x++)
        {
            var stack = new List<int>();
            var from = new List<int>();
            for (var y = Cells - 1; y >= 0; y--)
            {
                if (_pop[x, y] || _grid[x, y] < 0)
                    continue;

                stack.Add(_grid[x, y]);
                from.Add(y);
            }

            for (var y = 0; y < Cells; y++)
            {
                _pop[x, y] = false;
                _grid[x, y] = -1;
                _offset[x, y] = 0f;
            }

            for (var i = 0; i < stack.Count; i++)
            {
                var y = Cells - 1 - i;
                _grid[x, y] = stack[i];
                _offset[x, y] = from[i] - y;
            }

            var empty = Cells - stack.Count;
            for (var y = empty - 1; y >= 0; y--)
            {
                _grid[x, y] = _random.Next(Kinds);
                _offset[x, y] = -empty;
            }
        }

        _clearing = false;
    }

    private bool ClearMatches()
    {
        var matched = Find();
        if (matched.Count == 0)
            return false;

        Gain(matched.Count * 10);
        foreach (var cell in matched)
            _grid[cell.X, cell.Y] = -1;

        for (var x = 0; x < Cells; x++)
        {
            var write = Cells - 1;
            for (var y = Cells - 1; y >= 0; y--)
            {
                if (_grid[x, y] < 0)
                    continue;

                _grid[x, write] = _grid[x, y];
                if (write != y)
                    _grid[x, y] = -1;
                write--;
            }

            for (var y = write; y >= 0; y--)
                _grid[x, y] = _random.Next(Kinds);
        }

        return true;
    }

    private List<(int X, int Y)> Find()
    {
        var cells = new HashSet<(int X, int Y)>();
        for (var y = 0; y < Cells; y++)
        {
            var run = 1;
            for (var x = 1; x <= Cells; x++)
            {
                if (x < Cells && _grid[x, y] >= 0 && _grid[x, y] == _grid[x - 1, y])
                {
                    run++;
                    continue;
                }

                if (run >= 3)
                {
                    for (var i = 0; i < run; i++)
                        cells.Add((x - 1 - i, y));
                }

                run = 1;
            }
        }

        for (var x = 0; x < Cells; x++)
        {
            var run = 1;
            for (var y = 1; y <= Cells; y++)
            {
                if (y < Cells && _grid[x, y] >= 0 && _grid[x, y] == _grid[x, y - 1])
                {
                    run++;
                    continue;
                }

                if (run >= 3)
                {
                    for (var i = 0; i < run; i++)
                        cells.Add((x, y - 1 - i));
                }

                run = 1;
            }
        }

        return new List<(int X, int Y)>(cells);
    }

    private bool HasMove()
    {
        for (var x = 0; x < Cells; x++)
        for (var y = 0; y < Cells; y++)
        {
            if (x + 1 < Cells && WouldMatch(x, y, x + 1, y))
                return true;
            if (y + 1 < Cells && WouldMatch(x, y, x, y + 1))
                return true;
        }

        return false;
    }

    private bool WouldMatch(int x, int y, int x2, int y2)
    {
        Swap(x, y, x2, y2);
        var matched = Find().Count > 0;
        Swap(x, y, x2, y2);
        return matched;
    }

    private void Swap(int x, int y, int x2, int y2)
    {
        (_grid[x, y], _grid[x2, y2]) = (_grid[x2, y2], _grid[x, y]);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Visual.Field is { } field)
            FillBackground(handle, field);

        var inner = Inner(8);
        var cell = (int) Math.Min(inner.Width, inner.Height) / Cells;
        if (cell < 4)
            return;

        var originX = (int) (inner.Left + (inner.Width - cell * Cells) / 2);
        var originY = (int) (inner.Top + (inner.Height - cell * Cells) / 2);
        for (var x = 0; x < Cells; x++)
        for (var y = 0; y < Cells; y++)
        {
            var drop = _offset[x, y] * cell;
            var rect = new UIBox2(originX + x * cell + 2, originY + y * cell + 2 + drop, originX + (x + 1) * cell - 2, originY + (y + 1) * cell - 2 + drop);
            if (_pop[x, y])
            {
                var shrink = (0.16f - MathF.Max(_pulse, 0f)) / 0.16f * cell * 0.4f;
                rect = new UIBox2(rect.Left + shrink, rect.Top + shrink, rect.Right - shrink, rect.Bottom - shrink);
            }
            else if (x == _sx && y == _sy)
            {
                handle.DrawRect(new UIBox2(rect.Left - 1, rect.Top - 1, rect.Right + 1, rect.Bottom + 1), Color.FromHex("#f2d36b"));
            }
            else if (x == _cx && y == _cy)
            {
                handle.DrawRect(rect, Color.FromHex("#ffffff55"));
            }

            var kind = _grid[x, y];
            if (kind < 0)
                continue;

            var pad = _pop[x, y] ? 0f : cell * 0.12f;
            handle.DrawTextureRect(PdaArcadeArt.Texture(PdaArcadeArt.Profile.Fruits[kind]), new UIBox2(rect.Left + pad, rect.Top + pad, rect.Right - pad, rect.Bottom - pad));
        }

        for (var i = 0; i <= Cells; i++)
        {
            var x = originX + i * cell;
            var y = originY + i * cell;
            handle.DrawLine(new System.Numerics.Vector2(x, originY), new System.Numerics.Vector2(x, originY + cell * Cells), Color.FromHex("#12151f"));
            handle.DrawLine(new System.Numerics.Vector2(originX, y), new System.Numerics.Vector2(originX + cell * Cells, y), Color.FromHex("#12151f"));
        }
    }
}
