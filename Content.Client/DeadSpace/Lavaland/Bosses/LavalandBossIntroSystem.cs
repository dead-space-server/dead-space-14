// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Audio;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.DeadSpace.Lavaland.Bosses;

/// <summary>
/// Plays a boss intro sent by the server: a camera of its own follows the shots of the scene, the dolls of the
/// players act out their cues, the screen shakes on the hits, the boss name is shown. The server moves and spawns
/// the real things; only the camera and the sprites of the players change here.
/// </summary>
public sealed class LavalandBossIntroSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;
    [Dependency] private readonly LavalandSpriteMotionSystem _motion = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ContentAudioSystem _contentAudio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private const float MusicFadeIn = 1.5f;
    private const float MusicFadeOut = 1.5f;

    /// <summary>The boss theme quietly under the scene, and whether its fade out has begun.</summary>
    private EntityUid? _music;
    private bool _musicFading;

    private static readonly Direction[] LookAround = { Direction.West, Direction.East, Direction.West, Direction.South };

    private LavalandBossIntroOverlay _overlay = default!;
    private Scene? _scene;

    private sealed class Scene
    {
        public required LavalandBossIntroEvent Event;
        public readonly List<EntityUid> Watchers = new();
        public readonly Dictionary<EntityUid, (Vector2 Offset, Angle Rotation, bool Override, Direction Direction)> Saved = new();

        /// <summary>Where each knocked watcher was when it was hit.</summary>
        public readonly Dictionary<EntityUid, Vector2> KnockFrom = new();
        public required Robust.Shared.Graphics.Eye Camera;
        public Vector2 NormalZoom;
        public Vector2 Focus;
        public float Elapsed;

        /// <summary>The things of the scene by time, and how many are spawned already.</summary>
        public readonly List<LavalandIntroFx> Effects = new();
        public int NextEffect;

        public readonly List<LavalandIntroSound> Sounds = new();
        public int NextSound;
    }

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LavalandBossIntroOverlay(_cache);
        _overlays.AddOverlay(_overlay);
        SubscribeNetworkEvent<LavalandBossIntroEvent>(OnIntro);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        End();
        _overlays.RemoveOverlay(_overlay);
    }

    private void OnIntro(LavalandBossIntroEvent ev)
    {
        End();

        var current = _eyeManager.CurrentEye;
        var scene = new Scene
        {
            Event = ev,
            Camera = new Robust.Shared.Graphics.Eye
            {
                Position = current.Position,
                Rotation = current.Rotation,
                Zoom = current.Zoom,
                DrawFov = false,
            },
            NormalZoom = current.Zoom,
            Focus = current.Position.Position,
        };

        foreach (var netWatcher in ev.Watchers)
        {
            var watcher = GetEntity(netWatcher);
            if (!Exists(watcher) || !TryComp<SpriteComponent>(watcher, out var sprite))
                continue;

            scene.Watchers.Add(watcher);
            scene.Saved[watcher] = (sprite.Offset, sprite.Rotation, sprite.EnableDirectionOverride, sprite.DirectionOverride);
        }

        scene.Effects.AddRange(ev.Effects);
        scene.Effects.Sort((a, b) => a.Time.CompareTo(b.Time));
        scene.Sounds.AddRange(ev.Sounds);
        scene.Sounds.Sort((a, b) => a.Time.CompareTo(b.Time));

        _overlay.Title = ev.Title;
        _overlay.Subtitle = ev.Subtitle;
        _overlay.TitleColor = ev.TitleColor;
        _eyeManager.CurrentEye = scene.Camera;
        _scene = scene;
        StartMusic(ev);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_scene is not { } scene)
            return;

        scene.Elapsed += frameTime;
        var t = scene.Elapsed;
        var ev = scene.Event;
        if (t >= ev.Duration)
        {
            End();
            return;
        }

        // The theme fades away as the scene ends; the fight brings it in properly.
        if (!_musicFading && t >= ev.Duration - MusicFadeOut)
            StopMusic();

        UpdateCamera(scene, t, frameTime);
        UpdateDolls(scene, t);
        UpdateEffects(scene, t);

        var returnStart = ev.Duration - ev.ReturnTime;
        _overlay.Bars = t < 0.6f ? t / 0.6f : t > returnStart ? 1f - (t - returnStart) / MathF.Max(0.01f, ev.ReturnTime) : 1f;

        var titleT = t - ev.TitleDelay;
        _overlay.TitleAlpha = titleT < 0f || titleT > ev.TitleDuration
            ? 0f
            : MathF.Min(1f, MathF.Min(titleT / 0.4f, (ev.TitleDuration - titleT) / 0.5f));
    }

    private void UpdateCamera(Scene scene, float t, float frameTime)
    {
        var ev = scene.Event;
        if (_player.LocalEntity is not { } local)
            return;

        var player = _transform.GetMapCoordinates(local);
        Vector2 focus;
        float zoom;
        if (t >= ev.Duration - ev.ReturnTime)
        {
            focus = player.Position;
            zoom = scene.NormalZoom.X;
        }
        else
        {
            var shot = new LavalandIntroShot(0f, true, Vector2.Zero, ev.StartZoom);
            foreach (var candidate in ev.Shots)
            {
                if (candidate.Time <= t)
                    shot = candidate;
            }

            focus = shot.OnPlayer ? player.Position + shot.Point : shot.Point;
            zoom = shot.Zoom;
        }

        // Smooth glide towards the shot of this moment.
        var blend = 1f - MathF.Exp(-5f * frameTime);
        scene.Focus = Vector2.Lerp(scene.Focus, focus, blend);
        var currentZoom = scene.Camera.Zoom.X + (zoom - scene.Camera.Zoom.X) * blend;

        // Shakes fade out in half a second.
        var shake = 0f;
        foreach (var hit in ev.Shakes)
        {
            var since = t - hit.X;
            if (since >= 0f && since < 0.5f)
                shake = MathF.Max(shake, hit.Y * (1f - since / 0.5f));
        }

        var jitter = shake > 0f ? _random.NextVector2(shake) : Vector2.Zero;
        scene.Camera.Position = new MapCoordinates(scene.Focus + jitter, player.MapId);
        scene.Camera.Zoom = new Vector2(currentZoom, currentZoom);
    }

    /// <summary>Spawns the things of the scene whose moment has come, on the clock of the scene.</summary>
    private void UpdateEffects(Scene scene, float t)
    {
        while (scene.NextSound < scene.Sounds.Count && scene.Sounds[scene.NextSound].Time <= t)
        {
            var sound = scene.Sounds[scene.NextSound].Sound;
            _audio.PlayGlobal(_audio.ResolveSound(sound), Filter.Local(), false, sound.Params);
            scene.NextSound++;
        }

        if (scene.NextEffect >= scene.Effects.Count || _player.LocalEntity is not { } local)
            return;

        var mapId = _transform.GetMapCoordinates(local).MapId;
        while (scene.NextEffect < scene.Effects.Count && scene.Effects[scene.NextEffect].Time <= t)
        {
            _motion.SpawnFx(scene.Effects[scene.NextEffect], mapId);
            scene.NextEffect++;
        }
    }

    private void UpdateDolls(Scene scene, float t)
    {
        var cue = new LavalandIntroDollCue(0f, LavalandIntroDollAction.None);
        foreach (var candidate in scene.Event.Dolls)
        {
            if (candidate.Time <= t)
                cue = candidate;
        }

        var since = t - cue.Time;
        var progress = cue.Length <= 0f ? 1f : Math.Clamp(since / cue.Length, 0f, 1f);
        var eased = 1f - (1f - progress) * (1f - progress);

        // Knocked flying: when, for how long, and where each one lands (it stays there afterwards).
        LavalandIntroDollCue? knock = null;
        foreach (var candidate in scene.Event.Dolls)
        {
            if (candidate.Action == LavalandIntroDollAction.Knocked)
            {
                knock = candidate;
                break;
            }
        }

        foreach (var watcher in scene.Watchers)
        {
            // A doll flying through the air is drawn by its motion.
            if (HasComp<LavalandSpriteMotionComponent>(watcher))
                continue;

            if (!TryComp<SpriteComponent>(watcher, out var sprite) || !scene.Saved.TryGetValue(watcher, out var saved))
                continue;

            var offset = saved.Offset;
            var rotation = saved.Rotation;
            Direction? direction = null;

            if (knock is { } knockCue && t >= knockCue.Time && TryGetKnock(scene.Event, watcher, out var knockTarget))
            {
                // Drawn relative to where the body is now: the server moves it to the landing spot as it lands.
                var here = _transform.GetWorldPosition(watcher);
                if (!scene.KnockFrom.TryGetValue(watcher, out var from))
                    scene.KnockFrom[watcher] = from = here;

                var flight = knockCue.Length <= 0f ? 1f : Math.Clamp((t - knockCue.Time) / knockCue.Length, 0f, 1f);
                if (flight < 1f)
                {
                    // An arc through the air, spinning, like a pin hit by the ball.
                    var arc = new Vector2(0f, 2.5f * 4f * flight * (1f - flight));
                    offset += Vector2.Lerp(from, knockTarget, flight) + arc - here;
                    rotation += new Angle((t - knockCue.Time) * 12f);
                    direction = Direction.South;
                }
                else
                {
                    offset += knockTarget - here;
                }
            }

            switch (cue.Action)
            {
                case LavalandIntroDollAction.WalkIn:
                    offset += cue.Vector * (1f - eased);
                    direction = cue.Vector.X <= 0f ? Direction.East : Direction.West;
                    break;

                case LavalandIntroDollAction.LookAround:
                    direction = LookAround[(int) (since / 0.55f) % LookAround.Length];
                    break;

                case LavalandIntroDollAction.LookUp:
                    direction = Direction.North;
                    break;

                case LavalandIntroDollAction.Knocked:
                    break;

                case LavalandIntroDollAction.Lie:
                    rotation = Angle.FromDegrees(90);
                    direction = Direction.South;
                    break;

                case LavalandIntroDollAction.StandUp:
                    rotation = Angle.FromDegrees(90 * (1f - eased));
                    direction = Direction.South;
                    break;

                case LavalandIntroDollAction.StepBack:
                    offset += cue.Vector * eased;
                    direction = (-cue.Vector).GetDir();
                    break;
            }

            _sprite.SetOffset((watcher, sprite), offset);
            _sprite.SetRotation((watcher, sprite), rotation);
            _spriteTree.QueueTreeUpdate((watcher, sprite));
            sprite.EnableDirectionOverride = direction != null || saved.Override;
            sprite.DirectionOverride = direction ?? saved.Direction;
        }
    }

    private bool TryGetKnock(LavalandBossIntroEvent ev, EntityUid watcher, out Vector2 target)
    {
        var net = GetNetEntity(watcher);
        foreach (var knock in ev.Knocks)
        {
            if (knock.Watcher != net)
                continue;

            target = knock.Target;
            return true;
        }

        target = default;
        return false;
    }

    /// <summary>The boss's fight theme from its middle, quiet, fading in under the scene.</summary>
    private void StartMusic(LavalandBossIntroEvent ev)
    {
        StopMusic();
        _musicFading = false;
        if (ev.Music is not { } music || !_cfg.GetCVar(CCCCVars.BossMusicEnabled))
            return;

        var length = (float) _audio.GetAudioLength(music).TotalSeconds;
        var gain = _cfg.GetCVar(CCCCVars.BossMusicVolume);
        if (gain <= 0.001f)
            return;

        var audioParams = AudioParams.Default
            .WithVolume(ev.MusicVolume + SharedAudioSystem.GainToVolume(gain))
            .WithPlayOffset(MathF.Max(0f, length * Math.Clamp(ev.MusicStart, 0f, 0.95f)));

        var stream = _audio.PlayGlobal(music, Filter.Local(), false, audioParams);
        _music = stream?.Entity;
        if (stream != null)
            _contentAudio.FadeIn(_music, stream.Value.Component, MusicFadeIn);
    }

    private void StopMusic()
    {
        _musicFading = true;
        if (_music == null)
            return;

        _contentAudio.FadeOut(_music, duration: MusicFadeOut);
        _music = null;
    }

    private void End()
    {
        StopMusic();
        if (_scene is not { } scene)
            return;

        _scene = null;
        foreach (var (uid, saved) in scene.Saved)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
                continue;

            _sprite.SetOffset((uid, sprite), saved.Offset);
            _sprite.SetRotation((uid, sprite), saved.Rotation);
            _spriteTree.QueueTreeUpdate((uid, sprite));
            sprite.EnableDirectionOverride = saved.Override;
            sprite.DirectionOverride = saved.Direction;
        }

        _overlay.Bars = 0f;
        _overlay.TitleAlpha = 0f;

        // Back to the camera of the body the player controls.
        if (_player.LocalEntity is { } local && TryComp<EyeComponent>(local, out var eye) && eye.Eye != null)
            _eyeManager.CurrentEye = eye.Eye;
        else if (ReferenceEquals(_eyeManager.CurrentEye, scene.Camera))
            _eyeManager.ClearCurrentEye();
    }
}
