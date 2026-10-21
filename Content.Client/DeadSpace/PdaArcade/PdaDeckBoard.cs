// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.PdaArcade;

public sealed class PdaDeckBoard : PdaArcadeBoard
{
    private const int MapSize = 31;

    private static readonly string[] Kinds = { "carp", "tick", "bot" };

    private readonly char[] _cells = new char[MapSize * MapSize];
    private readonly List<Room> _rooms = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Bolt> _bolts = new();
    private readonly List<Prop> _props = new();
    private readonly List<Poster> _posters = new();
    private readonly List<int> _order = new();
    private bool _bloody;
    private readonly Random _random = new();
    private float[] _depth = Array.Empty<float>();
    private float _x = 7.5f;
    private float _y = 14.5f;
    private float _dirX;
    private float _dirY = -1f;
    private float _planeX = 0.66f;
    private float _planeY;
    private float _cool;
    private float _flash;
    private float _spawn = 3.2f;
    private float _pressure;
    private int _kills;
    private bool _left;
    private bool _right;
    private bool _up;
    private bool _down;

    public override void Command(string command, bool down)
    {
        if (command == "left")
            _left = down;
        else if (command == "right")
            _right = down;
        else if (command == "up")
            _up = down;
        else if (command == "down")
            _down = down;
        else if (command == "act" && down)
            Shoot();
    }

    protected override void OnRestart()
    {
        _enemies.Clear();
        _x = 7.5f;
        _y = 14.5f;
        _dirX = 0f;
        _dirY = -1f;
        _planeX = 0.66f;
        _planeY = 0f;
        _cool = 0f;
        _flash = 0f;
        _spawn = 3.2f;
        _pressure = 0f;
        _kills = 0;
        _left = false;
        _right = false;
        _up = false;
        _down = false;
        _bolts.Clear();
        _props.Clear();
        _posters.Clear();
        Build();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var dt = args.DeltaSeconds;
        if (_cool > 0f)
            _cool -= dt;
        if (_flash > 0f)
            _flash -= dt;

        if (Over || IsPaused)
            return;

        if (_left)
            Turn(-2.3f * dt);
        if (_right)
            Turn(2.3f * dt);
        if (_up)
            Slide(_dirX * 2.15f * dt, _dirY * 2.15f * dt);
        if (_down)
            Slide(-_dirX * 1.6f * dt, -_dirY * 1.6f * dt);

        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (!enemy.Alive)
                continue;

            if (enemy.Hurt > 0f)
                enemy.Hurt -= dt;

            Chase(ref enemy, dt);
            if (enemy.Shot > 0f)
                enemy.Shot -= dt;
            else
                TryFire(ref enemy);

            _enemies[i] = enemy;
            if (MathF.Abs(enemy.X - _x) < 0.38f && MathF.Abs(enemy.Y - _y) < 0.38f)
            {
                End(false);
                return;
            }
        }

        StepBolts(dt);
        if (Over)
            return;

        for (var i = 0; i < _props.Count; i++)
        {
            var prop = _props[i];
            if (!prop.Coin || prop.Taken)
                continue;

            if (MathF.Abs(prop.X - _x) > 0.34f || MathF.Abs(prop.Y - _y) > 0.34f)
                continue;

            prop.Taken = true;
            _props[i] = prop;
            Gain(50);
        }

        _pressure += dt;
        _spawn -= dt;
        if (_spawn > 0f)
            return;

        _spawn = MathF.Max(1.25f, 4.6f - _kills * 0.07f - _pressure * 0.035f);
        TrySpawn();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = Math.Max(1, (int)PixelSize.X);
        var height = Math.Max(1, (int)PixelSize.Y);
        if (_depth.Length != width)
            _depth = new float[width];

        var horizon = height * 0.46f;
        handle.DrawRect(new UIBox2(0, 0, width, horizon), Color.FromHex("#12161e"));
        var floor = Visual.Floor == null ? null : PdaArcadeArt.Texture(Visual.Floor);
        if (floor != null)
            CastFloor(handle, floor, width, height, horizon);
        else
            handle.DrawRect(new UIBox2(0, horizon, width, height), Color.FromHex("#3a4048"));

