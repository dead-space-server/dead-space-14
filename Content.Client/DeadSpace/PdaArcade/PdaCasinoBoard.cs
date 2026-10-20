// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaCasinoBoard : PdaArcadeBoard
{
    private const int CasinoWidth = 5;
    private const int CasinoHeight = 3;
    private const int ValueCount = 7;
    private readonly int[,] _grid = new int[CasinoWidth, CasinoHeight];
    private readonly bool[] _winningRows = new bool[CasinoHeight];
    private readonly bool[] _winningColumns = new bool[CasinoWidth];
    private readonly bool[] _winningDiagonalDown = new bool[CasinoWidth - 2];
    private readonly bool[] _winningDiagonalUp = new bool[CasinoWidth - 2];
    private readonly Random _random = new();
    private const float SpinDuration = 1f;
    private const float SymbolChangeInterval = 0.06f;
    private readonly int[,] _resultGrid = new int[CasinoWidth, CasinoHeight];
    private float _spinTime;
    private float _symbolChangeTime;
    private bool _spinning;

    protected override void OnRestart()
    {
        Gain(100);
        for (var x = 0; x < CasinoWidth; x++)
        for (var y = 0; y < CasinoHeight; y++)
            _grid[x, y] = _random.Next(1, ValueCount + 1);
    }

    public override void Command(string command, bool down)
    {
        if (!down || command != "act" || _spinning)
            return;
        SpinWithReward();
    }

    private int GetHorizontalMatchLenght(int StartX, int y)
    {
        var value = _grid[StartX, y];
        var length = 1;

        for (var x = StartX + 1; x < CasinoWidth; x++)
        {
            if (_grid[x, y] != value)
                break;
            length++;
        }

        return length;
    }

    private static int GetHorizontalMultiplier(int length)
    {
        return length switch
        {
            3 => 1,
            4 => 2,
            5 => 3,
            _ => 0
        };
    }

    private static int GetValueCost(int value)
    {
        return value switch
        {
            1 => 2,
            2 => 4,
            3 => 8,
            4 => 16,
            5 => 32,
            6 => 64,
            7 => 128,
            _ => 1,
        };
    }

    private void CheckHorizontal()
    {
        Array.Clear(_winningRows);
        for (var y = 0; y < CasinoHeight; y++)
        {
            for (var x = 0; x <= CasinoWidth - 3; x++)
            {
                var value = _grid[x, y];
                if (x > 0 && _grid[x - 1, y] == value)
                    continue;

                var length = GetHorizontalMatchLenght(x, y);

                if (length < 3)
                    continue;

                var multiplier = GetHorizontalMultiplier(length);
                var valueCost = GetValueCost(value);

                Gain(valueCost * multiplier);
                _winningRows[y] = true;
                x += length - 1;
            }
        }
    }

    private int GetVerticalMatchLenght(int x, int StartY)
    {
        var value = _grid[x, StartY];
        var length = 1;

        for (var y = StartY + 1; y < CasinoHeight; y++)
        {
            if (_grid[x, y] != value)
                break;
            length++;
        }

        return length;
    }

    private void CheckVertical()
    {
        Array.Clear(_winningColumns);
        for (var x = 0; x < CasinoWidth; x++)
        {
            for (var y = 0; y <= CasinoHeight - 3; y++)
            {
                var value = _grid[x, y];
                if (y > 0 && _grid[x, y - 1] == value)
                    continue;

                var length = GetVerticalMatchLenght(x, y);

                if (length < 3)
                    continue;

                var multiplier = GetHorizontalMultiplier(length);
                var valueCost = GetValueCost(value);

                Gain(valueCost * multiplier);
                _winningColumns[x] = true;
                y += length - 1;
            }
        }
    }

    private void CheckDiagonal()
    {
        Array.Clear(_winningDiagonalDown);
        Array.Clear(_winningDiagonalUp);

        for (var x = 0; x <= CasinoWidth - 3; x++)
        {
            for (var y = 0; y < CasinoHeight; y++)
            {
                if (y <= CasinoHeight - 3)
                {
                    var value = _grid[x, y];

                    if (_grid[x + 1, y + 1] == value && _grid[x + 2, y + 2] == value)
                    {
                        Gain(GetValueCost(value));
                        _winningDiagonalDown[x] = true;
                    }
                }

                if (y >= 2)
                {
                    var value = _grid[x, y];

                    if (_grid[x + 1, y - 1] == value && _grid[x + 2, y - 2] == value)
                    {
                        Gain(GetValueCost(value));
                        _winningDiagonalUp[x] = true;
                    }
                }
            }
        }
    }

    private void ForceWinningLine(int[,] grid)
    {
        var y = _random.Next(CasinoHeight);
        var startX = _random.Next(CasinoWidth - 2);
        var value = _random.Next(1, ValueCount + 1);

        for (var x = startX; x < startX + 3; x++)
            grid[x, y] = value;
    }

    private void CreateResult()
    {
        for (var x = 0; x < CasinoWidth; x++)
        {
            for (var y = 0; y < CasinoHeight; y++)
            {
                _resultGrid[x, y] = _random.Next(1, ValueCount + 1);
            }
        }
        if (_random.NextDouble() < 0.20)
            ForceWinningLine(_resultGrid);
    }

    private void SpinWithReward()
    {
        if (Over || IsPaused)
            return;

        Array.Clear(_winningRows);
        Array.Clear(_winningColumns);
        Array.Clear(_winningDiagonalDown);
        Array.Clear(_winningDiagonalUp);

        Gain(-10);
        CreateResult();

        _spinTime = 0f;
        _symbolChangeTime = 0f;
        _spinning = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_spinning)
            return;
        _spinTime += args.DeltaSeconds;
        _symbolChangeTime += args.DeltaSeconds;

        if (_symbolChangeTime >= SymbolChangeInterval)
        {
            _symbolChangeTime = 0f;

            for (var x = 0; x < CasinoWidth; x++)
            {
                for (var y = 0; y < CasinoHeight; y++)
                {
                    _grid[x, y] = _random.Next(1, ValueCount + 1);
                }
            }
        }
        if (_spinTime < SpinDuration)
            return;

        _spinning = false;
        Array.Copy(_resultGrid, _grid, _grid.Length);

        CheckHorizontal();
        CheckVertical();
        CheckDiagonal();

        var won = _winningRows.Any(value => value) || _winningColumns.Any(value => value) || _winningDiagonalDown.Any(value => value) || _winningDiagonalUp.Any(value => value);

        if (!won)
            Gain(-10);
        if (Score <= 0)
            End();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawFloor(handle);

        var inner = Inner(10);
        var cell = (int)Math.Min(inner.Width / CasinoWidth, inner.Height / CasinoHeight);
        if (cell < 8)
            return;

        var originX = (int)(inner.Left + (inner.Width - cell * CasinoWidth) / 2);
        var originY = (int)(inner.Top + (inner.Height - cell * CasinoHeight) / 2);
        var font = PdaArcadeArt.Font(Math.Max(10, cell / 3));

        for (var x = 0; x < CasinoWidth; x++)
        {
            for (var y = 0; y < CasinoHeight; y++)
            {
                var rect = new UIBox2(
                    originX + x * cell + 3,
                    originY + y * cell + 3,
                    originX + (x + 1) * cell - 3,
                    originY + (y + 1) * cell - 3);

                handle.DrawRect(rect, Color.FromHex("#cdc1b4"));

                var value = _grid[x, y];
                var iconRect = new UIBox2(rect.Left + cell * 0.12f,
                    rect.Top + cell * 0.12f,
                    rect.Right - cell * 0.12f,
                    rect.Bottom - cell * 0.12f);

                if (value == 7)
                {
                    const string text = "7";
                    var size = handle.GetDimensions(font, text, 1f);
                    var pos = new System.Numerics.Vector2(rect.Left + (rect.Width - size.X) / 2f, rect.Top + (rect.Height - size.Y) / 2f);
                    handle.DrawString(font, pos, text);
                }
                else
                {
                    var spriteKey = value switch
                    {
                        6 => "diamond",
                        5 => "gold",
                        4 => "clover",
                        3 => "bell",
                        2 => "lemon",
                        1 => "cherry",
                        _ => throw new InvalidOperationException($"Unexpected casino symbol: {value}"),
                    };

                    handle.DrawTextureRect(Sprite(spriteKey), iconRect);
                }
            }
        }

        for (var y = 0; y < CasinoHeight; y++)
        {
            if (!_winningRows[y])
                continue;

            var lineY = originY + y * cell + cell / 2;

            handle.DrawLine(new System.Numerics.Vector2(originX, lineY), new System.Numerics.Vector2(originX + CasinoWidth * cell, lineY), Color.Red);
        }
        for (var x = 0; x < CasinoWidth; x++)
        {
            if (!_winningColumns[x])
                continue;

            var lineX = originX + x * cell + cell / 2;

            handle.DrawLine(new System.Numerics.Vector2(lineX, originY), new System.Numerics.Vector2(lineX, originY + CasinoHeight * cell), Color.Red);
        }
        for (var x = 0; x < CasinoWidth - 2; x++)
        {
            if (_winningDiagonalDown[x])
            {
                handle.DrawLine(new System.Numerics.Vector2(originX + x * cell + cell / 2, originY + cell / 2), new System.Numerics.Vector2(originX + (x + 2) * cell + cell / 2, originY + 2 * cell + cell / 2), Color.Red);
            }
            if (_winningDiagonalUp[x])
            {
                handle.DrawLine(new System.Numerics.Vector2(originX + x * cell + cell / 2, originY + 2 * cell + cell / 2), new System.Numerics.Vector2(originX + (x + 2) * cell + cell / 2, originY + cell / 2), Color.Red);
            }
        }
    }
}
