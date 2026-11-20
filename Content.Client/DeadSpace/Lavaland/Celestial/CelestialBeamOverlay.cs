using Content.Shared.DeadSpace.Lavaland.Celestial;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using System.Numerics;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client.DeadSpace.Lavaland.Celestial;

/// <summary>
/// Векторная отрисовка атак Селестиала:
/// - лучи: розовый полупрозрачный шнур, затем чёрное ядро с розовой обводкой;
/// - сферы и шары: чёрные круги с розовой обводкой;
/// - Cutter: 8 лучей вращаются вокруг центра и вспыхивают.
/// </summary>
public sealed class CelestialBeamOverlay : Overlay
{
    private static readonly Color Pink = new(0xFF / 255f, 0x52 / 255f, 0x98 / 255f);
    private static readonly Color Black = new(0f, 0f, 0f, 1f);

    private sealed class Beam
    {
        public Vector2 Start;
        public Vector2 End;
        public MapId Map;
        public float PinkTime;
        public float DarkTime;
        public float Elapsed;
        public float WidthScale = 1f;
    }

    private sealed class Cutter
    {
        public Vector2 Center;
        public MapId Map;
        public float BaseAngle;
        public float Length;
        public float RotateTime;
        public float FireTime;
        public float Elapsed;
        // рандомный характер вращения каждого залпа
        public float Seed;
        public float SpinScale;   // общая скорость вращения
        public float JitterAmp;   // резкость рывков
        public float JitterFreq;  // частота рывков
    }

    private readonly List<Beam> _beams = new();
    private readonly List<Cutter> _cutters = new();

    private sealed class Bloom
    {
        public Vector2 Center;
        public MapId Map;
        public float BaseAngle;
        public float Length;
        public float RotateSpeed;
        public float SweepTime;
        public float FireTime;
        public float GapTime;
        public float Elapsed;
    }

    private readonly List<Bloom> _blooms = new();
    private readonly Random _bloomRand = new();

    private readonly IEntityManager _entities;
    private readonly Dictionary<EntityUid, float> _sphereElapsed = new();
    private readonly Dictionary<EntityUid, List<(Vector2 pos, float age)>> _orbTrails = new();
    private readonly Dictionary<EntityUid, float> _orbAge = new();

    private readonly Dictionary<EntityUid, (Vector2 pos, float radius)> _sphereLast = new();
    private readonly List<(Vector2 pos, float radius, float age)> _sphereAura = new();
    private const float TrailLife = 0.9f;
    private const float AuraTime = 0.7f;

    // анимации появления атак
    private const float BeamAimTime = 0.22f;    // сколько луч "выстреливает" от начала к цели
    private const float ImpactFlashTime = 0.12f; // вспышка в момент удара
    private const float CutterSpawnTime = 0.3f;  // лучи резака выходят из центра
    private const float BloomSpawnTime = 0.6f;   // крест увянь-в-цвету раскрывается
    private const float CircleSpawnTime = 0.25f; // кольцо появления кружка
    private const float OrbSpawnTime = 0.2f;     // кольцо появления шара

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CelestialBeamOverlay(IEntityManager entities)
    {
        _entities = entities;
    }

    public void Add(Vector2 start, Vector2 end, MapId map, float pinkTime, float darkTime, float widthScale = 1f)
    {
        _beams.Add(new Beam { Start = start, End = end, Map = map, PinkTime = pinkTime, DarkTime = darkTime, WidthScale = widthScale });
    }

    private readonly Random _cutterRand = new();

    public void AddBloom(Vector2 center, MapId map, float baseAngle, float length,
        float rotateSpeed, float sweepTime, float fireTime, float gapTime)
    {
        _blooms.Add(new Bloom
        {
            Center = center, Map = map, BaseAngle = baseAngle, Length = length,
            RotateSpeed = rotateSpeed, SweepTime = sweepTime, FireTime = fireTime, GapTime = gapTime
        });
    }

    public void AddCutter(Vector2 center, MapId map, float baseAngle, float length, float rotateTime, float fireTime)
    {
        _cutters.Add(new Cutter
        {
            Center = center,
            Map = map,
            BaseAngle = baseAngle,
            Length = length,
            RotateTime = rotateTime,
            FireTime = fireTime,
            Seed = (float) _cutterRand.NextDouble() * 100f,
            SpinScale = 0.8f + (float) _cutterRand.NextDouble() * 0.7f,   // 0.8x - 1.5x скорость
            JitterAmp = 0.08f + (float) _cutterRand.NextDouble() * 0.25f, // резкость рывков
            JitterFreq = 9f + (float) _cutterRand.NextDouble() * 16f,
        });
    }

