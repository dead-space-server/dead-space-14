// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared.DeadSpace.Storyteller;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Storyteller;

public sealed class StoryStageControl : Control
{
    private const float LieTime = 0.35f;
    private const float FlashTime = 0.3f;

    public bool Editing;
    public string? Brush;
    public ushort? SelectedProp;
    public event Action<int, int>? OnPlace;
    public event Action<ushort, int, int>? OnMoveProp;
    public event Action<ushort>? OnRemoveProp;
    public event Action<ushort?>? OnSelectProp;

    private readonly IEntityManager _entities;
    private readonly IPrototypeManager _prototypes;
    private readonly IResourceCache _cache;
    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _transform;
    private readonly SharedAudioSystem _audio;
    private readonly StorytellerSystem _story;
    private readonly Font _bubbleFont;
    private readonly Font _nameFont;
    private readonly Font _captionFont;
    private readonly Font _titleFont;
    private readonly Dictionary<byte, Figure> _figures = new();
    private readonly List<Effect> _effects = new();

    private StoryLocationPrototype? _location;
    private Texture? _floor;
    private int _floorVariants = 1;
    private Texture? _parallax;
    private SoundSpecifier? _footsteps;
    private float _time;
    private float _zoom = 1f;
    private float _shownZoom = 1f;
    private byte _focus;
    private Vector2 _shownFocus = Vector2.Zero;
    private float _shake;
    private float _nextStep;
    private string _caption = "";
    private float _captionAt;
    private float _locationAt = -10f;
    private string _light = "";
    private string _tint = "";
    private bool _loggedError;
    private StoryPropState[] _props = Array.Empty<StoryPropState>();
    private Vector2i? _hover;
    private ushort? _dragging;

    private StorySettingsPrototype Settings => StoryStage.Settings(_prototypes);

    private Vector2 Middle => new(Settings.Width / 2f, Settings.Height / 2f);

