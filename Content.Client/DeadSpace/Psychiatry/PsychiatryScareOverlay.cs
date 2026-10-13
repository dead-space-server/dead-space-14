// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Psychiatry;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Psychiatry;

public sealed class PsychiatryScareOverlay : Overlay
{
    private readonly IEntityManager _ent;
    private readonly SharedTransformSystem _xform;
    private readonly IGameTiming _timing;
    private readonly IRobustRandom _random;
    private readonly IPrototypeManager _proto;
    private readonly IResourceCache _resources;
    private readonly List<ScareFx> _fx = new();
    private readonly Dictionary<(string Path, string State), Texture?> _frames = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public SchizophreniaStage Stage;
    public int Seed;
    public EntityUid? Subject;

    public float ScareMinSec = 30f;
    public float ScareMaxSec = 120f;

    public Action<SoundSpecifier?>? PlaySound;

    private float _nextScareAt;

    public PsychiatryScareOverlay(
        IEntityManager ent,
        SharedTransformSystem xform,
        IGameTiming timing,
        IRobustRandom random,
        IPrototypeManager proto)
    {
        _ent = ent;
        _xform = xform;
        _timing = timing;
        _random = random;
        _proto = proto;
        _resources = IoCManager.Resolve<IResourceCache>();
        ZIndex = 210;
        _nextScareAt = (float) timing.CurTime.TotalSeconds + random.NextFloat(ScareMinSec, ScareMaxSec);
    }

    public void Configure(EntityUid subject, SchizophreniaComponent schizo)
    {
        Subject = subject;
        Seed = schizo.Seed;
        Stage = schizo.Stage;
    }

    public void Clear()
    {
        _fx.Clear();
        Subject = null;
    }

    public void Tick(float dt)
    {
        if (Subject is not { } subject || !_ent.EntityExists(subject) || Stage < SchizophreniaStage.Acute)
        {
            _fx.Clear();
            return;
        }

        var t = (float) _timing.CurTime.TotalSeconds;
        if (t >= _nextScareAt)
        {
            _nextScareAt = t + _random.NextFloat(ScareMinSec, ScareMaxSec);
            SpawnScare(subject);
        }

        for (var i = _fx.Count - 1; i >= 0; i--)
        {
            var fx = _fx[i];
            fx.Age += dt;
            if (fx.Age >= fx.MaxAge)
                _fx.RemoveAt(i);
            else
                _fx[i] = fx;
        }
    }

    private void SpawnScare(EntityUid subject)
    {
        PsychiatryScarePrototype? picked = null;
        var total = 0;
        foreach (var proto in _proto.EnumeratePrototypes<PsychiatryScarePrototype>())
        {
            if (proto.Weight <= 0)
                continue;
            total += proto.Weight;
            if (picked == null || _random.Next(total) < proto.Weight)
                picked = proto;
        }

        if (picked == null || picked.Sprite is not SpriteSpecifier.Rsi sprite)
            return;

        var origin = _xform.GetWorldPosition(subject);
        var roll = HashCode.Combine(Seed, (int) (_timing.CurTime.TotalSeconds), _fx.Count) & 255;
        var dir = Cardinal((roll >> 2) & 3);
        var lateral = Perpendicular(dir) * (_random.NextFloat(1.5f, 2.5f) * (_random.Prob(0.5f) ? 1f : -1f));
        var from = origin - dir * 8f + lateral;

        _fx.Add(new ScareFx
        {
            Path = sprite.RsiPath.ToString(),
            State = sprite.RsiState,
            FallbackPath = picked.FallbackSprite is SpriteSpecifier.Rsi fallback ? fallback.RsiPath.ToString() : null,
            FallbackState = picked.FallbackSprite is SpriteSpecifier.Rsi fallbackSprite ? fallbackSprite.RsiState : picked.FallbackState,
            Origin = from,
            Velocity = dir * picked.Speed,
            MaxAge = picked.Duration,
            Phase = (roll & 31) / 10f,
            Size = picked.Size,
            Wobble = picked.Wobble,
            Rotate = picked.Rotate,
            Angle = MathF.Abs(dir.Y) > MathF.Abs(dir.X) ? MathF.PI / 2f : 0f,
        });

        PlaySound?.Invoke(picked.Sound);
    }

    private static Vector2 Cardinal(int axis) => axis switch
    {
        0 => new Vector2(1f, 0f),
        1 => new Vector2(-1f, 0f),
        2 => new Vector2(0f, 1f),
        _ => new Vector2(0f, -1f),
    };

    private static Vector2 Perpendicular(Vector2 dir) => new(-dir.Y, dir.X);

    private Texture? Frame(string path, string state)
    {
        var key = (path, state);
        if (_frames.TryGetValue(key, out var cached))
            return cached;

        Texture? tex = null;
        var rooted = path.StartsWith("/Textures/", StringComparison.Ordinal)
            ? path
            : "/Textures/" + path.TrimStart('/');
        if (_resources.TryGetResource<RSIResource>(rooted, out var rsi) && rsi.RSI.TryGetState(state, out var st))
        {
            var frames = st.GetFrames(RsiDirection.South);
            if (frames.Length > 0)
                tex = frames[0];
        }

        _frames[key] = tex;
        return tex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Stage < SchizophreniaStage.Acute || Subject is not { } subject || !_ent.EntityExists(subject))
            return;

        var handle = args.WorldHandle;
        var t = (float) _timing.CurTime.TotalSeconds;

        foreach (var fx in _fx)
        {
            var progress = fx.Age / Math.Max(fx.MaxAge, 0.01f);
            var pos = fx.Origin + fx.Velocity * fx.Age;
            var fade = progress < 0.12f
                ? progress / 0.12f
                : progress > 0.8f
                    ? (1f - progress) / 0.2f
                    : 1f;
            fade = Math.Clamp(fade, 0f, 1f);

            var tex = Frame(fx.Path, fx.State);
            if (tex == null && fx.FallbackPath != null && fx.FallbackState != null)
                tex = Frame(fx.FallbackPath, fx.FallbackState);
            else if (tex == null && fx.FallbackState != null)
                tex = Frame(fx.Path, fx.FallbackState);

            if (tex == null)
                continue;

            var wobble = fx.Wobble
                ? new Vector2(0f, MathF.Sin(t * 3f + fx.Phase) * 0.15f)
                : Vector2.Zero;
            var tint = Color.White.WithAlpha(0.5f + 0.45f * fade);
            var size = fx.Size.X > 0f && fx.Size.Y > 0f ? fx.Size : Vector2.One;
            var angle = fx.Rotate ? fx.Angle : 0f;
            handle.DrawTextureRect(tex, new Box2Rotated(Box2.CenteredAround(pos + wobble, size), angle, pos + wobble), tint);
        }
    }

    private struct ScareFx
    {
        public string Path;
        public string State;
        public string? FallbackPath;
        public string? FallbackState;
        public Vector2 Origin;
        public Vector2 Velocity;
        public float Age;
        public float MaxAge;
        public float Phase;
        public Vector2 Size;
        public bool Wobble;
        public bool Rotate;
        public float Angle;
    }
}