    public void FrameUpdate(float frameTime)
    {
        for (var i = _blooms.Count - 1; i >= 0; i--)
        {
            _blooms[i].Elapsed += frameTime;
            if (_blooms[i].Elapsed >= _blooms[i].SweepTime + _blooms[i].FireTime + 0.5f)
                _blooms.RemoveAt(i);
        }

        for (var i = _beams.Count - 1; i >= 0; i--)
        {
            var beam = _beams[i];
            beam.Elapsed += frameTime;
            if (beam.Elapsed >= beam.PinkTime + beam.DarkTime + AuraTime)
                _beams.RemoveAt(i);
        }

        for (var i = _cutters.Count - 1; i >= 0; i--)
        {
            var cutter = _cutters[i];
            cutter.Elapsed += frameTime;
            if (cutter.Elapsed >= cutter.RotateTime + cutter.FireTime)
                _cutters.RemoveAt(i);
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        DrawBloom(handle, args.MapId);
        DrawBeams(handle, args.MapId);
        DrawSpheres(handle, args.MapId);
        DrawOrbs(handle, args.MapId, 1f / 60f);
        DrawCircles(handle, args.MapId);
        DrawCutters(handle, args.MapId);
    }

    private void DrawBeams(DrawingHandleWorld handle, MapId map)
    {
        foreach (var beam in _beams)
        {
            if (beam.Map != map)
                continue;

            var dir = beam.End - beam.Start;
            var length = dir.Length();
            if (length < 0.5f)
                continue;
            var angle = new Angle(dir);
            var mid = (beam.Start + beam.End) / 2f;

            var ws = beam.WidthScale;
            if (beam.Elapsed < beam.PinkTime)
            {
                // короткая анимация наведения: шнур выстреливает от начала к цели
                var aimTime = MathF.Min(BeamAimTime, beam.PinkTime * 0.8f);
                var grow = beam.PinkTime >= 0.3f && aimTime > 0.01f
                    ? Math.Clamp(beam.Elapsed / aimTime, 0f, 1f)
                    : 1f;
                var eased = 1f - (1f - grow) * (1f - grow);

                if (eased < 0.999f)
                {
                    // бледный ориентир на всю длину: направление читается сразу
                    DrawBeam(handle, mid, length, 0.45f * ws, angle, Pink.WithAlpha(0.10f));

                    var segLength = MathF.Max(0.6f, length * eased);
                    var tip = Vector2.Lerp(beam.Start, beam.End, eased);
                    var segMid = (beam.Start + tip) / 2f;

                    DrawBeam(handle, segMid, segLength, 1.2f * ws, angle, Pink.WithAlpha(0.5f));
                    DrawBeam(handle, segMid, segLength, 0.5f * ws, angle, Pink.WithAlpha(0.35f));

                    // яркая головка, бегущая к цели
                    handle.DrawCircle(tip, 0.34f * ws, Pink.WithAlpha(0.85f));
                    handle.DrawCircle(tip, 0.15f * ws, new Color(1f, 1f, 1f, 0.9f));

                    // вспышка в точке рождения луча
                    var born = Math.Clamp(1f - grow * 3f, 0f, 1f);
                    if (born > 0f)
                        handle.DrawCircle(beam.Start, 0.9f * ws * (1f - born * 0.5f), Pink.WithAlpha(0.35f * born));
                }
                else
                {
                    // полупрозрачный розовый шнур (фаза наведения)
                    DrawBeam(handle, mid, length, 1.4f * ws, angle, Pink.WithAlpha(0.45f));
                    DrawBeam(handle, mid, length, 0.9f * ws, angle, Pink.WithAlpha(0.5f));
                }
            }
            else if (beam.Elapsed < beam.PinkTime + beam.DarkTime)
            {
                var t = beam.Elapsed - beam.PinkTime;
                var fade = Math.Clamp(1f - t / beam.DarkTime, 0f, 1f);

                // ПРЯМ ЧЁРНОЕ ядро с ТОНКОЙ розовой обводкой
                DrawBeam(handle, mid, length, 1.05f * ws, angle, Pink.WithAlpha(0.9f * fade));
                DrawBeam(handle, mid, length, 0.75f * ws, angle, Black.WithAlpha(fade));

                // короткая вспышка в точке удара
                if (t < ImpactFlashTime)
                {
                    var k = t / ImpactFlashTime;
                    var r = (0.5f + 2.2f * k) * ws;
                    handle.DrawCircle(beam.End, r, Pink.WithAlpha(0.4f * (1f - k)));
                    handle.DrawCircle(beam.End, r * 0.45f, new Color(1f, 1f, 1f, 0.45f * (1f - k)));
                }
            }
            else
            {
                // после атаки остаётся небольшая розовая аура
                var t = beam.Elapsed - beam.PinkTime - beam.DarkTime;
                var fade = Math.Clamp(1f - t / AuraTime, 0f, 1f);
                DrawBeam(handle, mid, length, 1.6f * ws, angle, Pink.WithAlpha(0.18f * fade));
                DrawBeam(handle, mid, length, 0.7f * ws, angle, Pink.WithAlpha(0.28f * fade));
            }
        }
    }

    private void DrawBeam(DrawingHandleWorld handle, Vector2 mid, float length, float width, Angle angle, Color color)
    {
        // тот же путь, что и повороты спрайтов: SetTransform + прямоугольник по центру
        handle.SetTransform(mid, angle);
        handle.DrawRect(new Box2(-length / 2f, -width / 2f, length / 2f, width / 2f), color);
        handle.SetTransform(Vector2.Zero, Angle.Zero);
    }

    private void DrawBloom(DrawingHandleWorld handle, MapId map)
    {
        foreach (var bloom in _blooms)
        {
            if (bloom.Map != map)
                continue;

            var angle = bloom.BaseAngle + bloom.RotateSpeed * bloom.Elapsed;
            var sweeping = bloom.Elapsed < bloom.SweepTime + bloom.GapTime;
            var total = bloom.SweepTime + bloom.FireTime;

            var fadeIn = Math.Clamp(bloom.Elapsed / 0.8f, 0f, 1f);
            var fadeOut = Math.Clamp((total - bloom.Elapsed) / 0.5f, 0f, 1f);
            var alpha = MathF.Min(fadeIn, fadeOut);

            // крест раскрывается из Селестиала
            var spawnGrow = Math.Clamp(bloom.Elapsed / BloomSpawnTime, 0f, 1f);
            var spawnEase = 1f - MathF.Pow(1f - spawnGrow, 2f);
            var drawnLength = MathF.Max(1f, bloom.Length * spawnEase);

            for (var k = 0; k < 4; k++)
            {
                var ang = angle + k * MathF.PI / 2f;
                var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                var mid = bloom.Center + dir * (drawnLength / 2f);
                var beamAngle = new Angle(dir);

                if (sweeping)
                {
                    // розовая полупрозрачная светящаяся стадия
                    DrawBeam(handle, mid, drawnLength, 2.6f, beamAngle, Pink.WithAlpha(0.22f * alpha));
                    DrawBeam(handle, mid, drawnLength, 1.5f, beamAngle, Pink.WithAlpha(0.45f * alpha));
                    DrawBeam(handle, mid, drawnLength, 0.5f, beamAngle, new Color(1f, 1f, 1f, 0.5f * alpha));

                    // ветер по периметру: белёсые штрихи вдоль лучей
                    for (var w = 0; w < 6; w++)
                    {
                        var t = (float)(_bloomRand.NextDouble());
                        var dashPos = bloom.Center + dir * (t * drawnLength);
                        var perp = new Vector2(-dir.Y, dir.X) * (float)(_bloomRand.NextDouble() * 2 - 1) * 1.8f;
                        var dashMid = dashPos + perp;
                        handle.SetTransform(dashMid, beamAngle);
                        handle.DrawRect(new Box2(-1.5f, -0.12f, 1.5f, 0.12f),
                            new Color(1f, 0.85f, 0.95f, 0.3f * alpha));
                        handle.SetTransform(Vector2.Zero, Angle.Zero, Vector2.One);
                    }
                }
                else
                {
                    // чёрное ядро с розовой обводкой
                    DrawBeam(handle, mid, drawnLength, 2.3f, beamAngle, Pink.WithAlpha(0.95f * alpha));
                    DrawBeam(handle, mid, drawnLength, 1.6f, beamAngle, Black.WithAlpha(0.97f * alpha));
                }
            }
        }
    }

    private void DrawSpheres(DrawingHandleWorld handle, MapId map)
    {
        var query = _entities.EntityQueryEnumerator<CelestialSphereComponent, TransformComponent>();
        var alive = new HashSet<EntityUid>();
        while (query.MoveNext(out var uid, out var sphere, out var xform))
        {
            if (xform.MapID != map)
                continue;

            alive.Add(uid);
            var elapsed = _sphereElapsed.TryGetValue(uid, out var e) ? e : 0f;
            elapsed += 1f / 60f;
            _sphereElapsed[uid] = elapsed;

            var t = Math.Clamp(elapsed / sphere.Lifetime, 0f, 1f);
            var radius = MathHelper.Lerp(sphere.StartScale, sphere.EndScale, MathF.Pow(t, sphere.GrowthExponent));
            var pos = xform.WorldPosition;
            _sphereLast[uid] = (pos, radius);

            var fade = Math.Clamp(sphere.Lifetime - elapsed, 0f, 0.3f) / 0.3f;

            // розовая обводка + ПРЯМ ЧЁРНОЕ тело
            handle.DrawCircle(pos, radius, Pink.WithAlpha(0.9f * fade));
            handle.DrawCircle(pos, radius - 0.07f, Black.WithAlpha(0.97f * fade));
        }

        // исчезнувшие сферы оставляют небольшую розовую ауру
        foreach (var uid in _sphereElapsed.Keys.ToList())
        {
            if (alive.Contains(uid))
                continue;

            if (_sphereLast.Remove(uid, out var last))
                _sphereAura.Add((last.pos, last.radius, 0f));
            _sphereElapsed.Remove(uid);
        }

        for (var i = _sphereAura.Count - 1; i >= 0; i--)
        {
            var aura = _sphereAura[i];
            aura.age += 1f / 60f;
            _sphereAura[i] = aura;
            if (aura.age > AuraTime)
            {
                _sphereAura.RemoveAt(i);
                continue;
            }

            var fade = 1f - aura.age / AuraTime;
            handle.DrawCircle(aura.pos, aura.radius, Pink.WithAlpha(0.2f * fade));
            handle.DrawCircle(aura.pos, aura.radius * 0.5f, Pink.WithAlpha(0.15f * fade));
        }
    }

    private void DrawOrbs(DrawingHandleWorld handle, MapId map, float frameTime)
    {
        var query = _entities.EntityQueryEnumerator<CelestialOrbComponent, TransformComponent>();
        var alive = new HashSet<EntityUid>();
        while (query.MoveNext(out var uid, out var orb, out var xform))
        {
            if (xform.MapID != map)
                continue;

            alive.Add(uid);
            var pos = xform.WorldPosition;

            var age = _orbAge.TryGetValue(uid, out var a) ? a + frameTime : 0f;
            _orbAge[uid] = age;

            // след: копим точки, старые гаснем
            if (!_orbTrails.TryGetValue(uid, out var trail))
            {
                trail = new List<(Vector2, float)>();
                _orbTrails[uid] = trail;
            }

            var last = trail.Count > 0 ? trail[^1].Item1 : Vector2.Zero;
            if (trail.Count == 0 || (pos - last).LengthSquared() > 0.0004f)
                trail.Add((pos, 0f));

            for (var i = trail.Count - 1; i >= 0; i--)
            {
                var p = trail[i];
                p.age += frameTime;
                trail[i] = p;
                if (p.age > TrailLife)
                {
                    trail.RemoveAt(i);
                    continue;
                }

                var k = 1f - p.age / TrailLife;
                handle.DrawCircle(p.pos, orb.Radius * (0.35f + 0.5f * k), Pink.WithAlpha(0.35f * k));
            }

            handle.DrawCircle(pos, orb.Radius, Pink.WithAlpha(0.9f));
            handle.DrawCircle(pos, orb.Radius - 0.07f, Black.WithAlpha(0.97f));

            // глитч-появление: расходящееся кольцо
            if (age < OrbSpawnTime)
            {
                var k = age / OrbSpawnTime;
                handle.DrawCircle(pos, orb.Radius * (1f + 2.2f * k),
                    Pink.WithAlpha(0.5f * (1f - k)), filled: false);
            }
        }

        // чистим следы исчезнувших шаров
        foreach (var uid in _orbTrails.Keys.ToList())
        {
            if (!alive.Contains(uid))
            {
                _orbTrails.Remove(uid);
                _orbAge.Remove(uid);
            }
        }
    }

    private readonly Dictionary<EntityUid, float> _circleElapsed = new();

    private void DrawCircles(DrawingHandleWorld handle, MapId map)
    {
        var query = _entities.EntityQueryEnumerator<CelestialCircleComponent, TransformComponent>();
        var alive = new HashSet<EntityUid>();
        while (query.MoveNext(out var uid, out var circle, out var xform))
        {
            if (xform.MapID != map)
                continue;

            alive.Add(uid);
            var elapsed = _circleElapsed.TryGetValue(uid, out var e) ? e : 0f;
            elapsed += 1f / 60f;
            _circleElapsed[uid] = elapsed;

            var pos = xform.WorldPosition;
            var r = circle.Radius;

            if (elapsed < circle.WanderTime)
            {
                // кольцо появления
                if (elapsed < CircleSpawnTime)
                {
                    var grow = elapsed / CircleSpawnTime;
                    handle.DrawCircle(pos, r * (0.35f + 0.9f * grow),
                        Pink.WithAlpha(0.4f * (1f - grow)), filled: false);
                }

                // полупрозрачный розовый кружок
                handle.DrawCircle(pos, r, Pink.WithAlpha(0.3f));
                handle.DrawCircle(pos, r, Pink.WithAlpha(0.5f), filled: false);
            }
            else
            {
                // ЧЁРНЫЙ кружок с ТОНКОЙ розовой обводкой
                handle.DrawCircle(pos, r, Pink.WithAlpha(0.9f));
                handle.DrawCircle(pos, r - 0.09f, Black.WithAlpha(0.97f));
            }
        }

        foreach (var uid in _circleElapsed.Keys.ToList())
        {
            if (!alive.Contains(uid))
                _circleElapsed.Remove(uid);
        }
    }

    private void DrawCutters(DrawingHandleWorld handle, MapId map)
    {
        foreach (var cutter in _cutters)
        {
            if (cutter.Map != map)
                continue;

            var rotating = cutter.Elapsed < cutter.RotateTime;
            var fade = 1f;
            if (!rotating)
                fade = Math.Clamp(1f - (cutter.Elapsed - cutter.RotateTime) / cutter.FireTime, 0f, 1f);

            // лучи выстреливают из центра при появлении
            var spawnGrow = Math.Clamp(cutter.Elapsed / CutterSpawnTime, 0f, 1f);
            var spawnEase = 1f - MathF.Pow(1f - spawnGrow, 2f);
            var drawnLength = MathF.Max(0.5f, cutter.Length * spawnEase);

            if (spawnGrow < 1f)
                handle.DrawCircle(cutter.Center, 0.6f + 2.4f * spawnEase,
                    Pink.WithAlpha(0.35f * (1f - spawnGrow)), filled: false);

            // вращение дёрганое: скорость и резкость рывков случайны у каждого залпа,
            // рывки затухают к моменту вспышки (шаг четверть оборота сохраняет геометрию лучей)
            var progress = Math.Clamp(cutter.Elapsed / cutter.RotateTime, 0f, 1f);
            // плавное вращение: быстро в начале, замедляется к моменту вспышки;
            // степень easing случайна у каждого залпа, в конце всегда ровно 90°
            var eased = 1f - MathF.Pow(1f - progress, 1f + cutter.JitterAmp * 6f);
            var spin = eased * MathF.PI / 2f;

            // 4 линии через центр = 8 лучей, противоположные пары
            for (var k = 0; k < 4; k++)
            {
                var ang = cutter.BaseAngle + k * MathF.PI / 4f + spin;
                var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                var mid = cutter.Center;
                var angle = new Angle(dir);

                if (rotating)
                {
                    // тонкие розовые лучи вращаются
                    DrawBeam(handle, mid, drawnLength, 0.35f, angle, Pink.WithAlpha(0.7f));
                }
                else
                {
                    // вспышка: массивный тёмный луч с розовой обводкой
                    DrawBeam(handle, mid, drawnLength, 1.8f, angle, Pink.WithAlpha(0.95f * fade));
                    DrawBeam(handle, mid, drawnLength, 1.2f, angle, Black.WithAlpha(0.95f * fade));
                }
            }
        }
    }
}