    public StoryStageControl(IEntityManager entities, IPrototypeManager prototypes, StorytellerSystem story)
    {
        _entities = entities;
        _prototypes = prototypes;
        _story = story;
        _cache = IoCManager.Resolve<IResourceCache>();
        _sprites = entities.System<SpriteSystem>();
        _transform = entities.System<SharedTransformSystem>();
        _audio = entities.System<SharedAudioSystem>();
        _bubbleFont = _cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 12);
        _nameFont = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 10);
        _captionFont = _cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 15);
        _titleFont = _cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 22);
        MinSize = new Vector2(900, 520);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Stop;
    }

    public void SetScene(StorySceneEvent scene)
    {
        if (_location?.ID != scene.Location && _prototypes.TryIndex<StoryLocationPrototype>(scene.Location, out var location))
            SetLocation(location);

        _light = scene.Light ?? "";
        _tint = scene.Tint ?? "";
        _props = scene.Props ?? Array.Empty<StoryPropState>();
        if (SelectedProp is { } selected && _props.All(p => p.Id != selected))
        {
            SelectedProp = null;
            OnSelectProp?.Invoke(null);
        }
        _zoom = Math.Clamp(scene.Zoom, 1f, Settings.MaxZoom);
        _focus = scene.Focus;
        if (scene.Caption != _caption)
        {
            _caption = scene.Caption;
            _captionAt = _time;
        }

        var seen = new HashSet<byte>();
        foreach (var actor in scene.Actors)
        {
            seen.Add(actor.Id);
            var target = new Vector2(actor.X, actor.Y);
            var entering = scene.Effects.FirstOrDefault(e => e.Kind == StoryEffectKind.Enter && e.Actor == actor.Id);
            var entered = scene.Effects.Any(e => e.Kind == StoryEffectKind.Enter && e.Actor == actor.Id);
            if (!_figures.TryGetValue(actor.Id, out var figure))
            {
                figure = new Figure { Shown = target, Target = target, Alpha = entered ? 1f : 0f, Pose = actor.Pose, PreviousPose = actor.Pose };
                _figures[actor.Id] = figure;
            }

            if (entered && _location != null)
            {
                figure.Shown = StoryStage.Door(Settings, _location);
                figure.Alpha = 1f;
                figure.HoldUntil = _time + Math.Clamp(entering.Delay, 0f, 10f);
            }

            if (figure.Pose != actor.Pose)
            {
                figure.PreviousPose = figure.Pose;
                figure.PoseAt = _time;
            }

            figure.Actor = actor;
            figure.Target = target;
            figure.Pose = actor.Pose;
        }

        foreach (var id in _figures.Keys.ToArray())
        {
            if (!seen.Contains(id))
                _figures.Remove(id);
        }

        foreach (var effect in scene.Effects)
        {
            if (_effects.Count >= 64)
                break;

            _effects.Add(new Effect(effect, _time + Math.Clamp(effect.Delay, 0f, 10f)));
        }
    }

    private void SetLocation(StoryLocationPrototype location)
    {
        _location = location;
        _locationAt = _time;
        _floor = null;
        _floorVariants = 1;
        _footsteps = null;
        if (location.Floor is { } floorId && _prototypes.TryIndex(floorId, out ContentTileDefinition? tile) && tile.Sprite is { } path &&
            _cache.TryGetResource<TextureResource>(path, out var floor))
        {
            _floor = floor.Texture;
            _floorVariants = Math.Max(1, (int) tile.Variants);
            _footsteps = tile.FootstepSounds;
        }

        _parallax = location.Parallax is { } parallax && _cache.TryGetResource<TextureResource>(parallax, out var texture)
            ? texture.Texture
            : null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var dt = args.DeltaSeconds;
        _time += dt;
        _shownZoom += (_zoom - _shownZoom) * MathF.Min(1f, dt * 4f);
        _shake = MathF.Max(0f, _shake - dt);

        var anyMoving = false;
        foreach (var figure in _figures.Values)
        {
            if (figure.Actor == null)
                continue;

            if (figure.HoldUntil > _time)
                continue;

            var delta = figure.Target - figure.Shown;
            var distance = delta.Length();
            var speed = figure.Actor.Running ? Settings.RunSpeed : Settings.WalkSpeed;
            if (distance > 0.01f)
            {
                var step = MathF.Min(distance, speed * dt);
                figure.Shown += delta / distance * step;
                figure.Moving = true;
                figure.MoveDir = Math.Abs(delta.X) >= Math.Abs(delta.Y)
                    ? delta.X >= 0f ? Direction.East : Direction.West
                    : delta.Y >= 0f ? Direction.South : Direction.North;
                anyMoving = true;
            }
            else
            {
                figure.Shown = figure.Target;
                figure.Moving = false;
            }

            if (figure.Pose == StoryPose.Gone && !figure.Moving)
                figure.Alpha = MathF.Max(0f, figure.Alpha - dt / Settings.FadeTime);
            else
                figure.Alpha = MathF.Min(1f, figure.Alpha + dt * 3f);
        }

        if (anyMoving && _footsteps != null && _time >= _nextStep)
        {
            _nextStep = _time + 0.32f;
            Play(_footsteps, Settings.FootstepVolume);
        }

        foreach (var effect in _effects)
        {
            if (effect.Started || _time < effect.Start)
                continue;

            effect.Started = true;
            StartEffect(effect);
        }

        _effects.RemoveAll(e => e.Started && _time > e.Start + e.Duration);

        if (_focus != 0 && _figures.TryGetValue(_focus, out var focused))
            _shownFocus = Vector2.Lerp(_shownFocus, focused.Shown, MathF.Min(1f, dt * 4f));
        else
            _shownFocus = Vector2.Lerp(_shownFocus, Middle, MathF.Min(1f, dt * 4f));
    }

    private void StartEffect(Effect effect)
    {
        var source = Find(effect.Data.Actor);
        var target = Find(effect.Data.Target);
        switch (effect.Data.Kind)
        {
            case StoryEffectKind.Say:
                effect.Duration = 2.5f + effect.Data.Text.Length * 0.06f;
                break;
            case StoryEffectKind.Emote:
                effect.Duration = 2.6f;
                break;
            case StoryEffectKind.Hit:
                effect.Duration = 0.45f;
                if (target != null)
                    target.FlashAt = _time;
                if (source != null)
                    source.LungeAt = _time;
                Play(Use(effect.Data.Item) == StoryItemUse.Melee ? Settings.SmashSound : Settings.PunchSound);
                break;
            case StoryEffectKind.Shoot:
                effect.Duration = 0.4f;
                if (target != null)
                    target.FlashAt = _time + 0.08f;
                Play(Use(effect.Data.Item) == StoryItemUse.Laser ? Settings.LaserSound : Settings.BulletSound);
                break;
            case StoryEffectKind.Hug:
            case StoryEffectKind.Kiss:
                effect.Duration = 1.6f;
                Play(Settings.HugSound);
                break;
            case StoryEffectKind.Give:
                effect.Duration = 0.5f;
                Play(Settings.GiveSound);
                break;
            case StoryEffectKind.Point:
                effect.Duration = 2.2f;
                break;
            case StoryEffectKind.Dance:
                effect.Duration = 2f;
                if (source != null)
                    source.DanceUntil = _time + 2f;
                break;
            case StoryEffectKind.Fall:
                effect.Duration = 0.1f;
                Play(Settings.FallSound);
                break;
            case StoryEffectKind.Enter:
                effect.Duration = 0.1f;
                Play(Settings.DoorSound);
                break;
            case StoryEffectKind.Shake:
                effect.Duration = 0.1f;
                _shake = 0.5f;
                Play(Settings.ShakeSound);
                break;
        }
    }

    private void Play(SoundSpecifier? sound, float? volume = null)
    {
        if (sound == null)
            return;

        try
        {
            var audio = volume is { } level ? sound.Params.WithVolume(level) : sound.Params;
            _audio.PlayGlobal(sound, Filter.Local(), false, audio);
        }
        catch (Exception)
        {
        }
    }

    private StoryItemUse Use(string item)
    {
        return _prototypes.TryIndex<StoryItemPrototype>(item, out var proto) ? proto.Use : StoryItemUse.None;
    }

    private Figure? Find(byte id)
    {
        return id != 0 && _figures.TryGetValue(id, out var figure) ? figure : null;
    }

    #region Drawing

    private float Tile => MathF.Min(PixelSize.X / (float) Settings.Width, PixelSize.Y / (float) Settings.Height) * _shownZoom;

    private Vector2 Screen(Vector2 world)
    {
        var center = Vector2.Lerp(Middle, _shownFocus,
            Math.Clamp((_shownZoom - 1f) / MathF.Max(0.01f, Settings.MaxZoom - 1f), 0f, 1f));
        var jolt = Vector2.Zero;
        if (_shake > 0f)
            jolt = new Vector2(MathF.Sin(_time * 80f), MathF.Cos(_time * 63f)) * (_shake / 0.5f) * 6f * UIScale;

        return (world - center) * Tile + (Vector2) PixelSize / 2f + jolt;
    }

    protected override void Draw(IRenderHandle renderHandle)
    {
        var handle = renderHandle.DrawingHandleScreen;
        handle.DrawRect(PixelSizeBox, Color.Black);
        if (_location == null)
            return;

        var tile = Tile;
        Safe(() => DrawBackground(renderHandle, tile));

        var drawables = new List<(float Y, int Order, Action Draw)>();
        var index = 0;
        foreach (var prop in _props)
        {
            var copy = prop;
            var order = index++;
            var x = prop.X;
            var y = prop.Y;
            if (_dragging == prop.Id && _hover is { } hover)
            {
                x = hover.X;
                y = hover.Y;
            }

            drawables.Add((y + (y < 1 ? -10f : 0.45f), order, () => DrawStageProp(renderHandle, copy, x, y, tile)));
        }

        foreach (var (id, figure) in _figures)
        {
            var copyId = id;
            drawables.Add((figure.Shown.Y, 1000 + id, () => DrawFigure(renderHandle, copyId, figure, tile)));
        }

        foreach (var drawable in drawables.OrderBy(d => d.Y).ThenBy(d => d.Order))
            Safe(drawable.Draw);

        Safe(() => DrawEffects(handle, tile));
        Safe(() => DrawLighting(handle));
        if (Editing)
            Safe(() => DrawEditor(handle, tile));
        Safe(() => DrawBubbles(handle, tile));
        Safe(() => DrawOverlay(handle));
    }

    private void DrawEditor(DrawingHandleScreen handle, float tile)
    {
        var settings = Settings;
        var grid = new Color(1f, 1f, 1f, 0.08f);
        for (var x = 0; x <= settings.Width; x++)
            handle.DrawLine(Screen(new Vector2(x, 0)), Screen(new Vector2(x, settings.Height)), grid);
        for (var y = 0; y <= settings.Height; y++)
            handle.DrawLine(Screen(new Vector2(0, y)), Screen(new Vector2(settings.Width, y)), grid);

        if (SelectedProp is { } selected && _props.FirstOrDefault(p => p.Id == selected) is { Proto: not null } prop)
        {
            var at = _dragging == selected && _hover is { } moved ? moved : new Vector2i(prop.X, prop.Y);
            handle.DrawRect(TileBox(at.X, at.Y, tile), new Color(1f, 0.85f, 0.2f, 0.9f), false);
        }

        if (_hover is not { } hover)
            return;

        handle.DrawRect(TileBox(hover.X, hover.Y, tile), new Color(0.4f, 0.8f, 1f, 0.6f), false);
        if (_dragging == null && Brush != null && PropAt(hover) == null && _prototypes.HasIndex<EntityPrototype>(Brush))
            DrawProp(handle, Brush, hover.X, hover.Y, Direction.South, tile, Color.White.WithAlpha(0.55f));
    }

    private Vector2i? TileAt(Vector2 pixel)
    {
        var center = Vector2.Lerp(Middle, _shownFocus, Math.Clamp((_shownZoom - 1f) / MathF.Max(0.01f, Settings.MaxZoom - 1f), 0f, 1f));
        var world = (pixel - (Vector2) PixelSize / 2f) / Tile + center;
        var x = (int) MathF.Floor(world.X);
        var y = (int) MathF.Floor(world.Y);
        if (x < 0 || y < 0 || x >= Settings.Width || y >= Settings.Height)
            return null;

        return new Vector2i(x, y);
    }

    private StoryPropState? PropAt(Vector2i tile)
    {
        for (var i = _props.Length - 1; i >= 0; i--)
        {
            if (_props[i].X == tile.X && _props[i].Y == tile.Y)
                return _props[i];
        }

        return null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (!Editing)
            return;

        var tile = TileAt(args.RelativePixelPosition);
        if (tile == null)
            return;

        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            if (PropAt(tile.Value) is { } doomed)
                OnRemoveProp?.Invoke(doomed.Id);
            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (PropAt(tile.Value) is { } prop)
        {
            SelectedProp = prop.Id;
            _dragging = prop.Id;
            _hover = tile;
            OnSelectProp?.Invoke(prop.Id);
        }
        else if (Brush != null)
        {
            OnPlace?.Invoke(tile.Value.X, tile.Value.Y);
        }
        else
        {
            SelectedProp = null;
            OnSelectProp?.Invoke(null);
        }

        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || _dragging is not { } dragged)
            return;

        _dragging = null;
        var tile = TileAt(args.RelativePixelPosition);
        var prop = _props.FirstOrDefault(p => p.Id == dragged);
        if (tile is { } target && prop.Proto != null && (prop.X != target.X || prop.Y != target.Y))
            OnMoveProp?.Invoke(dragged, target.X, target.Y);

        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _hover = Editing ? TileAt(args.RelativePixelPosition) : null;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hover = null;
        _dragging = null;
    }

    private void Safe(Action draw)
    {
        try
        {
            draw();
        }
        catch (Exception e)
        {
            if (_loggedError)
                return;

            _loggedError = true;
            Logger.GetSawmill("storyteller").Error($"Story stage draw failed: {e}");
        }
    }

    private void DrawBackground(IRenderHandle renderHandle, float tile)
    {
        var handle = renderHandle.DrawingHandleScreen;
        var location = _location!;
        if (_parallax != null)
        {
            var drift = _time * 6f * UIScale;
            var size = new Vector2(PixelSize.X, PixelSize.X * _parallax.Height / Math.Max(1f, _parallax.Width)) * 1.2f;
            var start = new Vector2(-drift % size.X, 0f);
            for (var x = start.X - size.X; x < PixelSize.X + size.X; x += size.X)
            {
                for (var y = 0f; y < PixelSize.Y; y += size.Y)
                    handle.DrawTextureRect(_parallax, UIBox2.FromDimensions(new Vector2(x, y), size), Color.White.WithAlpha(0.85f));
            }
        }

        var walled = location.Wall != null;
        if (_floor != null)
        {
            var variant = _floor.Width / Math.Max(1, _floorVariants);
            for (var x = location.FloorFrom.X; x <= location.FloorTo.X; x++)
            {
                for (var y = location.FloorFrom.Y; y <= location.FloorTo.Y; y++)
                {
                    var v = (x * 7 + y * 13) % _floorVariants;
                    var region = new UIBox2(v * variant, 0, (v + 1) * variant, _floor.Height);
                    handle.DrawTextureRectRegion(_floor, TileBox(x, y, tile), region);
                }
            }

            if (walled && location.Door == null)
            {
                var region = new UIBox2(0, 0, variant, _floor.Height);
                handle.DrawTextureRectRegion(_floor, TileBox(0, Settings.DoorY, tile), region);
            }
        }

        if (_story.StageWalls.Count > 0)
        {
            foreach (var (at, uid) in _story.StageWalls)
                DrawStageEntity(renderHandle, uid, at.X, at.Y, Direction.South, tile);
            return;
        }

        if (!walled)
            return;

        var wall = Icon(location.Wall!.Value);
        for (var x = 0; x < Settings.Width; x++)
            DrawTexture(handle, wall.Default, new Vector2(x + 0.5f, 0.5f), tile, Color.White);

        for (var y = 1; y < Settings.Height; y++)
        {
            if (y != Settings.DoorY)
                DrawTexture(handle, wall.Default, new Vector2(0.5f, y + 0.5f), tile, Color.White);

            DrawTexture(handle, wall.Default, new Vector2(Settings.Width - 0.5f, y + 0.5f), tile, Color.White);
        }

        if (location.Door is { } door)
            DrawTexture(handle, Icon(door).Default, new Vector2(0.5f, Settings.DoorY + 0.5f), tile, Color.White);
    }

    private UIBox2 TileBox(int x, int y, float tile)
    {
        var topLeft = Screen(new Vector2(x, y));
        return UIBox2.FromDimensions(topLeft, new Vector2(tile + 0.5f, tile + 0.5f));
    }

    private void DrawStageProp(IRenderHandle renderHandle, StoryPropState prop, int x, int y, float tile)
    {
        if (_story.StageProp(prop.Id) is not { } uid || !DrawStageEntity(renderHandle, uid, x, y, prop.Dir, tile))
            DrawProp(renderHandle.DrawingHandleScreen, prop.Proto, x, y, prop.Dir, tile, Color.White);
    }

    private bool DrawStageEntity(IRenderHandle renderHandle, EntityUid uid, int x, int y, Direction dir, float tile)
    {
        if (!_entities.TryGetComponent(uid, out SpriteComponent? sprite) || !_entities.TryGetComponent(uid, out TransformComponent? xform))
            return false;

        _sprites.ForceUpdate(uid);
        renderHandle.DrawEntity(uid, Screen(new Vector2(x + 0.5f, y + 0.5f)), new Vector2(tile / EyeManager.PixelsPerMeter),
            Angle.Zero, Angle.Zero, dir, sprite, xform, _transform);
        return true;
    }

    private void DrawProp(DrawingHandleScreen handle, string proto, int x, int y, Direction dir, float tile, Color color)
    {
        if (!_prototypes.HasIndex<EntityPrototype>(proto))
            return;

        var texture = Frame(_sprites.GetPrototypeIcon(proto), dir);
        DrawTexture(handle, texture, new Vector2(x + 0.5f, y + 0.5f), tile, color);
    }

    private IRsiStateLike Icon(EntProtoId proto)
    {
        return _sprites.GetPrototypeIcon(proto.Id);
    }

    private Texture Frame(IRsiStateLike state, Direction dir)
    {
        var rsiDir = state.RsiDirections == RsiDirectionType.Dir1
            ? RsiDirection.South
            : dir switch
            {
                Direction.North => RsiDirection.North,
                Direction.East => RsiDirection.East,
                Direction.West => RsiDirection.West,
                _ => RsiDirection.South,
            };

        if (!state.IsAnimated || state.AnimationFrameCount <= 1)
            return state.GetFrame(rsiDir, 0);

        var total = 0f;
        for (var i = 0; i < state.AnimationFrameCount; i++)
            total += state.GetDelay(i);

        if (total <= 0f)
            return state.GetFrame(rsiDir, 0);

        var t = _time % total;
        for (var i = 0; i < state.AnimationFrameCount; i++)
        {
            t -= state.GetDelay(i);
            if (t <= 0f)
                return state.GetFrame(rsiDir, i);
        }

        return state.GetFrame(rsiDir, 0);
    }

    private void DrawTexture(DrawingHandleScreen handle, Texture texture, Vector2 world, float tile, Color color, float scale = 1f)
    {
        var size = (Vector2) texture.Size * tile / EyeManager.PixelsPerMeter * scale;
        var center = Screen(world);
        handle.DrawTextureRect(texture, UIBox2.FromDimensions(center - size / 2f, size), color);
    }

    private void DrawFigure(IRenderHandle renderHandle, byte id, Figure figure, float tile)
    {
        if (figure.Actor == null || figure.Alpha <= 0.01f)
            return;

        var actor = figure.Actor;
        var position = figure.Shown;
        var lying = figure.Pose is StoryPose.Lie or StoryPose.Fallen or StoryPose.Dead;
        var wasLying = figure.PreviousPose is StoryPose.Lie or StoryPose.Fallen or StoryPose.Dead;
        var poseT = Math.Clamp((_time - figure.PoseAt) / LieTime, 0f, 1f);
        var lie = lying ? (wasLying ? 1f : Ease(poseT)) : (wasLying ? 1f - Ease(poseT) : 0f);

        var offset = Vector2.Zero;
        if (figure.Moving)
            offset.Y -= MathF.Abs(MathF.Sin(_time * (actor.Running ? 16f : 10f))) * 0.06f;
        if (figure.Pose == StoryPose.Sit)
            offset.Y += 0.12f;
        if (figure.Pose == StoryPose.Kneel)
            offset.Y += 0.2f;

        var lungeT = _time - figure.LungeAt;
        if (lungeT is >= 0f and < 0.3f)
            offset += Step(actor.Facing) * MathF.Sin(lungeT / 0.3f * MathF.PI) * 0.3f;

        var flashT = _time - figure.FlashAt;
        var color = Color.White;
        if (flashT is >= 0f and < FlashTime)
        {
            var k = 1f - flashT / FlashTime;
            color = Color.InterpolateBetween(Color.White, new Color(1f, 0.3f, 0.3f), k);
            offset += new Vector2(MathF.Sin(flashT * 60f) * 0.06f * k, 0f);
        }

        if (figure.Pose == StoryPose.Dead)
            color = Color.InterpolateBetween(color, new Color(0.65f, 0.65f, 0.7f), Ease(poseT));

        var direction = figure.Moving ? figure.MoveDir : actor.Facing;
        if (figure.DanceUntil > _time)
        {
            var spin = new[] { Direction.South, Direction.East, Direction.North, Direction.West };
            direction = spin[(int) (_time / 0.18f) % spin.Length];
            offset.Y -= MathF.Abs(MathF.Sin(_time * 9f)) * 0.12f;
        }

        var held = actor.Held.Length > 0 && !_story.InHand(id) && _prototypes.TryIndex<StoryItemPrototype>(actor.Held, out var heldProto)
            ? heldProto
            : null;
        var screen = renderHandle.DrawingHandleScreen;
        if (held != null && direction == Direction.North && lie < 0.5f)
            DrawHeld(screen, held, position + offset, direction, tile, figure.Alpha);

        var doll = _story.Doll(id);
        if (doll is { } uid && _entities.TryGetComponent(uid, out SpriteComponent? sprite) && _entities.TryGetComponent(uid, out TransformComponent? xform))
        {
            _sprites.ForceUpdate(uid);
            _sprites.SetRotation((uid, sprite), Angle.FromDegrees(90 * lie));
            var world = renderHandle.DrawingHandleWorld;
            var old = world.Modulate;
            world.Modulate = old * color.WithAlpha(figure.Alpha);
            renderHandle.DrawEntity(uid, Screen(position + offset + new Vector2(0f, 0.15f * lie)), new Vector2(tile / EyeManager.PixelsPerMeter),
                Angle.Zero, Angle.Zero, direction, sprite, xform, _transform);
            world.Modulate = old;
        }
        else
        {
            var center = Screen(position + offset);
            screen.DrawCircle(center, tile * 0.3f, new Color(0.6f, 0.6f, 0.6f, figure.Alpha));
        }

        if (held != null && direction != Direction.North && lie < 0.5f)
            DrawHeld(screen, held, position + offset, direction, tile, figure.Alpha);

        var name = actor.Name;
        if (name.Length > 0)
        {
            var scale = UIScale;
            var size = screen.GetDimensions(_nameFont, name, scale);
            var at = Screen(position + new Vector2(0f, 0.55f)) - new Vector2(size.X / 2f, 0f);
            screen.DrawString(_nameFont, at + new Vector2(1f, 1f), name, scale, Color.Black.WithAlpha(0.6f * figure.Alpha));
            screen.DrawString(_nameFont, at, name, scale, new Color(0.92f, 0.92f, 0.92f, 0.85f * figure.Alpha));
        }
    }

    private void DrawHeld(DrawingHandleScreen handle, StoryItemPrototype item, Vector2 position, Direction facing, float tile, float alpha)
    {
        var hand = facing switch
        {
            Direction.North => new Vector2(-0.22f, 0.05f),
            Direction.East => new Vector2(0.22f, 0.1f),
            Direction.West => new Vector2(-0.22f, 0.1f),
            _ => new Vector2(0.22f, 0.12f),
        };

        var texture = Icon(item.Item).Default;
        var size = (Vector2) texture.Size * tile / EyeManager.PixelsPerMeter * 0.6f;
        var center = Screen(position + hand);
        var box = UIBox2.FromDimensions(center - size / 2f, size);
        if (facing == Direction.West)
            box = new UIBox2(box.Right, box.Top, box.Left, box.Bottom);

        handle.DrawTextureRect(texture, box, Color.White.WithAlpha(alpha));
    }

    private void DrawEffects(DrawingHandleScreen handle, float tile)
    {
        foreach (var effect in _effects)
        {
            if (!effect.Started)
                continue;

            var t = Math.Clamp((_time - effect.Start) / Math.Max(0.01f, effect.Duration), 0f, 1f);
            var source = Find(effect.Data.Actor);
            var target = Find(effect.Data.Target);
            switch (effect.Data.Kind)
            {
                case StoryEffectKind.Hit when target != null:
                {
                    var spec = Use(effect.Data.Item) == StoryItemUse.Melee ? Settings.SlashArc : Settings.PunchArc;
                    if (spec == null)
                        break;

                    var arc = _sprites.RsiStateLike(spec);
                    var count = Math.Max(1, arc.AnimationFrameCount);
                    var frame = Math.Clamp((int) (t * count), 0, count - 1);
                    var facing = source?.Actor?.Facing ?? Direction.South;
                    var direction = arc.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : Rsi(facing);
                    var tex = arc.GetFrame(direction, frame);
                    DrawTexture(handle, tex, target.Shown, tile, Color.White.WithAlpha(1f - t * 0.5f));
                    break;
                }
                case StoryEffectKind.Shoot when source != null:
                {
                    var facing = source.Actor?.Facing ?? Direction.South;
                    var muzzle = source.Shown + Step(facing) * 0.55f;
                    var end = target?.Shown ?? source.Shown + Step(facing) * 8f;
                    var laser = Use(effect.Data.Item) == StoryItemUse.Laser;
                    if (t < 0.35f)
                    {
                        if ((laser ? Settings.LaserMuzzle : Settings.BulletMuzzle) is { } flash)
                            DrawTexture(handle, _sprites.RsiStateLike(flash).Default, muzzle, tile, Color.White);
                        var line = laser ? Settings.LaserTrail : Settings.BulletTrail;
                        handle.DrawLine(Screen(muzzle), Screen(end), line);
                        handle.DrawLine(Screen(muzzle) + new Vector2(0f, 1f), Screen(end) + new Vector2(0f, 1f), line.WithAlpha(0.5f));
                    }

                    if (target != null && t > 0.15f && (laser ? Settings.LaserImpact : Settings.BulletImpact) is { } impact)
                        DrawTexture(handle, _sprites.RsiStateLike(impact).Default, target.Shown, tile, Color.White.WithAlpha(1f - t));

                    break;
                }
                case StoryEffectKind.Hug or StoryEffectKind.Kiss when source != null && Settings.Hearts != null:
                {
                    var hearts = _sprites.RsiStateLike(Settings.Hearts);
                    var middle = target != null ? (source.Shown + target.Shown) / 2f : source.Shown;
                    var rise = middle - new Vector2(0f, 0.6f + t * 0.8f);
                    DrawTexture(handle, Frame(hearts, Direction.South), rise, tile, Color.White.WithAlpha(1f - t));
                    break;
                }
                case StoryEffectKind.Give when source != null && target != null:
                {
                    if (!_prototypes.TryIndex<StoryItemPrototype>(effect.Data.Item, out var item))
                        break;

                    var at = Vector2.Lerp(source.Shown, target.Shown, Ease(t)) - new Vector2(0f, MathF.Sin(t * MathF.PI) * 0.5f);
                    DrawTexture(handle, Icon(item.Item).Default, at, tile, Color.White, 0.7f);
                    break;
                }
                case StoryEffectKind.Point when source != null && Settings.Pointer != null:
                {
                    var spot = target?.Shown ?? source.Shown + Step(source.Actor?.Facing ?? Direction.South) * 2f;
                    var bob = MathF.Sin(_time * 6f) * 0.08f;
                    DrawTexture(handle, _sprites.RsiStateLike(Settings.Pointer).Default, spot - new Vector2(0f, 0.9f + bob), tile,
                        Color.White.WithAlpha(t > 0.8f ? (1f - t) * 5f : 1f));
                    break;
                }
            }
        }
    }

    private void DrawLighting(DrawingHandleScreen handle)
    {
        var location = _location!;
        _prototypes.TryIndex<StoryLightPrototype>(_light, out var light);
        var darkness = light?.Darkness ?? location.Darkness;
        if (darkness > 0f)
            handle.DrawRect(PixelSizeBox, Color.Black.WithAlpha(Math.Clamp(darkness, 0f, 0.9f)));

        var tint = _prototypes.TryIndex<StoryTintPrototype>(_tint, out var tintProto) && tintProto.Color is { } chosen
            ? chosen
            : location.Tint;
        if (tint.A > 0f)
            handle.DrawRect(PixelSizeBox, tint);

        if (light is { Pulse.A: > 0f })
        {
            var pulse = light.Pulse.A * (0.5f + 0.5f * MathF.Sin(_time * 4f));
            handle.DrawRect(PixelSizeBox, light.Pulse.WithAlpha(pulse));
        }
    }

    private void DrawBubbles(DrawingHandleScreen handle, float tile)
    {
        var stacked = new Dictionary<byte, float>();
        foreach (var effect in _effects)
        {
            if (!effect.Started || effect.Data.Kind is not (StoryEffectKind.Say or StoryEffectKind.Emote))
                continue;

            var source = Find(effect.Data.Actor);
            if (source == null)
                continue;

            var t = (_time - effect.Start) / Math.Max(0.01f, effect.Duration);
            var alpha = t > 0.85f ? Math.Clamp((1f - t) / 0.15f, 0f, 1f) : Math.Clamp((_time - effect.Start) / 0.15f, 0f, 1f);
            var emote = effect.Data.Kind == StoryEffectKind.Emote;
            var text = !emote
                ? effect.Data.Text
                : _prototypes.TryIndex<StoryEmotePrototype>(effect.Data.Text, out var emoteProto)
                    ? "*" + Loc.GetString(emoteProto.Name) + "*"
                    : "*" + effect.Data.Text + "*";
            var lines = Wrap(handle, text, 230f * UIScale);
            var scale = UIScale;
            var lineHeight = _bubbleFont.GetLineHeight(scale);
            var width = lines.Max(l => handle.GetDimensions(_bubbleFont, l, scale).X);
            var height = lineHeight * lines.Count;
            var lift = stacked.GetValueOrDefault(effect.Data.Actor);
            var anchor = Screen(source.Shown - new Vector2(0f, 0.75f)) - new Vector2(0f, lift + (1f - alpha) * -6f);
            var pad = new Vector2(7f, 4f) * scale;
            var box = new UIBox2(anchor.X - width / 2f - pad.X, anchor.Y - height - pad.Y * 2f, anchor.X + width / 2f + pad.X, anchor.Y);
            var back = emote ? new Color(0.16f, 0.12f, 0.05f, 0.82f * alpha) : new Color(0.05f, 0.06f, 0.09f, 0.85f * alpha);
            var border = emote ? new Color(0.9f, 0.75f, 0.35f, 0.7f * alpha) : new Color(0.55f, 0.6f, 0.75f, 0.7f * alpha);
            handle.DrawRect(box, back);
            handle.DrawRect(box, border, false);
            var y = box.Top + pad.Y;
            foreach (var line in lines)
            {
                var size = handle.GetDimensions(_bubbleFont, line, scale);
                var textColor = emote ? new Color(1f, 0.88f, 0.6f, alpha) : new Color(1f, 1f, 1f, alpha);
                handle.DrawString(_bubbleFont, new Vector2(anchor.X - size.X / 2f, y), line, scale, textColor);
                y += lineHeight;
            }

            stacked[effect.Data.Actor] = lift + box.Height + 4f * scale;
        }
    }

    private List<string> Wrap(DrawingHandleScreen handle, string text, float max)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && handle.GetDimensions(_bubbleFont, candidate, UIScale).X > max)
            {
                lines.Add(current);
                current = word;
                if (lines.Count >= 4)
                    break;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0 && lines.Count < 5)
            lines.Add(current);

        if (lines.Count == 0)
            lines.Add(text);

        return lines;
    }

    private void DrawOverlay(DrawingHandleScreen handle)
    {
        var w = PixelSize.X;
        var h = PixelSize.Y;
        const int steps = 12;
        var depth = MathF.Min(w, h) * 0.12f;
        for (var i = 0; i < steps; i++)
        {
            var inset = depth * i / steps;
            var color = Color.Black.WithAlpha(0.07f * (1f - (float) i / steps));
            handle.DrawRect(new UIBox2(0f, 0f, w, inset + 1f), color);
            handle.DrawRect(new UIBox2(0f, h - inset - 1f, w, h), color);
            handle.DrawRect(new UIBox2(0f, 0f, inset + 1f, h), color);
            handle.DrawRect(new UIBox2(w - inset - 1f, 0f, w, h), color);
        }

        var scale = UIScale;
        var sinceLocation = _time - _locationAt;
        if (_location != null && sinceLocation < 4f)
        {
            var alpha = sinceLocation < 0.4f ? sinceLocation / 0.4f : sinceLocation > 3f ? 4f - sinceLocation : 1f;
            var title = Loc.GetString(_location.Name);
            var size = handle.GetDimensions(_titleFont, title, scale);
            var at = new Vector2(24f * scale, 18f * scale);
            handle.DrawRect(new UIBox2(at - new Vector2(10f, 6f) * scale, at + size + new Vector2(10f, 6f) * scale), Color.Black.WithAlpha(0.5f * alpha));
            handle.DrawString(_titleFont, at, title, scale, new Color(0.95f, 0.88f, 0.7f, alpha));
        }

        if (_caption.Length == 0)
            return;

        var captionAlpha = Math.Clamp((_time - _captionAt) / 0.3f, 0f, 1f);
        var lines = WrapCaption(handle, _caption, w - 80f * scale);
        var lineHeight = _captionFont.GetLineHeight(scale);
        var height = lineHeight * lines.Count + 16f * scale;
        handle.DrawRect(new UIBox2(0f, h - height, w, h), new Color(0.02f, 0.02f, 0.03f, 0.92f * captionAlpha));
        handle.DrawRect(new UIBox2(0f, h - height - 2f * scale, w, h - height), new Color(0.75f, 0.65f, 0.45f, 0.5f * captionAlpha));
        var y = h - height + 8f * scale;
        foreach (var line in lines)
        {
            var size = handle.GetDimensions(_captionFont, line, scale);
            handle.DrawString(_captionFont, new Vector2((w - size.X) / 2f, y), line, scale, new Color(0.96f, 0.93f, 0.86f, captionAlpha));
            y += lineHeight;
        }
    }

    private List<string> WrapCaption(DrawingHandleScreen handle, string text, float max)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && handle.GetDimensions(_captionFont, candidate, UIScale).X > max)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines.Take(3).ToList();
    }

    #endregion

    private static RsiDirection Rsi(Direction dir)
    {
        return dir switch
        {
            Direction.North => RsiDirection.North,
            Direction.East => RsiDirection.East,
            Direction.West => RsiDirection.West,
            _ => RsiDirection.South,
        };
    }

    private static Vector2 Step(Direction facing)
    {
        return facing switch
        {
            Direction.North => new Vector2(0f, -1f),
            Direction.East => new Vector2(1f, 0f),
            Direction.West => new Vector2(-1f, 0f),
            _ => new Vector2(0f, 1f),
        };
    }

    private static float Ease(float value)
    {
        var x = Math.Clamp(value, 0f, 1f);
        return 1f - (1f - x) * (1f - x);
    }

    private sealed class Figure
    {
        public StoryActorState? Actor;
        public Vector2 Shown;
        public Vector2 Target;
        public StoryPose Pose;
        public StoryPose PreviousPose;
        public float PoseAt = -10f;
        public float Alpha;
        public bool Moving;
        public Direction MoveDir = Direction.South;
        public float FlashAt = -10f;
        public float LungeAt = -10f;
        public float DanceUntil;
        public float HoldUntil;
    }

    private sealed class Effect
    {
        public readonly StoryEffect Data;
        public readonly float Start;
        public float Duration = 1f;
        public bool Started;

        public Effect(StoryEffect data, float start)
        {
            Data = data;
            Start = start;
        }
    }
}
