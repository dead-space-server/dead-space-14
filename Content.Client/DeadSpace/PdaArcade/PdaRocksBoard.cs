// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaRocksBoard : PdaArcadeBoard
{
    private readonly List<Rock> _rocks = new();
    private readonly List<Shot> _shots = new();
    private Vector2 _ship;
    private Vector2 _vel;
    private float _angle;
    private float _cool;
    private bool _left;
    private bool _right;
    private bool _thrust;
    private readonly Random _random = new();

    public PdaRocksBoard()
    {
        OnKeyBindUp += OnRelease;
    }

    public void HoldLeft(bool down) => _left = down;
    public void HoldRight(bool down) => _right = down;
    public void HoldThrust(bool down) => _thrust = down;

    public override void Command(string command, bool down)
    {
        if (command == "left")
            HoldLeft(down);
        else if (command == "right")
            HoldRight(down);
        else if (command == "up")
            HoldThrust(down);
        else if (command == "act" && down)
            Fire();
    }

    public void Kick()
    {
        var dir = new Vector2(MathF.Sin(_angle), -MathF.Cos(_angle));
        _vel += dir * 40f;
    }

    public void NudgeAngle(float delta)
    {
        _angle += delta;
    }

    public void Fire()
    {
        if (Over || _cool > 0f)
            return;

        _cool = 0.28f;
        var dir = new Vector2(MathF.Sin(_angle), -MathF.Cos(_angle));
        _shots.Add(new Shot(_ship + dir * 12f, dir * 160f, 0.9f));
    }

    protected override void OnRestart()
    {
        _rocks.Clear();
        _shots.Clear();
        _ship = new Vector2(100, 100);
        _vel = Vector2.Zero;
        _angle = 0f;
        _cool = 0f;
        _left = false;
        _right = false;
        _thrust = false;
        for (var i = 0; i < 4; i++)
            _rocks.Add(Make(2, new Vector2(_random.Next(40, 160), _random.Next(20, 70))));
    }

    protected override void OnKey(GUIBoundKeyEventArgs args)
    {
        base.OnKey(args);
        if (Key(args, EngineKeyFunctions.TextCursorLeft) || Key(args, EngineKeyFunctions.MoveLeft))
            _left = true;
        else if (Key(args, EngineKeyFunctions.TextCursorRight) || Key(args, EngineKeyFunctions.MoveRight))
            _right = true;
        else if (Key(args, EngineKeyFunctions.TextCursorUp) || Key(args, EngineKeyFunctions.MoveUp))
            _thrust = true;
        else if (Key(args, EngineKeyFunctions.Use) || Key(args, EngineKeyFunctions.Use))
            Fire();
    }

    private void OnRelease(GUIBoundKeyEventArgs args)
    {
        if (Key(args, EngineKeyFunctions.TextCursorLeft) || Key(args, EngineKeyFunctions.MoveLeft))
            _left = false;
        else if (Key(args, EngineKeyFunctions.TextCursorRight) || Key(args, EngineKeyFunctions.MoveRight))
            _right = false;
        else if (Key(args, EngineKeyFunctions.TextCursorUp) || Key(args, EngineKeyFunctions.MoveUp))
            _thrust = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Over || IsPaused)
            return;

        var dt = args.DeltaSeconds;
        if (_left)
            _angle -= 2.6f * dt;
        if (_right)
            _angle += 2.6f * dt;

        var dir = new Vector2(MathF.Sin(_angle), -MathF.Cos(_angle));
        if (_thrust)
            _vel += dir * 80f * dt;
        _vel *= MathF.Pow(0.35f, dt);
        _ship += _vel * dt;
        Bounce(ref _ship, ref _vel, 8f);
        _cool -= dt;

        for (var i = _shots.Count - 1; i >= 0; i--)
        {
            var shot = _shots[i];
            shot.Pos += shot.Vel * dt;
            shot.Life -= dt;
            _shots[i] = shot;
            if (shot.Life <= 0f || shot.Pos.X < 0f || shot.Pos.Y < 0f || shot.Pos.X > 200f || shot.Pos.Y > 200f)
                _shots.RemoveAt(i);
        }

        for (var i = _rocks.Count - 1; i >= 0; i--)
        {
            var rock = _rocks[i];
            rock.Pos += rock.Vel * dt;
            Bounce(ref rock.Pos, ref rock.Vel, rock.Radius);
            _rocks[i] = rock;
            if (Vector2.Distance(_ship, rock.Pos) < rock.Radius + 6f)
            {
                End();
                return;
            }

            for (var s = _shots.Count - 1; s >= 0; s--)
            {
                if (Vector2.Distance(_shots[s].Pos, rock.Pos) > rock.Radius)
                    continue;

                _shots.RemoveAt(s);
                Gain(rock.Size == 2 ? 20 : rock.Size == 1 ? 40 : 80);
                _rocks.RemoveAt(i);
                if (rock.Size > 0)
                {
                    _rocks.Add(Make(rock.Size - 1, rock.Pos));
                    _rocks.Add(Make(rock.Size - 1, rock.Pos));
                }

                break;
            }
        }

        if (_rocks.Count == 0)
        {
            for (var i = 0; i < 5; i++)
                _rocks.Add(Make(2, new Vector2(_random.Next(20, 180), _random.Next(20, 180))));
        }
    }

    private Rock Make(int size, Vector2 pos)
    {
        var angle = (float) (_random.NextDouble() * Math.PI * 2);
        var speed = 18f + (2 - size) * 16f;
        return new Rock(pos, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed, size, 5f + size * 4f);
    }

    private static void Bounce(ref Vector2 pos, ref Vector2 vel, float pad)
    {
        if (pos.X < pad)
        {
            pos.X = pad;
            vel.X = MathF.Abs(vel.X);
        }

        if (pos.Y < pad)
        {
            pos.Y = pad;
            vel.Y = MathF.Abs(vel.Y);
        }

        if (pos.X > 200f - pad)
        {
            pos.X = 200f - pad;
            vel.X = -MathF.Abs(vel.X);
        }

        if (pos.Y > 200f - pad)
        {
            pos.Y = 200f - pad;
            vel.Y = -MathF.Abs(vel.Y);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawFloor(handle);
        var inner = Inner(6);
        var scale = Math.Min(inner.Width, inner.Height) / 200f;
        if (scale <= 0f)
            return;

        var origin = new Vector2(inner.Left + (inner.Width - 200 * scale) / 2f, inner.Top + (inner.Height - 200 * scale) / 2f);
        handle.DrawRect(new UIBox2(origin.X, origin.Y, origin.X + 200 * scale, origin.Y + 200 * scale), Color.FromHex("#182033"));
        var nose = origin + _ship * scale;
        var dir = new Vector2(MathF.Sin(_angle), -MathF.Cos(_angle));
        var side = new Vector2(dir.Y, -dir.X);
        handle.DrawLine(nose + dir * 8f * scale, nose - dir * 6f * scale + side * 5f * scale, Color.FromHex("#f4f4f4"));
        handle.DrawLine(nose + dir * 8f * scale, nose - dir * 6f * scale - side * 5f * scale, Color.FromHex("#f4f4f4"));
        foreach (var rock in _rocks)
        {
            var center = origin + rock.Pos * scale;
            handle.DrawCircle(center, rock.Radius * scale, Color.FromHex("#c4b39a"), false);
        }

        foreach (var shot in _shots)
            handle.DrawCircle(origin + shot.Pos * scale, 2f, Color.FromHex("#fff1a8"), true);
    }

    private struct Rock
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public int Size;
        public float Radius;

        public Rock(Vector2 pos, Vector2 vel, int size, float radius)
        {
            Pos = pos;
            Vel = vel;
            Size = size;
            Radius = radius;
        }
    }

    private struct Shot
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;

        public Shot(Vector2 pos, Vector2 vel, float life)
        {
            Pos = pos;
            Vel = vel;
            Life = life;
        }
    }
}
