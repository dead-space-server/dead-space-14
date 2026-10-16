// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Robust.Client.Animations;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Animations;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// The deep night over a chasm: the countdown over the own head, and the shadow rising from the chasm that drags
/// its victim down - a dark pool spreading under him, tendrils winding up, the doll darkening and sinking.
/// </summary>
public sealed class LavalandShadowClientSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;

    private const string GrabKey = "lavaland-shadow-grab";

    private readonly Dictionary<EntityUid, (Vector2 Offset, Vector2 Scale, Color Color)> _original = new();
    private LavalandShadowOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new LavalandShadowOverlay(EntityManager, IoCManager.Resolve<IPlayerManager>(),
            IoCManager.Resolve<IEyeManager>(), IoCManager.Resolve<IGameTiming>(), IoCManager.Resolve<IResourceCache>());
        _overlays.AddOverlay(_overlay);

        SubscribeLocalEvent<LavalandShadowGrabComponent, AfterAutoHandleStateEvent>(OnGrabState);
        SubscribeLocalEvent<LavalandShadowGrabComponent, ComponentRemove>(OnGrabRemove);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<LavalandShadowGrabComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var grab, out var sprite))
        {
            grab.Elapsed += frameTime;
            _spriteTree.QueueTreeUpdate((uid, sprite));
        }
    }

    private void OnGrabState(Entity<LavalandShadowGrabComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_original.ContainsKey(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.Elapsed = 0f;
        _original[ent] = (sprite.Offset, sprite.Scale, sprite.Color);
        _animation.Play(ent.Owner, BuildGrab(ent.Comp, sprite), GrabKey);
    }

    private void OnGrabRemove(Entity<LavalandShadowGrabComponent> ent, ref ComponentRemove args)
    {
        if (_animation.HasRunningAnimation(ent.Owner, GrabKey))
            _animation.Stop(ent.Owner, null, GrabKey);

        if (!_original.Remove(ent, out var original) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        _sprite.SetOffset(spriteEnt, original.Offset);
        _sprite.SetScale(spriteEnt, original.Scale);
        _sprite.SetColor(spriteEnt, original.Color);
    }

    /// <summary>A jerk as it is seized, a struggle, then pulled down into the dark.</summary>
    private static Animation BuildGrab(LavalandShadowGrabComponent grab, SpriteComponent sprite)
    {
        var d = MathF.Max(0.5f, grab.Duration);
        var offset = sprite.Offset;
        var scale = sprite.Scale;
        var dark = Color.FromHex("#14101e");

        var offsetTrack = Track(nameof(SpriteComponent.Offset),
            (offset, 0f),
            (offset + new Vector2(0f, 0.25f), d * 0.08f),
            (offset + new Vector2(-0.08f, -0.05f), d * 0.12f),
            (offset + new Vector2(0.08f, -0.1f), d * 0.1f),
            (offset + new Vector2(-0.06f, -0.15f), d * 0.1f),
            (offset + new Vector2(0f, -0.7f), d * 0.6f));

        var scaleTrack = Track(nameof(SpriteComponent.Scale),
            (scale, 0f),
            (scale * new Vector2(0.92f, 1.1f), d * 0.08f),
            (scale, d * 0.32f),
            (scale * new Vector2(0.55f, 0.35f), d * 0.6f));

        var colorTrack = Track(nameof(SpriteComponent.Color),
            (sprite.Color, 0f),
            (Color.InterpolateBetween(sprite.Color, dark, 0.45f), d * 0.3f),
            (dark.WithAlpha(0.2f), d * 0.7f));

        return new Animation
        {
            Length = TimeSpan.FromSeconds(d + 0.3f),
            AnimationTracks = { offsetTrack, scaleTrack, colorTrack },
        };
    }

    private static AnimationTrackComponentProperty Track(string property, params (object Value, float Delay)[] frames)
    {
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = property,
            InterpolationMode = AnimationInterpolationMode.Linear,
        };

        foreach (var (value, delay) in frames)
        {
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(value, delay));
        }

        return track;
    }
}

