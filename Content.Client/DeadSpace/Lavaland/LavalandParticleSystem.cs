// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.Chasm;
using Content.Shared.Examine;
using Content.Shared.Tiles;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Small client-only particles: ash and sparks over lava (like in minecraft), petals and motes over the flowers
/// of the chromite caves, and the ambience of the caves themselves - fireflies at night, a milky way of
/// twinkling specks and swarms of fireflies when they glow. Can be turned off with <see cref="CCCCVars.LavalandParticles"/>;
/// the cloud under the feet of a flight potion drinker is always shown, it tells that he walks over chasms.
/// </summary>
public sealed class LavalandParticleSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const int MaxParticles = 600;

    /// <summary>No more than this many new particles a frame, so a burst never stalls a frame.</summary>
    private const int MaxSpawnsPerFrame = 40;

    private const float ScanInterval = 0.25f;

    public struct Particle
    {
        public MapId Map;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public float Phase;
        public float Spin;
        public Color Color;
        public LavalandParticleKind Kind;
    }

    public readonly List<Particle> Particles = new();

    private static readonly Color[] StarColors =
    {
        Color.FromHex("#ffffff"), Color.FromHex("#bfe8ff"), Color.FromHex("#d7c4ff"), Color.FromHex("#8fd8ff"),
    };

    private static readonly Color[] FireflyColors =
    {
        Color.FromHex("#c8ff7a"), Color.FromHex("#8fffd8"), Color.FromHex("#9fd8ff"),
    };

    private readonly List<(EntityUid Uid, LavalandParticleEmitterComponent Comp, Vector2 Position)> _emitters = new();
    private readonly Dictionary<EntityUid, (float Main, float Alt)> _accumulators = new();
    private float _scanTimer;
    private float _ambientStars;
    private float _ambientFireflies;
    private float _ambientMotes;
    private int _spawnsThisFrame;
    private bool _enabled = true;

    /// <summary>Where the player looks from this frame, or null when nothing hides behind walls (ghosts, no FOV).</summary>
    private MapCoordinates? _viewer;

    private const float VisibilityRange = 32f;
    private LavalandParticleOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LavalandParticleOverlay(this);
        _overlays.AddOverlay(_overlay);
        Subs.CVar(_cfg, CCCCVars.LavalandParticles, OnToggled, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        Particles.Clear();
    }

    private void OnToggled(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
            return;

        Particles.RemoveAll(p => p.Kind != LavalandParticleKind.Cloud);
        _emitters.Clear();
        _accumulators.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eye = _eye.CurrentEye.Position;
        if (eye.MapId == MapId.Nullspace)
        {
            Particles.Clear();
            return;
        }

        _spawnsThisFrame = 0;
        var view = _eye.GetWorldViewport().Enlarged(1.5f);
        _viewer = _eye.CurrentEye.DrawFov && _player.LocalEntity is { } local
            ? _transform.GetMapCoordinates(local)
            : null;

        if (_enabled)
        {
            var caves = CompOrNull<LavalandChromiteCavesComponent>(_map.GetMapOrInvalid(eye.MapId));
            var glowing = caves?.Mode == LavalandCavesMode.Glow;

            _scanTimer -= frameTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = ScanInterval;
                ScanEmitters(eye.MapId, view);
            }

            EmitFromEmitters(eye.MapId, glowing, frameTime);
            if (caves != null)
                UpdateCavesAmbience(eye.MapId, view, caves.Mode, frameTime);
        }

        EmitClouds(eye.MapId, view, frameTime);
        Step(eye.MapId, frameTime);
    }

    /// <summary>Only emitters on screen, looked up a few times a second instead of every frame.</summary>
    private void ScanEmitters(MapId map, Box2 view)
    {
        _emitters.Clear();
        var query = EntityQueryEnumerator<LavalandParticleEmitterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != map)
                continue;

            var position = _transform.GetWorldPosition(xform);
            // Lava behind a wall does not sparkle through it.
            if (view.Contains(position) && IsVisible(map, position))
                _emitters.Add((uid, comp, position));
        }

        if (_accumulators.Count > 2048)
            _accumulators.Clear();
    }

    private void EmitFromEmitters(MapId map, bool glowing, float frameTime)
    {
        foreach (var (uid, comp, position) in _emitters)
        {
            var (main, alt) = _accumulators.GetValueOrDefault(uid);
            main = Math.Min(main + comp.Rate * (glowing ? comp.GlowBoost : 1f) * frameTime, 2f);
            while (main >= 1f && CanSpawn())
            {
                main -= 1f;
                Emit(map, position, comp, comp.Kind, comp.Colors);
            }

            if (comp.AltKind is { } altKind && comp.AltRate > 0f)
            {
                alt = Math.Min(alt + comp.AltRate * frameTime, 2f);
                while (alt >= 1f && CanSpawn())
                {
                    alt -= 1f;
                    Emit(map, position, comp, altKind, comp.AltColors);
                }
            }

            _accumulators[uid] = (main, alt);
        }
    }

    private bool CanSpawn()
    {
        return Particles.Count < MaxParticles && _spawnsThisFrame < MaxSpawnsPerFrame;
    }

    private void Add(Particle particle)
    {
        _spawnsThisFrame++;
        Particles.Add(particle);
    }

    private void Emit(MapId map, Vector2 origin, LavalandParticleEmitterComponent comp, LavalandParticleKind kind, List<Color> colors)
    {
        var spread = comp.Spread;
        var position = origin + new Vector2(_random.NextFloat(-spread, spread), _random.NextFloat(-spread, spread));
        var speed = comp.Speed;
        var velocity = kind switch
        {
            LavalandParticleKind.Ember => new Vector2(_random.NextFloat(-0.15f, 0.15f), speed * _random.NextFloat(0.6f, 1.2f)),
            LavalandParticleKind.Ash => new Vector2(_random.NextFloat(-0.25f, 0.25f), speed * _random.NextFloat(0.4f, 0.8f)),
            LavalandParticleKind.Petal => new Vector2(_random.NextFloat(-0.9f, 0.9f), speed * _random.NextFloat(0.8f, 1.4f)),
            LavalandParticleKind.Firefly => _random.NextAngle().ToVec() * speed * 0.5f,
            LavalandParticleKind.Star => _random.NextAngle().ToVec() * 0.03f,
            _ => new Vector2(_random.NextFloat(-0.06f, 0.06f), speed * _random.NextFloat(0.5f, 1f)),
        };

        Add(new Particle
        {
            Map = map,
            Position = position,
            Velocity = velocity,
            Life = _random.NextFloat(comp.LifetimeMin, MathF.Max(comp.LifetimeMin, comp.LifetimeMax)),
            Size = _random.NextFloat(comp.SizeMin, MathF.Max(comp.SizeMin, comp.SizeMax)),
            Phase = _random.NextFloat(0f, MathF.Tau),
            Spin = _random.NextFloat(-6f, 6f),
            Color = colors.Count == 0 ? Color.White : _random.Pick(colors),
            Kind = kind,
        });
    }

    /// <summary>A small white cloud puffing under the feet of everyone carried by the flight potion.</summary>
    private void EmitClouds(MapId map, Box2 view, float frameTime)
    {
        var query = EntityQueryEnumerator<LavalandCloudWalkerComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != map)
                continue;

            var feet = _transform.GetWorldPosition(xform) + new Vector2(0f, -0.38f);
            if (!view.Contains(feet) || !_random.Prob(Math.Clamp(14f * frameTime, 0f, 1f)) ||
                Particles.Count >= MaxParticles + 64 || !IsOverAbyss(xform) || !IsVisible(map, feet))
            {
                continue;
            }

            // Shown even with particles off: it tells that chasms don't take him.
            Particles.Add(new Particle
            {
                Map = map,
                Position = feet + new Vector2(_random.NextFloat(-0.28f, 0.28f), _random.NextFloat(-0.06f, 0.06f)),
                Velocity = new Vector2(_random.NextFloat(-0.25f, 0.25f), _random.NextFloat(-0.08f, 0.04f)),
                Life = _random.NextFloat(0.6f, 1.1f),
                Size = _random.NextFloat(0.1f, 0.17f),
                Phase = _random.NextFloat(0f, MathF.Tau),
                Color = Color.White.WithAlpha(0.85f),
                Kind = LavalandParticleKind.Cloud,
            });
        }
    }

    /// <summary>
    /// Night - a few fireflies; glow - a band of twinkling specks across the screen like the milky way and swarms
    /// of fireflies; deep night - only rare faint motes.
    /// </summary>
    private void UpdateCavesAmbience(MapId map, Box2 view, LavalandCavesMode mode, float frameTime)
    {
        var (stars, fireflies, motes) = mode switch
        {
            LavalandCavesMode.Glow => (28f, 3f, 2f),
            LavalandCavesMode.Night => (0f, 0.8f, 0.6f),
            _ => (0f, 0f, 0.3f),
        };

        _ambientStars = Math.Min(_ambientStars + stars * frameTime, 4f);
        _ambientFireflies = Math.Min(_ambientFireflies + fireflies * frameTime, 2f);
        _ambientMotes = Math.Min(_ambientMotes + motes * frameTime, 2f);

        // The band crosses the screen diagonally and creeps slowly along.
        var center = view.Center;
        var axis = Vector2.Normalize(new Vector2(1f, 0.38f));
        var normal = new Vector2(-axis.Y, axis.X);
        var reach = view.Size.Length() * 0.55f;
        while (_ambientStars >= 1f && CanSpawn())
        {
            _ambientStars -= 1f;
            // Sum of randoms: dense in the middle of the band, thin at its edges.
            var across = (_random.NextFloat(-1f, 1f) + _random.NextFloat(-1f, 1f) + _random.NextFloat(-1f, 1f)) * 1.6f;
            var along = _random.NextFloat(-reach, reach);
            var wave = MathF.Sin(along * 0.25f) * 1.2f;
            var starAt = center + axis * along + normal * (across + wave);
            if (!IsVisible(map, starAt))
                continue;

            Add(new Particle
            {
                Map = map,
                Position = starAt,
                Velocity = axis * 0.06f,
                Life = _random.NextFloat(2.5f, 6f),
                Size = _random.NextFloat(0.03f, MathF.Abs(across) < 1f ? 0.08f : 0.05f),
                Phase = _random.NextFloat(0f, MathF.Tau),
                Color = _random.Pick(StarColors),
                Kind = LavalandParticleKind.Star,
            });
        }

        while (_ambientFireflies >= 1f && CanSpawn())
        {
            _ambientFireflies -= 1f;
            var fireflyAt = RandomIn(view);
            if (!IsVisible(map, fireflyAt))
                continue;

            Add(new Particle
            {
                Map = map,
                Position = fireflyAt,
                Velocity = _random.NextAngle().ToVec() * 0.3f,
                Life = _random.NextFloat(4f, 8f),
                Size = _random.NextFloat(0.05f, 0.08f),
                Phase = _random.NextFloat(0f, MathF.Tau),
                Color = _random.Pick(FireflyColors),
                Kind = LavalandParticleKind.Firefly,
            });
        }

        while (_ambientMotes >= 1f && CanSpawn())
        {
            _ambientMotes -= 1f;
            var moteAt = RandomIn(view);
            if (!IsVisible(map, moteAt))
                continue;

            Add(new Particle
            {
                Map = map,
                Position = moteAt,
                Velocity = new Vector2(_random.NextFloat(-0.05f, 0.05f), _random.NextFloat(0.08f, 0.2f)),
                Life = _random.NextFloat(3f, 6f),
                Size = _random.NextFloat(0.04f, 0.06f),
                Phase = _random.NextFloat(0f, MathF.Tau),
                Color = mode == LavalandCavesMode.DeepDark ? Color.FromHex("#5f7fc8") : Color.FromHex("#9fd8ff"),
                Kind = LavalandParticleKind.Mote,
            });
        }
    }

    /// <summary>
    /// Nothing solid under the feet: a chasm, lava, water or open space. Only there the cloud gathers under him.
    /// </summary>
    private bool IsOverAbyss(TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return true;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<ChasmComponent>(uid) || HasComp<LavaComponent>(uid) ||
                MetaData(uid.Value).EntityPrototype?.ID.Contains("Water", StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The spot can be seen by the player: nothing that blocks sight stands between them.</summary>
    private bool IsVisible(MapId map, Vector2 position)
    {
        if (_viewer is not { } viewer || viewer.MapId != map)
            return true;

        return _examine.InRangeUnOccluded(viewer, new MapCoordinates(position, map), VisibilityRange, null);
    }

    private Vector2 RandomIn(Box2 box)
    {
        return new Vector2(_random.NextFloat(box.Left, box.Right), _random.NextFloat(box.Bottom, box.Top));
    }

    private void Step(MapId map, float frameTime)
    {
        var particles = CollectionsMarshal.AsSpan(Particles);
        var count = particles.Length;
        for (var i = count - 1; i >= 0; i--)
        {
            ref var p = ref particles[i];
            p.Age += frameTime;
            if (p.Age >= p.Life || p.Map != map)
            {
                // Swap with the last live one instead of shifting the whole list.
                count--;
                particles[i] = particles[count];
                continue;
            }

            switch (p.Kind)
            {
                case LavalandParticleKind.Ember:
                    // Rising and slowing, flicking side to side.
                    p.Velocity *= 1f - 0.6f * frameTime;
                    p.Position += p.Velocity * frameTime + new Vector2(MathF.Sin(p.Age * 9f + p.Phase) * 0.15f * frameTime, 0f);
                    break;

                case LavalandParticleKind.Ash:
                    p.Position += p.Velocity * frameTime + new Vector2(MathF.Sin(p.Age * 2f + p.Phase) * 0.3f * frameTime, 0f);
                    break;

                case LavalandParticleKind.Petal:
                    // Thrown up, then fluttering down in a sway.
                    p.Velocity.Y = MathF.Max(p.Velocity.Y - 1.6f * frameTime, -0.35f);
                    p.Velocity.X *= 1f - 1.2f * frameTime;
                    p.Position += p.Velocity * frameTime + new Vector2(MathF.Sin(p.Age * 3f + p.Phase) * 0.5f * frameTime, 0f);
                    break;

                case LavalandParticleKind.Firefly:
                    // Wanders by turning a little every frame, no random calls per particle.
                    var (sin, cos) = MathF.SinCos(MathF.Sin(p.Age * 1.7f + p.Phase) * 2.2f * frameTime);
                    p.Velocity = new Vector2(p.Velocity.X * cos - p.Velocity.Y * sin, p.Velocity.X * sin + p.Velocity.Y * cos);
                    p.Position += p.Velocity * frameTime;
                    break;

                case LavalandParticleKind.Cloud:
                    // Puffs swell and drift apart.
                    p.Size += 0.12f * frameTime;
                    p.Position += p.Velocity * frameTime;
                    break;

                default:
                    p.Position += p.Velocity * frameTime + new Vector2(MathF.Sin(p.Age * 1.3f + p.Phase) * 0.08f * frameTime, 0f);
                    break;
            }
        }

        if (count < Particles.Count)
            Particles.RemoveRange(count, Particles.Count - count);
    }

    /// <summary>Opacity of a particle right now: fades in and out, flickers, blinks or twinkles by kind.</summary>
    public static float Alpha(in Particle p)
    {
        var t = p.Age / p.Life;
        var fade = MathF.Min(1f, t / 0.15f) * MathF.Min(1f, (1f - t) / 0.3f);
        return p.Kind switch
        {
            LavalandParticleKind.Ember => fade * (0.65f + 0.35f * MathF.Sin(p.Age * 22f + p.Phase)),
            LavalandParticleKind.Ash => fade * 0.45f,
            LavalandParticleKind.Firefly => fade * MathF.Max(0f, MathF.Sin(p.Age * 2.4f + p.Phase)),
            LavalandParticleKind.Star => fade * (0.55f + 0.45f * MathF.Sin(p.Age * 5f + p.Phase)),
            LavalandParticleKind.Mote => fade * (0.6f + 0.4f * MathF.Sin(p.Age * 3f + p.Phase)),
            LavalandParticleKind.Cloud => fade * p.Color.A,
            _ => fade,
        };
    }
}
