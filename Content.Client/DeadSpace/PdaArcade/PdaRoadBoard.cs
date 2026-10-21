// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaRoadBoard : PdaArcadeBoard
{
    private readonly List<Traffic> _traffic = new();
    private readonly Random _random = new();
    private float _x = 0.5f;
    private float _speed = 0.35f;
    private float _scroll;
    private float _fuel = 100f;
    private float _spawn = 0.4f;
    private float _spin;
    private bool _left;
    private bool _right;
    private bool _fast;
    private int _mark;
    private bool _brake;

    public override void Command(string command, bool down)
    {
        if (command == "left")
            _left = down;
        else if (command == "right")
            _right = down;
        else if (command == "up")
            _fast = down;
        else if (command == "down")
            _brake = down;
    }

    protected override void OnRestart()
    {
        _traffic.Clear();
        _x = Num("startX", 0.5f);
        _speed = Num("startSpeed", 0.35f);
        _scroll = 0f;
        _fuel = Num("startFuel", 100f);
        _spawn = Num("startSpawn", 0.4f);
        _spin = 0f;
        _left = false;
        _right = false;
        _fast = false;
        _mark = 0;
        _brake = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (Over || IsPaused)
            return;

        var dt = args.DeltaSeconds;
        var steer = (_right ? 1f : 0f) - (_left ? 1f : 0f);
        if (_spin != 0f)
        {
            _x += _spin * dt;
            _spin += steer * dt * Num("spinSteer", 1.4f);
            if (MathF.Abs(_spin) < Num("spinStop", 0.15f) && steer != 0f)
                _spin = 0f;
        }
        else
        {
            _x = Math.Clamp(_x + steer * dt * Num("steer", 0.85f), Num("laneMin", 0.12f), Num("laneMax", 0.88f));
        }

        var gear = _brake ? Num("brake", -0.45f) : _fast ? Num("accel", 0.55f) : Num("cruise", 0.12f);
        _speed = Math.Clamp(_speed + gear * dt, Num("speedMin", 0.15f), _fast ? Num("speedFast", 1.35f) : Num("speedCruise", 0.72f));
        _scroll += _speed * dt * Num("scroll", 140f);
        _fuel -= (_fast ? Num("fuelFast", 9f) : Num("fuelCruise", 5f)) * dt;
        Hum("ambient");
        if (_x < Num("edge", 0.16f) || _x > 1f - Num("edge", 0.16f))
        {
            _fuel -= Num("edgeFuel", 28f);
            _x = Num("startX", 0.5f);
            _spin = 0f;
            _speed = Num("resetSpeed", 0.3f);
        }

        if (_fuel <= 0f)
        {
            Show(Math.Max(0, Score));
            End(false);
            return;
        }

        for (var i = _traffic.Count - 1; i >= 0; i--)
        {
            var car = _traffic[i];
            car.Y += (_speed - car.Pace) * dt * Num("trafficPace", 0.85f);
            if (car.Kind == 1)
                car.X = Math.Clamp(car.X + MathF.Sin(_scroll * 0.05f + car.Y * 6f) * dt * 0.35f, 0.2f, 0.8f);

            if (MathF.Abs(car.X - _x) < Num("hitX", 0.16f) && MathF.Abs(car.Y - Num("playerY", 0.78f)) < Num("hitY", 0.11f))
            {
                if (car.Kind == 2)
                {
                    _fuel = Math.Min(Num("startFuel", 100f), _fuel + Num("fuelGain", 28f));
                    Score += (int)Num("fuelScore", 100f);
                    Noise("fuel");
                }
                else if (car.Kind == 3)
                {
                    Noise("crash");
                    Show(Score);
                    End(false);
                    return;
                }
                else if (_spin == 0f)
                {
                    _spin = car.X < _x ? -Num("spinForce", 1.1f) : Num("spinForce", 1.1f);
                    _fuel -= Num("crashFuel", 8f);
                    Noise("crash");
                }

                _traffic.RemoveAt(i);
                continue;
            }

            if (car.Y > Num("passY", 1.05f))
            {
                Score += (int)Num("passScore", 50f);
                _traffic.RemoveAt(i);
                continue;
            }

            _traffic[i] = car;
        }

        _spawn -= dt;
        if (_spawn <= 0f)
        {
            var kind = _random.Next(8) switch
            {
                0 => 2,
                1 => 3,
                < 4 => 1,
                _ => 0,
            };
            _traffic.Add(new Traffic(Num("lane0", 0.22f) + _random.Next(3) * Num("laneStep", 0.28f), Num("spawnY", -0.08f), Num("paceMin", 0.15f) + _random.NextSingle() * Num("paceSpan", 0.25f), kind, NextSprite()));
            _spawn = Math.Max(Num("spawnMin", 0.35f), Num("spawnBase", 0.95f) - Score * Num("spawnTighten", 0.0004f));
        }

        var step = (int)Num("milestone", 1000f);
        if (step > 0 && Score / step > _mark)
        {
            _mark = Score / step;
            Noise("pass");
        }

        Show(Score);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelSize.X;
        var height = PixelSize.Y;
        if (Visual.Floor != null)
            TileFloor(handle, Visual.Floor);
        else
            FillBackground(handle, Color.FromHex("#2f6b32"));

        var left = width * Num("roadLeft", 0.12f);
        var right = width * (Num("roadLeft", 0.12f) + Num("roadSpan", 0.76f));
        handle.DrawRect(new UIBox2(left, 0, right, height), Color.FromHex("#4a4a4a"));
        handle.DrawRect(new UIBox2(left, 0, left + 4, height), Color.White);
        handle.DrawRect(new UIBox2(right - 4, 0, right, height), Color.White);
        var dash = (_scroll % 28f) - 28f;
        for (var y = dash; y < height; y += 28f)
            handle.DrawRect(new UIBox2(width * 0.5f - 2, y, width * 0.5f + 2, y + 14), Color.FromHex("#f2d36b"));

        foreach (var car in _traffic)
            DrawCar(handle, car.X, car.Y, car.Kind, car.Sprite, width, height);

        DrawCar(handle, _x, Num("playerY", 0.78f), -1, 0, width, height);
        var bar = Math.Clamp(_fuel / 100f, 0f, 1f);
        handle.DrawRect(new UIBox2(6, 6, 10, height * 0.4f), Color.FromHex("#1b1e2a"));
        handle.DrawRect(new UIBox2(6, 6 + (1f - bar) * height * 0.4f, 10, height * 0.4f), Color.FromHex("#7dffb3"));
    }

    private void DrawCar(DrawingHandleScreen handle, float x, float y, int kind, int sprite, float width, float height)
    {
        Texture tex;
        if (kind < 0)
            tex = Sprite("car");
        else if (kind == 2 && Visual.Sprites.ContainsKey("fuel"))
            tex = Sprite("fuel");
        else if (kind == 3 && Visual.Sprites.ContainsKey("truck"))
            tex = Sprite("truck");
        else if (Visual.Obstacles.Count > 0)
            tex = PdaArcadeArt.Texture(Visual.Obstacles[sprite % Visual.Obstacles.Count]);
        else
            return;

        var size = kind == 3 ? Num("truckSize", 84f) : Num("carSize", 64f);
        var px = width * (Num("roadLeft", 0.12f) + x * Num("roadSpan", 0.76f));
        var py = height * y;
        handle.DrawTextureRect(tex, new UIBox2(px - size * 0.5f, py - size * 0.5f, px + size * 0.5f, py + size * 0.5f));
    }

    private int NextSprite()
    {
        var count = Visual.Obstacles.Count;
        return count == 0 ? 0 : _random.Next(count);
    }

    private struct Traffic
    {
        public float X;
        public float Y;
        public float Pace;
        public int Kind;
        public int Sprite;

        public Traffic(float x, float y, float pace, int kind, int sprite)
        {
            X = x;
            Y = y;
            Pace = pace;
            Kind = kind;
            Sprite = sprite;
        }
    }
}
