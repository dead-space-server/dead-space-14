// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using Content.Shared.DeadSpace.PdaArcade;

namespace Content.Server.DeadSpace.PdaArcade;

public static class PdaTablePlay
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Fresh(string game, IReadOnlyDictionary<string, float>? numbers = null)
    {
        return game switch
        {
            PdaArcadeGame.Cross => ".........",
            PdaArcadeGame.Ships => "H|3221|3221|" + new string('0', 72),
            PdaArcadeGame.Pong => PongStart(numbers),
            PdaArcadeGame.Chess => "rnbqkbnrpppppppp................................PPPPPPPPRNBQKBNRw",
            PdaArcadeGame.Checks => FreshChecks(),
            _ => "",
        };
    }

    private static string PongStart(IReadOnlyDictionary<string, float>? numbers)
    {
        var speed = Num(numbers, "ballStart", 0.28f);
        var lift = Num(numbers, "ballLift", 0.18f);
        return string.Create(Inv, $"0.500 0.500 0.500 0.500 {speed:0.000} {lift:0.000} 0 0");
    }

    public static string View(string game, string board, bool host)
    {
        if (game == PdaArcadeGame.Ships)
            return ViewShips(board, host);

        return board;
    }

    private static string ViewShips(string board, bool host)
    {
        if (!SplitShips(board, out var phase, out var hostShips, out var guestShips, out var grids))
            return board;

        var ownShips = host ? hostShips : guestShips;
        var foeShips = host ? guestShips : hostShips;
        var ownGrid = host ? grids[..36] : grids[36..];
        var foeGrid = (host ? grids[36..] : grids[..36]).ToCharArray();
        for (var i = 0; i < foeGrid.Length; i++)
        {
            if (foeGrid[i] == '1')
                foeGrid[i] = '0';
        }

        var you = host ? phase is 'H' or 'h' : phase is 'G' or 'g';
        var shown = phase is 'H' or 'G' ? (you ? 'Y' : 'W') : you ? 'S' : 'W';
        return shown + "|" + ownShips + "|" + foeShips + "|" + ownGrid + new string(foeGrid);
    }

    private static bool SplitShips(string board, out char phase, out string hostShips, out string guestShips, out string grids)
    {
        phase = 'H';
        hostShips = "";
        guestShips = "";
        grids = "";
        var parts = board.Split('|');
        if (parts.Length < 4 || parts[0].Length == 0 || parts[3].Length < 72)
            return false;

        phase = parts[0][0];
        hostShips = parts[1];
        guestShips = parts[2];
        grids = parts[3][..72];
        return true;
    }

    public static bool TryMove(string game, ref string board, bool host, string move, out string status, out bool over, out int score)
    {
        status = "";
        over = false;
        score = 0;
        return game switch
        {
            PdaArcadeGame.Cross => Cross(ref board, host, move, out status, out over, out score),
            PdaArcadeGame.Ships => Ships(ref board, host, move, out status, out over, out score),
            PdaArcadeGame.Chess => Chess(ref board, host, move, out status, out over, out score),
            PdaArcadeGame.Checks => Checks(ref board, host, move, out status, out over, out score),
            _ => false,
        };
    }

    public static void Step(string game, ref string board, int hostInput, int guestInput, float dt, IReadOnlyDictionary<string, float>? numbers, out bool over, out int hostScore, out int guestScore)
    {
        over = false;
        hostScore = 0;
        guestScore = 0;
        if (game == PdaArcadeGame.Pong)
            StepPong(ref board, hostInput, guestInput, dt, numbers, out over, out hostScore, out guestScore);
    }

    private static bool Cross(ref string board, bool host, string move, out string status, out bool over, out int score)
    {
        status = "";
        over = false;
        score = 0;
        if (board.Length != 9 || !int.TryParse(move, out var cell) || cell < 0 || cell > 8 || board[cell] != '.')
        {
            status = "pda-arcade-illegal";
            return false;
        }

        var mark = host ? 'x' : 'o';
        var chars = board.ToCharArray();
        var xs = 0;
        var os = 0;
        foreach (var ch in chars)
        {
            if (ch == 'x')
                xs++;
            else if (ch == 'o')
                os++;
        }

        if ((mark == 'x' && xs != os) || (mark == 'o' && xs != os + 1))
        {
            status = "pda-arcade-wait";
            return false;
        }

        chars[cell] = mark;
        board = new string(chars);
        if (Line(chars, mark))
        {
            over = true;
            score = 1;
            status = "pda-arcade-win";
            return true;
        }

        if (!board.Contains('.'))
        {
            over = true;
            status = "pda-arcade-draw";
            return true;
        }

        status = "pda-arcade-turn";
        return true;
    }

    private static bool Line(char[] cells, char mark)
    {
        int[][] lines =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
            new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
            new[] { 0, 4, 8 }, new[] { 2, 4, 6 },
        };
        foreach (var line in lines)
        {
            if (cells[line[0]] == mark && cells[line[1]] == mark && cells[line[2]] == mark)
                return true;
        }

        return false;
    }

    private static bool Ships(ref string board, bool host, string move, out string status, out bool over, out int score)
    {
        status = "pda-arcade-illegal";
        over = false;
        score = 0;
        if (!SplitShips(board, out var phase, out var hostShips, out var guestShips, out var grids))
            return false;

        var minePlace = host ? 'H' : 'G';
        var mineShot = host ? 'h' : 'g';
        var cells = grids.ToCharArray();
        var own = host ? 0 : 36;
        var foe = host ? 36 : 0;
        var parts = move.Split(',');
        if (phase == minePlace)
        {
            if (parts.Length != 4 || !int.TryParse(parts[0], out var size) || !int.TryParse(parts[1], out var row) || !int.TryParse(parts[2], out var col) || !int.TryParse(parts[3], out var dir))
                return false;

            var remain = host ? hostShips : guestShips;
            var token = size.ToString(CultureInfo.InvariantCulture);
            var at = remain.IndexOf(token, StringComparison.Ordinal);
            if (at < 0 || size < 1 || size > 4 || dir < 0 || dir > 1)
                return false;

            var spots = new int[size];
            for (var i = 0; i < size; i++)
            {
                var r = row + (dir == 1 ? i : 0);
                var c = col + (dir == 0 ? i : 0);
                if (r < 0 || c < 0 || r > 5 || c > 5)
                    return false;

                spots[i] = own + r * 6 + c;
                if (cells[spots[i]] != '0')
                    return false;
            }

            foreach (var spot in spots)
                cells[spot] = '1';

            remain = remain.Remove(at, 1);
            if (host)
                hostShips = remain;
            else
                guestShips = remain;

            if (remain.Length == 0)
                phase = host ? 'G' : 'h';

            board = PackShips(phase, hostShips, guestShips, cells);
            status = phase == 'h' ? "pda-arcade-turn" : "pda-arcade-place";
            return true;
        }

        if (phase != mineShot)
        {
            status = "pda-arcade-wait";
            return false;
        }

        if (parts.Length != 2 || !int.TryParse(parts[0], out var shotRow) || !int.TryParse(parts[1], out var shotCol))
            return false;

        if (shotRow < 0 || shotCol < 0 || shotRow > 5 || shotCol > 5)
            return false;

        var shot = foe + shotRow * 6 + shotCol;
        if (cells[shot] == '2' || cells[shot] == '3')
            return false;

        var hit = cells[shot] == '1';
        cells[shot] = hit ? '3' : '2';
        if (!hit)
            phase = host ? 'g' : 'h';

        var sunk = true;
        for (var i = 0; i < 36; i++)
        {
            if (cells[foe + i] == '1')
                sunk = false;
        }

        board = PackShips(phase, hostShips, guestShips, cells);
        if (sunk)
        {
            over = true;
            score = 1;
            status = "pda-arcade-win";
            return true;
        }

        status = hit ? "pda-arcade-hit" : "pda-arcade-miss";
        return true;
    }

    private static string PackShips(char phase, string hostShips, string guestShips, char[] cells)
    {
        return phase + "|" + hostShips + "|" + guestShips + "|" + new string(cells);
    }

    private static string FreshChecks()
    {
        var cells = new char[64];
        Array.Fill(cells, '.');
        for (var i = 0; i < 64; i++)
        {
            var row = i / 8;
            var col = i % 8;
            if ((row + col) % 2 == 0)
                continue;

            if (row <= 2)
                cells[i] = 'b';
            else if (row >= 5)
                cells[i] = 'w';
        }

        return new string(cells) + "w";
    }

    private static bool Checks(ref string board, bool host, string move, out string status, out bool over, out int score)
    {
        status = "pda-arcade-illegal";
        over = false;
        score = 0;
        if (board.Length != 65)
            return false;

        var side = host ? 'w' : 'b';
        if (char.ToLowerInvariant(board[64]) != side)
        {
            status = "pda-arcade-wait";
            return false;
        }

        var dash = move.IndexOf('-');
        if (dash <= 0 || !int.TryParse(move[..dash], out var from) || !int.TryParse(move[(dash + 1)..], out var to))
            return false;

        if (from < 0 || to < 0 || from > 63 || to > 63 || from == to)
            return false;

        var cells = board.ToCharArray();
        var piece = cells[from];
        if (char.ToLowerInvariant(piece) != side || cells[to] != '.')
            return false;

        var fr = from / 8;
        var fc = from % 8;
        var tr = to / 8;
        var tc = to % 8;
        var dr = tr - fr;
        var dc = tc - fc;
        if (Math.Abs(dr) != Math.Abs(dc) || dr == 0)
            return false;

        var king = char.IsUpper(piece);
        var forward = side == 'w' ? -1 : 1;
        if (!king && Math.Abs(dr) == 1 && dr == forward)
        {
            cells[to] = piece;
            cells[from] = '.';
        }
        else if (Math.Abs(dr) == 2)
        {
            var mid = (fr + tr) / 2 * 8 + (fc + tc) / 2;
            var jumped = cells[mid];
            if (jumped == '.' || char.ToLowerInvariant(jumped) == side)
                return false;

            if (!king && dr != forward * 2)
                return false;

            cells[mid] = '.';
            cells[to] = piece;
            cells[from] = '.';
        }
        else if (king && Math.Abs(dr) > 0)
        {
            var stepR = Math.Sign(dr);
            var stepC = Math.Sign(dc);
            var jumped = 0;
            var r = fr + stepR;
            var c = fc + stepC;
            while (r != tr || c != tc)
            {
                var at = cells[r * 8 + c];
                if (at != '.')
                {
                    if (char.ToLowerInvariant(at) == side)
                        return false;

                    jumped++;
                }

                r += stepR;
                c += stepC;
            }

            if (jumped > 1)
                return false;

            if (jumped == 1)
            {
                r = fr + stepR;
                c = fc + stepC;
                while (r != tr || c != tc)
                {
                    var index = r * 8 + c;
                    if (cells[index] != '.')
                        cells[index] = '.';

                    r += stepR;
                    c += stepC;
                }
            }

            cells[to] = piece;
            cells[from] = '.';
        }
        else
        {
            return false;
        }

        if (side == 'w' && to / 8 == 0)
            cells[to] = 'W';
        if (side == 'b' && to / 8 == 7)
            cells[to] = 'B';

        cells[64] = side == 'w' ? 'b' : 'w';
        board = new string(cells);
        var foe = side == 'w' ? 'b' : 'w';
        var any = false;
        foreach (var ch in cells)
        {
            if (char.ToLowerInvariant(ch) == foe)
                any = true;
        }

        if (!any)
        {
            over = true;
            score = 1;
            status = "pda-arcade-win";
            return true;
        }

        status = "pda-arcade-turn";
        return true;
    }

    private static bool Chess(ref string board, bool host, string move, out string status, out bool over, out int score)
    {
        status = "pda-arcade-illegal";
        over = false;
        score = 0;
        if (board.Length != 65)
            return false;

        var white = host;
        var mark = white ? 'w' : 'b';
        if (board[64] != mark)
        {
            status = "pda-arcade-wait";
            return false;
        }

        var dash = move.IndexOf('-');
        if (dash <= 0 || !int.TryParse(move[..dash], out var from) || !int.TryParse(move[(dash + 1)..], out var to))
            return false;

        if (!ChessSlide(board, from, to, white, out var next))
            return false;

        if (Attacked(next, white))
            return false;

        var foe = white ? 'b' : 'w';
        next = next[..64] + foe;
        board = next;
        if (!HasMove(board, !white))
        {
            over = true;
            if (Attacked(board, !white))
            {
                score = 1;
                status = "pda-arcade-win";
            }
            else
            {
                status = "pda-arcade-draw";
            }

            return true;
        }

        status = "pda-arcade-turn";
        return true;
    }

    private static bool ChessSlide(string board, int from, int to, bool white, out string next)
    {
        next = board;
        if (from < 0 || to < 0 || from > 63 || to > 63 || from == to)
            return false;

        var piece = board[from];
        if (piece == '.' || char.IsUpper(piece) != white)
            return false;

        var target = board[to];
        if (target != '.' && char.IsUpper(target) == white)
            return false;

        var fr = from / 8;
        var fc = from % 8;
        var tr = to / 8;
        var tc = to % 8;
        var dr = tr - fr;
        var dc = tc - fc;
        var kind = char.ToLowerInvariant(piece);
        var ok = kind switch
        {
            'p' => Pawn(board, fr, fc, tr, tc, white),
            'n' => Math.Abs(dr) * Math.Abs(dc) == 2,
            'k' => Math.Max(Math.Abs(dr), Math.Abs(dc)) == 1,
            'r' => Clear(board, fr, fc, tr, tc, true),
            'b' => Clear(board, fr, fc, tr, tc, false),
            'q' => Clear(board, fr, fc, tr, tc, true) || Clear(board, fr, fc, tr, tc, false),
            _ => false,
        };
        if (!ok)
            return false;

        var cells = board.ToCharArray();
        cells[to] = piece;
        cells[from] = '.';
        if (kind == 'p' && (tr == 0 || tr == 7))
            cells[to] = white ? 'Q' : 'q';

        next = new string(cells, 0, 64) + board[64];
        return true;
    }

    private static bool Pawn(string board, int fr, int fc, int tr, int tc, bool white)
    {
        var dir = white ? -1 : 1;
        var start = white ? 6 : 1;
        if (tc == fc && board[tr * 8 + tc] == '.' && tr == fr + dir)
            return true;

        if (tc == fc && fr == start && tr == fr + dir * 2 && board[(fr + dir) * 8 + fc] == '.' && board[tr * 8 + tc] == '.')
            return true;

        if (Math.Abs(tc - fc) == 1 && tr == fr + dir)
        {
            var at = board[tr * 8 + tc];
            return at != '.' && char.IsUpper(at) != white;
        }

        return false;
    }

    private static bool Clear(string board, int fr, int fc, int tr, int tc, bool ortho)
    {
        var dr = tr - fr;
        var dc = tc - fc;
        if (ortho)
        {
            if (dr != 0 && dc != 0)
                return false;
        }
        else if (Math.Abs(dr) != Math.Abs(dc) || dr == 0)
        {
            return false;
        }

        var stepR = Math.Sign(dr);
        var stepC = Math.Sign(dc);
        var r = fr + stepR;
        var c = fc + stepC;
        while (r != tr || c != tc)
        {
            if (board[r * 8 + c] != '.')
                return false;

            r += stepR;
            c += stepC;
        }

        return true;
    }

    private static bool Attacked(string board, bool white)
    {
        var king = white ? 'K' : 'k';
        var at = board.IndexOf(king);
        if (at < 0)
            return true;

        for (var i = 0; i < 64; i++)
        {
            if (board[i] == '.' || char.IsUpper(board[i]) == white)
                continue;

            if (ChessSlide(board, i, at, !white, out _))
                return true;
        }

        return false;
    }

    private static bool HasMove(string board, bool white)
    {
        for (var from = 0; from < 64; from++)
        {
            if (board[from] == '.' || char.IsUpper(board[from]) != white)
                continue;

            for (var to = 0; to < 64; to++)
            {
                if (!ChessSlide(board, from, to, white, out var next))
                    continue;

                if (!Attacked(next, white))
                    return true;
            }
        }

        return false;
    }


    private static void StepPong(ref string board, int hostInput, int guestInput, float dt, IReadOnlyDictionary<string, float>? numbers, out bool over, out int hostScore, out int guestScore)
    {
        over = false;
        hostScore = 0;
        guestScore = 0;
        var parts = board.Split(' ');
        if (parts.Length != 8)
            return;

        var padA = float.Parse(parts[0], Inv);
        var padB = float.Parse(parts[1], Inv);
        var x = float.Parse(parts[2], Inv);
        var y = float.Parse(parts[3], Inv);
        var vx = float.Parse(parts[4], Inv);
        var vy = float.Parse(parts[5], Inv);
        var sa = int.Parse(parts[6], Inv);
        var sb = int.Parse(parts[7], Inv);
        var speed = MathF.Sqrt(vx * vx + vy * vy);
        var cap = Num(numbers, "speedCap", 1.15f);
        var gain = Num(numbers, "speedGain", 0.05f);
        if (speed > 0.01f)
        {
            var next = MathF.Min(cap, speed + dt * gain);
            vx *= next / speed;
            vy *= next / speed;
        }

        var paddle = Num(numbers, "paddle", 0.85f);
        var padMin = Num(numbers, "padMin", 0.12f);
        var padMax = Num(numbers, "padMax", 0.88f);
        padA = Math.Clamp(padA + hostInput * paddle * dt, padMin, padMax);
        padB = Math.Clamp(padB + guestInput * paddle * dt, padMin, padMax);
        x += vx * dt;
        y += vy * dt;
        var wall = Num(numbers, "wall", 0.04f);
        var goal = Num(numbers, "goal", 0.08f);
        var reach = Num(numbers, "reach", 0.14f);
        if (y < wall || y > 1f - wall)
            vy = -vy;

        var bump = Num(numbers, "bump", 0.02f);
        if (x < goal && Math.Abs(y - padA) < reach && vx < 0f)
            vx = Math.Abs(vx) + bump;
        if (x > 1f - goal && Math.Abs(y - padB) < reach && vx > 0f)
            vx = -Math.Abs(vx) - bump;

        if (x < 0f)
        {
            sb++;
            x = 0.5f;
            y = 0.5f;
            Serve(sa, sb, numbers, out vx, out vy);
        }
        else if (x > 1f)
        {
            sa++;
            x = 0.5f;
            y = 0.5f;
            Serve(sa, sb, numbers, out vx, out vy);
            vx = -vx;
            vy = -vy;
        }

        var win = (int)Num(numbers, "win", 5f);
        if (sa >= win || sb >= win)
        {
            over = true;
            hostScore = sa;
            guestScore = sb;
        }

        board = string.Create(Inv, $"{padA:0.000} {padB:0.000} {x:0.000} {y:0.000} {vx:0.000} {vy:0.000} {sa} {sb}");
    }

    private static void Serve(int sa, int sb, IReadOnlyDictionary<string, float>? numbers, out float vx, out float vy)
    {
        var rally = Num(numbers, "rallyBase", 0.28f) + (sa + sb) * Num(numbers, "rallyStep", 0.035f);
        vx = rally;
        vy = Num(numbers, "liftBase", 0.16f) * (1f + (sa + sb) * Num(numbers, "liftStep", 0.08f));
    }

    private static float Num(IReadOnlyDictionary<string, float>? numbers, string key, float fallback)
    {
        return numbers != null && numbers.TryGetValue(key, out var value) ? value : fallback;
    }

}