        CastWalls(handle, width, height, horizon);
        CastProps(handle, width, height, horizon);
        CastEnemies(handle, width, height);
        CastBolts(handle, width, height);
        DrawGun(handle, width, height);
    }

    private void Add(float x, float y, int kind, int hp, float speed, bool ranged, float cooldown)
    {
        _enemies.Add(new Enemy
        {
            X = x,
            Y = y,
            Kind = kind,
            Hp = hp,
            Speed = speed,
            Alive = true,
            Ranged = ranged,
            Cooldown = cooldown,
            Shot = ranged ? 0.4f + _random.NextSingle() * 0.8f : 0f,
        });
    }

    private void Build()
    {
        Array.Fill(_cells, '#');
        _rooms.Clear();
        _bloody = _random.Next(3) == 0;
        for (var n = 0; n < 48 && _rooms.Count < 9; n++)
        {
            var w = _random.Next(4, 8);
            var h = _random.Next(4, 8);
            var x = _random.Next(1, MapSize - w - 1);
            var y = _random.Next(1, MapSize - h - 1);
            var blocked = false;
            foreach (var room in _rooms)
            {
                if (x <= room.X + room.W + 1 && x + w + 1 >= room.X && y <= room.Y + room.H + 1 && y + h + 1 >= room.Y)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            for (var iy = y; iy < y + h; iy++)
            for (var ix = x; ix < x + w; ix++)
                _cells[iy * MapSize + ix] = '.';

            _rooms.Add(new Room(x, y, w, h));
        }

        if (_rooms.Count == 0)
        {
            for (var iy = 12; iy < 18; iy++)
            for (var ix = 12; ix < 18; ix++)
                _cells[iy * MapSize + ix] = '.';

            _rooms.Add(new Room(12, 12, 6, 6));
        }

        for (var i = 1; i < _rooms.Count; i++)
            Hall(_rooms[i - 1], _rooms[i]);

        for (var i = 0; i < _rooms.Count; i++)
            Dress(_rooms[i], i != 0);

        var start = _rooms[0];
        _x = start.X + start.W * 0.5f;
        _y = start.Y + start.H * 0.5f;
        for (var i = 1; i < _rooms.Count; i++)
        {
            var room = _rooms[i];
            var count = 1 + _random.Next(3);
            for (var n = 0; n < count; n++)
                Drop(room);
        }
    }

    private void Dress(Room room, bool furniture)
    {
        StickPoster(room.X + _random.Next(room.W), room.Y - 1, 1);
        StickPoster(room.X - 1, room.Y + _random.Next(room.H), 0);
        if (furniture && room.W >= 4 && room.H >= 4)
        {
            PushProp(room.X + 1.5f, room.Y + room.H - 1.5f, "chair", true, false);
            PushProp(room.X + room.W * 0.5f, room.Y + room.H * 0.5f, "table", true, false);
            PushProp(room.X + room.W - 1.5f, room.Y + 1.5f, "plant", true, false);
        }

        var coins = 1 + _random.Next(3);
        for (var n = 0; n < coins; n++)
        {
            var x = room.X + 1 + _random.Next(Math.Max(1, room.W - 2)) + 0.5f;
            var y = room.Y + 1 + _random.Next(Math.Max(1, room.H - 2)) + 0.5f;
            PushProp(x, y, "coin", false, true);
        }
    }

    private void StickPoster(int x, int y, int side)
    {
        if ((uint)x >= MapSize || (uint)y >= MapSize || _cells[y * MapSize + x] == '.')
            return;

        _posters.Add(new Poster { X = x, Y = y, Side = side });
    }

    private void PushProp(float x, float y, string sprite, bool solid, bool coin)
    {
        if (Blocked(x, y, false))
            return;

        _props.Add(new Prop
        {
            X = x,
            Y = y,
            Sprite = sprite,
            Solid = solid,
            Coin = coin,
        });
    }

    private void Hall(Room from, Room to)
    {
        var x = from.X + from.W / 2;
        var y = from.Y + from.H / 2;
        var x1 = to.X + to.W / 2;
        var y1 = to.Y + to.H / 2;
        while (x != x1)
        {
            OpenCell(x, y);
            OpenCell(x, y + 1);
            x += Math.Sign(x1 - x);
        }

        while (y != y1)
        {
            OpenCell(x, y);
            OpenCell(x + 1, y);
            y += Math.Sign(y1 - y);
        }
    }

    private void OpenCell(int x, int y)
    {
        if ((uint)x >= MapSize || (uint)y >= MapSize || x == 0 || y == 0 || x == MapSize - 1 || y == MapSize - 1)
            return;

        _cells[y * MapSize + x] = '.';
    }

    private void Drop(Room room)
    {
        var x = room.X + 1 + _random.Next(Math.Max(1, room.W - 2)) + 0.5f;
        var y = room.Y + 1 + _random.Next(Math.Max(1, room.H - 2)) + 0.5f;
        if (!Blocked(x, y, true) && !HitsProp(x, y))
            AddKind(x, y);
    }

    private void AddKind(float x, float y)
    {
        var kind = _random.Next(Kinds.Length);
        if (kind == 1)
            Add(x, y, kind, 3, 0.48f, true, 0.95f);
        else if (kind == 0)
            Add(x, y, kind, 2, 1.7f, false, 0f);
        else
            Add(x, y, kind, 2, 1.2f, false, 0f);
    }

    private void TrySpawn()
    {
        var alive = 0;
        foreach (var enemy in _enemies)
        {
            if (enemy.Alive)
                alive++;
        }

        if (alive >= 16)
            return;

        if (_enemies.Count > 48)
            _enemies.RemoveAll(enemy => !enemy.Alive);

        for (var n = 0; n < 24; n++)
        {
            var x = _random.Next(1, MapSize - 1) + 0.5f;
            var y = _random.Next(1, MapSize - 1) + 0.5f;
            if (Blocked(x, y, true) || HitsProp(x, y))
                continue;

            var dx = x - _x;
            var dy = y - _y;
            if (dx * dx + dy * dy < 49f || Sees(x, y))
                continue;

            AddKind(x, y);
            return;
        }
    }

    private void TryFire(ref Enemy enemy)
    {
        if (!enemy.Ranged)
            return;

        var dx = _x - enemy.X;
        var dy = _y - enemy.Y;
        var dist2 = dx * dx + dy * dy;
        if (dist2 < 1.4f || dist2 > 64f || !Sees(enemy.X, enemy.Y))
        {
            enemy.Shot = 0.25f;
            return;
        }

        enemy.Shot = enemy.Cooldown;
        var dist = MathF.Sqrt(dist2);
        const float speed = 2.15f;
        _bolts.Add(new Bolt
        {
            X = enemy.X,
            Y = enemy.Y,
            VX = dx / dist * speed,
            VY = dy / dist * speed,
            Life = 3.4f,
        });
        Noise("shoot");
    }

    private void StepBolts(float dt)
    {
        for (var i = _bolts.Count - 1; i >= 0; i--)
        {
            var bolt = _bolts[i];
            var span = MathF.Sqrt(bolt.VX * bolt.VX + bolt.VY * bolt.VY) * dt;
            var steps = Math.Clamp((int)MathF.Ceiling(span / 0.1f), 1, 8);
            var slice = dt / steps;
            var stop = false;
            for (var step = 0; step < steps && !stop; step++)
            {
                bolt.X += bolt.VX * slice;
                bolt.Y += bolt.VY * slice;
                if (Blocked(bolt.X, bolt.Y, true))
                {
                    stop = true;
                    break;
                }

            if (bolt.Friendly)
            {
                var hit = -1;
                for (var n = 0; n < _enemies.Count; n++)
                {
                    var enemy = _enemies[n];
                    if (!enemy.Alive)
                        continue;

                    var ex = enemy.X - bolt.X;
                    var ey = enemy.Y - bolt.Y;
                    if (ex * ex + ey * ey > 0.045f)
                        continue;

                    hit = n;
                    break;
                }

                if (hit >= 0)
                {
                    var enemy = _enemies[hit];
                    enemy.Hp--;
                    enemy.Hurt = 0.12f;
                    if (enemy.Hp <= 0)
                    {
                        enemy.Alive = false;
                        _kills++;
                        Gain(100);
                    }

                    _enemies[hit] = enemy;
                    stop = true;
                    break;
                }
            }
            else
            {
                var px = bolt.X - _x;
                var py = bolt.Y - _y;
                if (px * px + py * py < 0.0144f)
                {
                    End(false);
                    return;
                }
            }
            }

            bolt.Life -= dt;
            if (stop || bolt.Life <= 0f)
            {
                _bolts.RemoveAt(i);
                continue;
            }

            _bolts[i] = bolt;
        }
    }

    private bool Sees(float x, float y)
    {
        var dx = x - _x;
        var dy = y - _y;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        var steps = Math.Max(1, (int)(dist / 0.2f));
        for (var i = 1; i < steps; i++)
        {
            var t = i / (float)steps;
            if (Blocked(_x + dx * t, _y + dy * t, true))
                return false;
        }

        return true;
    }

    private void Shoot()
    {
        if (Over || IsPaused || _cool > 0f)
            return;

        _cool = 0.28f;
        _flash = 0.12f;
        Noise("shoot");
        _bolts.Add(new Bolt
        {
            X = _x + _dirX * 0.35f,
            Y = _y + _dirY * 0.35f,
            VX = _dirX * 11f,
            VY = _dirY * 11f,
            Life = 1.1f,
            Friendly = true,
        });
    }

    private void Chase(ref Enemy enemy, float dt)
    {
        var dx = _x - enemy.X;
        var dy = _y - enemy.Y;
        float ax;
        float ay;
        float bx;
        float by;
        if (MathF.Abs(dx) > MathF.Abs(dy))
        {
            ax = MathF.Sign(dx);
            ay = 0f;
            bx = 0f;
            by = MathF.Sign(dy);
        }
        else
        {
            ax = 0f;
            ay = MathF.Sign(dy);
            bx = MathF.Sign(dx);
            by = 0f;
        }

        var step = enemy.Speed * dt;
        if (!Nudge(ref enemy, ax * step, ay * step) && !Nudge(ref enemy, bx * step, by * step))
            Nudge(ref enemy, -ax * step, -ay * step);
    }

    private bool Nudge(ref Enemy enemy, float dx, float dy)
    {
        if (dx == 0f && dy == 0f)
            return false;

        var x = enemy.X + dx;
        var y = enemy.Y + dy;
        if (!Free(x, y, true) || Crowded(enemy, x, y))
            return false;

        enemy.X = x;
        enemy.Y = y;
        return true;
    }

    private bool Crowded(Enemy self, float x, float y)
    {
        foreach (var other in _enemies)
        {
            if (!other.Alive || other.X == self.X && other.Y == self.Y)
                continue;

            if (MathF.Abs(other.X - x) < 0.45f && MathF.Abs(other.Y - y) < 0.45f)
                return true;
        }

        return false;
    }

    private void Slide(float dx, float dy)
    {
        if (Free(_x + dx, _y, false))
            _x += dx;
        if (Free(_x, _y + dy, false))
            _y += dy;
    }

    private void Turn(float angle)
    {
        var c = MathF.Cos(angle);
        var s = MathF.Sin(angle);
        var dirX = _dirX * c - _dirY * s;
        var dirY = _dirX * s + _dirY * c;
        _dirX = dirX;
        _dirY = dirY;
        var planeX = _planeX * c - _planeY * s;
        var planeY = _planeX * s + _planeY * c;
        _planeX = planeX;
        _planeY = planeY;
    }

    private void CastFloor(DrawingHandleScreen handle, Texture floor, int width, int height, float horizon)
    {
        var span = Math.Min(32, floor.Width);
        var spanH = floor.Height;
        const int step = 5;
        for (var y = (int)horizon; y < height; y += step)
        {
            var row = y - horizon + 1f;
            var dist = height * 0.5f / row;
            var shade = Math.Clamp(1.15f - dist * 0.07f, 0.38f, 1f);
            var tint = new Color(shade, shade, shade, 1f);
            var y1 = Math.Min(height, y + step);
            var x0 = _dirX - _planeX;
            var y0 = _dirY - _planeY;
            var x1 = _dirX + _planeX;
            var y1r = _dirY + _planeY;
            for (var x = 0; x < width; x += step)
            {
                var t = (x + step * 0.5f) / width;
                var wx = _x + dist * (x0 + (x1 - x0) * t);
                var wy = _y + dist * (y0 + (y1r - y0) * t);
                var u = (int)(Frac(wx) * Math.Max(1, span - 8));
                var v = (int)(Frac(wy) * Math.Max(1, spanH - 8));
                var right = Math.Min(width, x + step);
                handle.DrawTextureRectRegion(
                    floor,
                    new UIBox2(x, y, right, y1),
                    new UIBox2(u, v, u + 8, v + 8),
                    tint);
            }
        }
    }

    private void CastWalls(DrawingHandleScreen handle, int width, int height, float horizon)
    {
        for (var x = 0; x < width; x++)
        {
            var camera = 2f * x / width - 1f;
            var rayX = _dirX + _planeX * camera;
            var rayY = _dirY + _planeY * camera;
            var mapX = (int)_x;
            var mapY = (int)_y;
            var deltaX = MathF.Abs(rayX) < 0.0001f ? 1e30f : MathF.Abs(1f / rayX);
            var deltaY = MathF.Abs(rayY) < 0.0001f ? 1e30f : MathF.Abs(1f / rayY);
            int stepX;
            int stepY;
            float sideX;
            float sideY;
            if (rayX < 0f)
            {
                stepX = -1;
                sideX = (_x - mapX) * deltaX;
            }
            else
            {
                stepX = 1;
                sideX = (mapX + 1f - _x) * deltaX;
            }

            if (rayY < 0f)
            {
                stepY = -1;
                sideY = (_y - mapY) * deltaY;
            }
            else
            {
                stepY = 1;
                sideY = (mapY + 1f - _y) * deltaY;
            }

            var side = 0;
            var guard = 0;
            var hit = false;
            while (guard++ < 80)
            {
                if (sideX < sideY)
                {
                    sideX += deltaX;
                    mapX += stepX;
                    side = 0;
                }
                else
                {
                    sideY += deltaY;
                    mapY += stepY;
                    side = 1;
                }

                if (Blocked(mapX, mapY, true))
                {
                    hit = true;
                    break;
                }
            }

            if (!hit)
            {
                _depth[x] = 40f;
                continue;
            }

            var perp = side == 0 ? sideX - deltaX : sideY - deltaY;
            if (perp < 0.05f)
                perp = 0.05f;

            _depth[x] = perp;
            var line = (int)(height / perp);
            var top = (int)(horizon - line * 0.5f);
            var bottom = top + line;
            var surface = Surface(mapX, mapY);
            var tw = surface.Width;
            var th = surface.Height;
            var hitX = side == 0 ? _y + perp * rayY : _x + perp * rayX;
            hitX -= MathF.Floor(hitX);
            var texX = (int)(hitX * tw);
            if (side == 0 && rayX > 0f || side == 1 && rayY < 0f)
                texX = tw - texX - 1;

            texX = Math.Clamp(texX, 0, tw - 1);
            var shade = Math.Clamp(1.25f / (1f + perp * 0.28f), 0.3f, 1f);
            if (side == 1)
                shade *= 0.72f;

            var y0 = Math.Clamp(top, 0, height);
            var y1 = Math.Clamp(bottom, 0, height);
            if (y1 <= y0)
                continue;

            var tint = new Color(shade, shade, shade, 1f);
            handle.DrawTextureRectRegion(
                surface,
                new UIBox2(x, y0, x + 1, y1),
                new UIBox2(texX, 0, texX + 1, th),
                tint);
            PaintPoster(handle, mapX, mapY, side, hitX, x, y0, y1, tint);
        }
    }

    private void CastEnemies(DrawingHandleScreen handle, int width, int height)
    {
        _order.Clear();
        for (var i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i].Alive)
                _order.Add(i);
        }

        _order.Sort((a, b) => Dist2(_enemies[b]).CompareTo(Dist2(_enemies[a])));

        var denom = _planeX * _dirY - _dirX * _planeY;
        if (MathF.Abs(denom) < 0.0001f)
            return;

        var inv = 1f / denom;
        for (var n = 0; n < _order.Count; n++)
        {
            var enemy = _enemies[_order[n]];
            var sprite = Sprite(Kinds[enemy.Kind]);
            var dx = enemy.X - _x;
            var dy = enemy.Y - _y;
            var transformX = inv * (_dirY * dx - _dirX * dy);
            var transformY = inv * (-_planeY * dx + _planeX * dy);
            if (transformY < 0.08f)
                continue;

            var screen = (int)(width / 2f * (1f + transformX / transformY));
            var size = (int)(height / transformY);
            if (size < 2)
                continue;

            var left = screen - size / 2;
            var top = (int)(height * 0.46f) - size / 2;
            var tint = enemy.Hurt > 0f
                ? new Color(1f, 0.45f, 0.45f, 1f)
                : Color.White;
            for (var column = 0; column < size; column++)
            {
                var x = left + column;
                if (x < 0 || x >= width || transformY >= _depth[x])
                    continue;

                var texX = column * sprite.Width / size;
                texX = Math.Clamp(texX, 0, sprite.Width - 1);
                var y0 = Math.Clamp(top, 0, height);
                var y1 = Math.Clamp(top + size, 0, height);
                if (y1 <= y0)
                    continue;

                handle.DrawTextureRectRegion(
                    sprite,
                    new UIBox2(x, y0, x + 1, y1),
                    new UIBox2(texX, 0, texX + 1, sprite.Height),
                    tint);
            }
        }
    }

    private void CastBolts(DrawingHandleScreen handle, int width, int height)
    {
        var denom = _planeX * _dirY - _dirX * _planeY;
        if (MathF.Abs(denom) < 0.0001f)
            return;

        var inv = 1f / denom;
        foreach (var bolt in _bolts)
        {
            var dx = bolt.X - _x;
            var dy = bolt.Y - _y;
            var transformY = inv * (-_planeY * dx + _planeX * dy);
            if (transformY < 0.08f)
                continue;

            var transformX = inv * (_dirY * dx - _dirX * dy);
            var screen = (int)(width / 2f * (1f + transformX / transformY));
            if (screen < 0 || screen >= width || transformY >= _depth[screen])
                continue;

            var grow = bolt.Friendly ? 0.18f : 0.1f;
            var cap = bolt.Friendly ? height * 0.28f : height * 0.16f;
            var size = Math.Clamp(height * grow / transformY, 4f, cap);
            var top = height * 0.46f - size * 0.5f;
            var color = bolt.Friendly ? Color.FromHex("#7ee7ff") : Color.FromHex("#ff5544");
            if (bolt.Friendly)
                handle.DrawLine(new Vector2(width * 0.5f, height * 0.78f), new Vector2(screen, top + size * 0.5f), color);

            handle.DrawRect(new UIBox2(screen - size * 0.5f, top, screen + size * 0.5f, top + size), color);
        }
    }

    private void PaintPoster(DrawingHandleScreen handle, int mapX, int mapY, int side, float along, int screenX, int top, int bottom, Color tint)
    {
        var found = false;
        foreach (var poster in _posters)
        {
            if (poster.X == mapX && poster.Y == mapY && poster.Side == side)
            {
                found = true;
                break;
            }
        }

        if (!found)
            return;

        var posterTex = Sprite("poster");
        var span = (bottom - top) * 0.42f;
        var mid = (top + bottom) * 0.5f;
        var y0 = mid - span * 0.5f;
        var y1 = mid + span * 0.5f;
        var texX = Math.Clamp((int)(along * posterTex.Width), 0, posterTex.Width - 1);
        handle.DrawTextureRectRegion(
            posterTex,
            new UIBox2(screenX, y0, screenX + 1, y1),
            new UIBox2(texX, 0, texX + 1, posterTex.Height),
            tint);
    }

    private void CastProps(DrawingHandleScreen handle, int width, int height, float horizon)
    {
        var denom = _planeX * _dirY - _dirX * _planeY;
        if (MathF.Abs(denom) < 0.0001f)
            return;

        var inv = 1f / denom;
        foreach (var prop in _props)
        {
            if (prop.Taken)
                continue;

            var dx = prop.X - _x;
            var dy = prop.Y - _y;
            var transformY = inv * (-_planeY * dx + _planeX * dy);
            if (transformY < 0.12f)
                continue;

            var transformX = inv * (_dirY * dx - _dirX * dy);
            var screen = width / 2f * (1f + transformX / transformY);
            var column = (int)screen;
            if (column < 0 || column >= width || transformY >= _depth[column])
                continue;

            var sprite = Sprite(prop.Sprite);
            var scale = prop.Coin ? 0.22f : 0.4f;
            var size = height / transformY * scale;
            var floorY = horizon + height * 0.5f / transformY;
            var left = screen - size * 0.5f;
            var top = floorY - size;
            handle.DrawTextureRect(sprite, new UIBox2(left, top, left + size, top + size));
        }
    }

    private void DrawGun(DrawingHandleScreen handle, int width, int height)
    {
        var gun = Sprite("gun");
        var barrel = height * 0.46f;
        var thick = barrel * gun.Height / Math.Max(1, gun.Width);
        var kick = _flash > 0f ? barrel * 0.08f : 0f;
        var center = new Vector2(width * 0.5f, height - barrel * 0.5f + kick);
        handle.SetTransform(center, Angle.FromDegrees(-90f));
        handle.DrawTextureRect(gun, new UIBox2(-barrel * 0.5f, -thick * 0.5f, barrel * 0.5f, thick * 0.5f));
        handle.SetTransform(Vector2.Zero, Angle.Zero);
        if (_flash <= 0f)
            return;

        var muzzle = new Vector2(width * 0.5f, height - barrel + kick);
        handle.DrawLine(muzzle, new Vector2(width * 0.5f, height * 0.4f), Color.FromHex("#ff5544"));
    }

    private float Dist2(Enemy enemy)
    {
        var dx = enemy.X - _x;
        var dy = enemy.Y - _y;
        return dx * dx + dy * dy;
    }

    private bool Free(float x, float y, bool doors)
    {
        const float radius = 0.18f;
        return !Blocked(x - radius, y - radius, doors)
            && !Blocked(x + radius, y - radius, doors)
            && !Blocked(x - radius, y + radius, doors)
            && !Blocked(x + radius, y + radius, doors)
            && !HitsProp(x, y);
    }

    private bool HitsProp(float x, float y)
    {
        foreach (var prop in _props)
        {
            if (!prop.Solid)
                continue;

            if (MathF.Abs(prop.X - x) < 0.32f && MathF.Abs(prop.Y - y) < 0.32f)
                return true;
        }

        return false;
    }

    private bool Blocked(float x, float y, bool doors)
    {
        return Blocked((int)MathF.Floor(x), (int)MathF.Floor(y), doors);
    }

    private bool Blocked(int x, int y, bool doors)
    {
        if ((uint)x >= MapSize || (uint)y >= MapSize)
            return true;

        var cell = _cells[y * MapSize + x];
        if (cell == '.')
            return false;

        if (cell == 'D')
            return doors;

        return true;
    }

    private Texture Surface(int x, int y)
    {
        var cell = (uint)x >= MapSize || (uint)y >= MapSize ? '#' : _cells[y * MapSize + x];
        if (_bloody || cell == 'B')
            return Sprite("meat");

        return Sprite("wall");
    }

    private static float Frac(float value)
    {
        var part = value - MathF.Floor(value);
        return part < 0f ? part + 1f : part;
    }

    private struct Enemy
    {
        public float X;
        public float Y;
        public float Speed;
        public float Hurt;
        public int Hp;
        public int Kind;
        public float Shot;
        public float Cooldown;
        public bool Ranged;
        public bool Alive;
    }

    private struct Bolt
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Life;
        public bool Friendly;
    }

    private struct Poster
    {
        public int X;
        public int Y;
        public int Side;
    }

    private struct Prop
    {
        public float X;
        public float Y;
        public string Sprite;
        public bool Solid;
        public bool Coin;
        public bool Taken;
    }

    private readonly struct Room
    {
        public readonly int X;
        public readonly int Y;
        public readonly int W;
        public readonly int H;

        public Room(int x, int y, int w, int h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }
}