/// <summary>Countdown over the own head (screen) and the shadow pool with its tendrils (world).</summary>
public sealed class LavalandShadowOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IEyeManager _eye;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public LavalandShadowOverlay(IEntityManager entities, IPlayerManager player, IEyeManager eye, IGameTiming timing, IResourceCache cache)
    {
        _entities = entities;
        _player = player;
        _eye = eye;
        _timing = timing;
        _transform = entities.System<SharedTransformSystem>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 20);
        ZIndex = 6;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space == OverlaySpace.ScreenSpace)
            DrawCountdown(args.ScreenHandle);
        else
            DrawShadows(args.WorldHandle, args.MapId);
    }

    private void DrawCountdown(DrawingHandleScreen handle)
    {
        if (_player.LocalEntity is not { } local ||
            !_entities.TryGetComponent<LavalandShadowTimerComponent>(local, out var timer))
        {
            return;
        }

        var left = (timer.EndTime - _timing.CurTime).TotalSeconds;
        if (left <= 0)
            return;

        var coords = _entities.GetComponent<TransformComponent>(local).Coordinates.Offset(new Vector2(0f, 1.1f));
        var screen = _eye.CoordinatesToScreen(coords).Position;
        var text = ((int) Math.Ceiling(left)).ToString();

        // Redder and beating faster as the time runs out.
        var urgency = 1f - (float) Math.Clamp(left / 10.0, 0, 1);
        var beat = (MathF.Sin((float) _timing.RealTime.TotalSeconds * (4f + urgency * 10f)) + 1f) / 2f;
        var color = Color.InterpolateBetween(Color.FromHex("#d8d0ff"), Color.FromHex("#ff3030"), urgency)
            .WithAlpha(0.75f + 0.25f * beat);

        var size = handle.GetDimensions(_font, text, 1f);
        var position = screen - size / 2f;
        handle.DrawString(_font, position + new Vector2(2f, 2f), text, Color.Black.WithAlpha(0.7f));
        handle.DrawString(_font, position, text, color);
    }

    private void DrawShadows(DrawingHandleWorld handle, Robust.Shared.Map.MapId map)
    {
        var query = _entities.EntityQueryEnumerator<LavalandShadowGrabComponent, TransformComponent>();
        while (query.MoveNext(out _, out var grab, out var xform))
        {
            if (xform.MapID != map)
                continue;

            var t = Math.Clamp(grab.Elapsed / MathF.Max(0.1f, grab.Duration), 0f, 1f);
            var feet = _transform.GetWorldPosition(xform) + new Vector2(0f, -0.3f);

            // The pool of darkness spreading under him.
            handle.DrawCircle(feet, 0.25f + 0.6f * MathF.Min(1f, t * 2f), Color.FromHex("#05030a").WithAlpha(0.75f));
            handle.DrawCircle(feet, 0.15f + 0.4f * MathF.Min(1f, t * 2f), Color.FromHex("#000000").WithAlpha(0.85f));

            // Tendrils winding up around the body, then pulling down with it.
            const int tendrils = 5;
            var reach = MathF.Sin(MathF.Min(1f, t * 1.6f) * MathF.PI * 0.5f) * 1.1f * (1f - MathF.Max(0f, t - 0.7f) / 0.3f * 0.6f);
            for (var i = 0; i < tendrils; i++)
            {
                var side = (i - (tendrils - 1) / 2f) * 0.16f;
                var root = feet + new Vector2(side * 1.6f, 0f);
                var previous = root;
                const int steps = 6;
                for (var s = 1; s <= steps; s++)
                {
                    var k = s / (float) steps;
                    var wave = MathF.Sin(grab.Elapsed * 9f + i * 1.7f + k * 4f) * 0.12f * k;
                    var point = root + new Vector2(side * (1f - k) * -1.2f + wave, reach * k);
                    var width = 0.07f * (1f - k * 0.7f);
                    var normal = new Vector2(width, 0f);
                    handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip,
                        new[] { previous - normal, previous + normal, point - normal * 0.8f, point + normal * 0.8f },
                        Color.FromHex("#0a0612").WithAlpha(0.9f));
                    previous = point;
                }
            }
        }
    }
}
