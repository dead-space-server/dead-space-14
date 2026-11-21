// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Resources;
using Content.Shared.DeadSpace.RoundEnd;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

/// <summary>
/// The Architect's full-screen MVP scene. Over the Circle's sky a huge necromorph moon rises from below; when half of
/// it is out, a geyser of blood bursts from behind it and throws the tiny Architect high up. They flip, land on the
/// moon's dome, the camera closes in and the Architect swings the arm out to the side.
/// The character is drawn layer by layer and cut into head, torso, arms and legs, so the limbs move on their own.
/// </summary>
public sealed class ArchitectMvpShowcase : Control
{
    // Timeline, seconds.
    private const float GeyserStart = 2.6f;
    private const float RiseEnd = 4.4f;
    private const float FlipEnd = 5.8f;
    // The moon keeps rising until the very moment the Architect lands on it.
    private const float MoonEnd = FlipEnd;
    // A sharp, tilted push in on the Architect right after the landing.
    private const float ZoomStart = FlipEnd + 0.12f;
    private const float ZoomEnd = ZoomStart + 0.35f;
    private const float ArmStart = ZoomEnd + 0.35f;
    private const float ArmEnd = ArmStart + 0.8f;
    private const float TitleStart = ArmEnd + 0.2f;
    private const float ShakeLength = 0.35f;
    private const float SceneLength = 11.5f;
    private const float FadeIn = 0.5f;
    private const float FadeOut = 0.7f;

    // Sizes as shares of the screen height.
    private const float MoonHeight = 1.25f;
    private const float CharacterHeight = 0.055f;
    private const float Apex = 0.1f;
    private const float MaxZoom = 3.4f;
    // Camera roll once pushed in, radians.
    private const float Roll = -0.22f;

    // Where the top of the moon's dome is in its 248x324 sprite. The dome pulses, so its top differs per frame.
    private const float DomeTopX = 124f / 248f;
    private const float MoonFrameDelay = 0.2f;
    private const float MoonSpriteHeight = 324f;
    private static readonly float[] DomeTops = { 0f, 2f, 4f, 10f, 13f, 12f, 7f, 3f };

    private const int GeyserCount = 300;
    private const float GeyserLength = 2.2f;
    private const float GeyserLife = 1.6f;
    private const int SplashCount = 40;

    private static readonly ResPath[] SkyLayers =
    {
        new("/Textures/_DeadSpace/Necromorfs/TheCircle/Parallax/Asteroids/DustParallaxBG.png"),
        new("/Textures/_DeadSpace/Necromorfs/TheCircle/Parallax/Asteroids/DustParallaxNeb.png"),
        new("/Textures/_DeadSpace/Necromorfs/TheCircle/Parallax/Asteroids/ThreePlanets.png"),
    };

    private static readonly SpriteSpecifier.Rsi Moon = new(new ResPath("/Textures/_DeadSpace/Necromorfs/moon.rsi"), "moon");
    private static readonly SpriteSpecifier.Rsi ArmInHand =
        new(new ResPath("/Textures/_DeadSpace/Necromorfs/TheCircle/Weapon/tools/architect-arm.rsi"), "inhand-right");

    // Body pieces of a 32x32 humanoid. Limb angles are "outward positive"; the sign says which side of the screen.
    private static readonly Piece Head = new(new UIBox2(0, 0, 32, 9), default, 0f);
    private static readonly Piece Torso = new(new UIBox2(12, 9, 20, 24), default, 0f);
    private static readonly Piece Whole = new(new UIBox2(0, 0, 32, 32), default, 0f);
    private static readonly Piece ViewerLeftArm = new(new UIBox2(0, 9, 12, 24), new Vector2(11f, 10.5f), 1f);
    private static readonly Piece ViewerRightArm = new(new UIBox2(20, 9, 32, 24), new Vector2(21f, 10.5f), -1f);
    private static readonly Piece ViewerLeftLeg = new(new UIBox2(0, 24, 16, 32), new Vector2(13.5f, 23.5f), 1f);
    private static readonly Piece ViewerRightLeg = new(new UIBox2(16, 24, 32, 32), new Vector2(18.5f, 23.5f), -1f);

    // Arm angles, outward positive.
    private const float ArmRest = 0.12f;
    private const float ArmUp = MathF.PI * 0.94f;
    private const float ArmCrossed = -1.3f;
    private const float ArmOut = 1.45f;
    private const float Lean = -0.35f;

    private static readonly ProtoId<ShaderPrototype> AdditiveShader = "ArchitectMvpAdditive";

    // Sound: the score from the middle of the track, and blood when the Architect is thrown out.
    private const string Music = "/Audio/_DeadSpace/TEMP_FOR_EVENT/TheZuTi_music/Dying_Light/Demolition.ogg";
    private const float MusicStartShare = 0.45f;
    private const float MusicVolume = -3f;
    private static readonly SoundSpecifier BloodBurst = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/exit_blood.ogg");
    private static readonly SoundSpecifier BloodGush = new SoundPathSpecifier("/Audio/Effects/Fluids/blood1.ogg");
    private static readonly SoundSpecifier BloodSpray = new SoundPathSpecifier("/Audio/Effects/Fluids/blood2.ogg");
    private static readonly SoundSpecifier Landing = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    // Worthy contenders are thrown out by the same geyser a moment after the Architect, fly out to the sides
    // and come down on one knee beside them just as they land.
    private const float ContenderLaunch = GeyserStart + 0.45f;
    private const float ContenderLaunchGap = 0.15f;
    private const float ContenderRiseLength = 1.4f;
    private const float ContenderApex = 0.22f;
    private const float LabelsStart = TitleStart + 0.5f;
    // How far from the Architect they kneel, in sprite pixels, and the dome's curvature (its radius in the sprite).
    private const float ContenderSpacing = 34f;
    private const float DomeRadius = 69f / 248f;

    // Light colours: the moon's sickly glow and the red of the blood.
    private static readonly Color MoonLight = Color.FromHex("#ffcf8a");
    private static readonly Color BloodLight = Color.FromHex("#ff2a1a");

    private readonly IEntityManager _entities;
    private readonly SpriteSystem _sprites;
    private readonly SharedAudioSystem _audio;
    private readonly ShaderInstance _additive;
    private readonly List<Texture> _sky = new();
    private readonly RSI.State? _armState;
    private readonly Font _titleFont;
    private readonly Font _nameFont;
    private readonly Font _smallFont;
    private readonly List<Contender> _contenders = new();
    private readonly List<(Texture Texture, Color Color, Vector2 TopLeft)> _contenderLayers = new();
    private int _kills;
    private int _assists;
    private bool _hasStats;
    private EntityUid? _music;
    private int _sounds;
    private readonly Particle[] _geyser = new Particle[GeyserCount];
    private readonly Particle[] _splash = new Particle[SplashCount];
    private readonly List<(Texture Texture, Color Color, Vector2 TopLeft)> _layers = new();

    private EntityUid? _character;
    private Color _light = Color.White;
    private List<(Texture Texture, Color Color, Vector2 TopLeft)> _drawLayers = new();
    private float _time;
    private bool _playing;

    public string CharacterName = string.Empty;

    /// <summary>
    /// The Architect always looks the same in the scene, whatever the player wore or looked like.
    /// </summary>
    public static readonly RoundEndDollData ArchitectLook = new()
    {
        BodyPrototype = "MobHuman",
        FallbackGear = "TheCircleArchitectShowcaseGear",
    };

    public event Action? Finished;

    public ArchitectMvpShowcase(IEntityManager entities)
    {
        _entities = entities;
        _sprites = entities.System<SpriteSystem>();
        _audio = entities.System<SharedAudioSystem>();
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        Visible = false;

        var cache = IoCManager.Resolve<IResourceCache>();
        foreach (var layer in SkyLayers)
        {
            if (cache.TryGetResource<TextureResource>(layer, out var texture))
                _sky.Add(texture.Texture);
        }

        _armState = _sprites.RsiStateLike(ArmInHand) as RSI.State;
        _additive = IoCManager.Resolve<IPrototypeManager>().Index(AdditiveShader).Instance();
        _titleFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 40);
        _nameFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 22);
        _smallFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 14);

        var random = new Random(1337);
        for (var i = 0; i < GeyserCount; i++)
        {
            var angle = (random.NextSingle() - 0.5f) * 0.45f;
            var speed = 1.05f + random.NextSingle() * 0.55f;
            _geyser[i] = new Particle(
                GeyserStart + GeyserLength * i / GeyserCount,
                new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * speed,
                0.004f + random.NextSingle() * 0.011f,
                Color.FromHsv(new Vector4(0.99f, 0.9f, 0.35f + random.NextSingle() * 0.5f, 1f)));
        }

        for (var i = 0; i < SplashCount; i++)
        {
            var side = i % 2 == 0 ? -1f : 1f;
            var angle = side * (0.6f + random.NextSingle() * 0.8f);
            var speed = 0.12f + random.NextSingle() * 0.2f;
            _splash[i] = new Particle(
                FlipEnd,
                new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * speed,
                0.002f + random.NextSingle() * 0.004f,
                Color.FromHsv(new Vector4(0.99f, 0.85f, 0.4f + random.NextSingle() * 0.4f, 1f)));
        }
    }

    /// <summary>
    /// The round-end portrait of the character to throw around.
    /// </summary>
    public void SetCharacter(EntityUid? snapshot)
    {
        _character = snapshot;
    }

    /// <summary>
    /// The hero's kills and assists and the two teammates shown next to them.
    /// </summary>
    public void SetCast(RoundEndCreditsShowcaseCast cast, RoundEndDollPreviewSystem previews, int owner)
    {
        _kills = cast.Kills;
        _assists = cast.Assists;
        _hasStats = true;
        _contenders.Clear();
        foreach (var data in cast.Contenders)
        {
            var contender = new Contender(data.Name, data.Kills, data.Assists);
            _contenders.Add(contender);
            if (data.Doll is { } doll)
                previews.Request(owner, doll, snapshot => contender.Snapshot = snapshot);
        }
    }

    public void Play()
    {
        _time = 0f;
        _sounds = 0;
        _playing = true;
        Visible = true;
        StartMusic();
    }

    public void Stop()
    {
        _playing = false;
        Visible = false;
        _music = _audio.Stop(_music);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _music = _audio.Stop(_music);
    }

    private void StartMusic()
    {
        _music = _audio.Stop(_music);
        var music = _audio.ResolveSound(new SoundPathSpecifier(Music));
        var length = (float) _audio.GetAudioLength(music).TotalSeconds;
        var audio = AudioParams.Default.WithVolume(MusicVolume).WithPlayOffset(length * MusicStartShare);
        _music = _audio.PlayGlobal(music, Filter.Local(), false, audio)?.Entity;
    }

    /// <summary>
    /// One-off sounds, each played once when the scene reaches its moment.
    /// </summary>
    private void UpdateSounds()
    {
        PlayOnce(0, GeyserStart, BloodBurst);
        PlayOnce(1, GeyserStart + 0.15f, BloodGush);
        PlayOnce(2, GeyserStart + 0.7f, BloodSpray);
        PlayOnce(3, GeyserStart + 1.3f, BloodGush);
        PlayOnce(4, FlipEnd, Landing);
    }

    private void PlayOnce(int index, float at, SoundSpecifier sound)
    {
        var bit = 1 << index;
        if (_time < at || (_sounds & bit) != 0)
            return;

        _sounds |= bit;
        _audio.PlayGlobal(sound, Filter.Local(), false);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_playing)
            return;

        _time += args.DeltaSeconds;
        var fade = Math.Min(Phase(0f, FadeIn), 1f - Phase(SceneLength - FadeOut, SceneLength));
        Modulate = Color.White.WithAlpha(fade);
        UpdateSounds();
        if (_music != null)
            _audio.SetGain(_music, Math.Min(Phase(0f, 1.5f), fade));

        if (_time < SceneLength)
            return;

        Stop();
        var finished = Finished;
        Finished = null;
        finished?.Invoke();
    }

    #region Timeline

    private float Phase(float from, float to)
    {
        return Math.Clamp((_time - from) / (to - from), 0f, 1f);
    }

    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    private static float EaseInOut(float x) => x * x * (3f - 2f * x);

    private float H => PixelSize.Y;

    private float W => PixelSize.X;

    private Vector2 MoonSize(Texture moon)
    {
        var height = H * MoonHeight;
        return new Vector2(height * moon.Width / moon.Height, height);
    }

    /// <summary>
    /// Top of the moon sprite: from below the screen up until about half of it is out.
    /// </summary>
    private float MoonTop()
    {
        var height = H * MoonHeight;
        return MathHelper.Lerp(H + height * 0.05f, H - height * 0.5f, EaseOut(Phase(0f, MoonEnd)));
    }

    private Vector2 DomeTop()
    {
        var frame = (int) (_time / MoonFrameDelay) % DomeTops.Length;
        return new Vector2(W * 0.5f + H * MoonHeight * 0.765f * (DomeTopX - 0.5f),
            MoonTop() + H * MoonHeight * DomeTops[frame] / MoonSpriteHeight);
    }

    private float Scale => H * CharacterHeight / 32f;

    /// <summary>
    /// Where the character's feet are, how far the body leans, which way it faces and how the limbs are posed.
    /// </summary>
    private (Vector2 Feet, float Turn, RsiDirection Facing, Pose Pose) CharacterPose()
    {
        var dome = DomeTop();
        var apex = new Vector2(dome.X, H * Apex + Scale * 32f);
        if (_time < RiseEnd)
        {
            // Thrown up with the back to us, leaning, the tentacle arm raised high.
            var rise = EaseOut(Phase(GeyserStart + 0.3f, RiseEnd));
            var feet = Vector2.Lerp(dome + new Vector2(0f, H * 0.04f), apex, rise);
            var kick = MathF.Sin(_time * 9f) * 0.12f;
            var lean = Lean * EaseOut(Phase(GeyserStart + 0.3f, GeyserStart + 0.9f));
            return (feet, lean, RsiDirection.North, new Pose(ArmUp, ArmRest, 0.1f + kick, 0.1f - kick));
        }

        if (_time < FlipEnd)
        {
            // Falling feet first, arms crossed on the chest, turning half a circle to face us.
            var drop = Phase(RiseEnd, FlipEnd);
            var feet = new Vector2(dome.X, MathHelper.Lerp(apex.Y, dome.Y, drop * drop));
            var turn = EaseInOut(drop);
            var facing = turn < 0.33f ? RsiDirection.North : turn < 0.66f ? RsiDirection.East : RsiDirection.South;
            var cross = EaseOut(Phase(RiseEnd, RiseEnd + 0.3f));
            var right = MathHelper.Lerp(ArmUp, ArmCrossed, cross);
            var left = MathHelper.Lerp(ArmRest, ArmCrossed, cross);
            return (feet, Lean * (1f - turn), facing, new Pose(right, left, 0f, 0f));
        }

        // Landed in a wide stance, arms opening; then the tentacle arm swings out to the side.
        var land = EaseOut(Phase(FlipEnd, FlipEnd + 0.4f));
        var swing = EaseOut(Phase(ArmStart, ArmEnd));
        var breathe = MathF.Sin(_time * 3f) * 0.03f;
        var rest = MathHelper.Lerp(ArmCrossed, ArmRest, land);
        return (dome, -0.06f * swing, RsiDirection.South,
            new Pose(MathHelper.Lerp(rest, ArmOut, swing) + breathe, rest + breathe, 0.3f * land, 0.3f * land));
    }

    private static float EaseOutQuart(float x) => 1f - MathF.Pow(1f - x, 4f);

    /// <summary>
    /// How far the push in has gone: fast at first, settling at the end.
    /// </summary>
    private float Push() => EaseOutQuart(Phase(ZoomStart, ZoomEnd));

    private float Zoom() => MathHelper.Lerp(1f, MaxZoom, Push());

    private Vector2 Focus()
    {
        var center = new Vector2(W, H) / 2f;
        var subject = DomeTop() - new Vector2(0f, Scale * 18f);
        return Vector2.Lerp(center, subject, Push());
    }

    /// <summary>
    /// Scene to screen: the camera looks at <see cref="Focus"/>, closes in, tilts, and jolts on the landing.
    /// </summary>
    private Matrix3x2 Camera()
    {
        var shake = Vector2.Zero;
        var jolt = _time - FlipEnd;
        if (jolt is > 0f and < ShakeLength)
        {
            var strength = (1f - jolt / ShakeLength) * H * 0.012f;
            shake = new Vector2(MathF.Sin(jolt * 90f), MathF.Cos(jolt * 70f)) * strength;
        }

        return Matrix3x2.CreateTranslation(-Focus()) *
               Matrix3x2.CreateScale(Zoom()) *
               Matrix3x2.CreateRotation(Roll * Push()) *
               Matrix3x2.CreateTranslation(new Vector2(W, H) / 2f + shake);
    }

    #endregion

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (!_playing)
            return;

        handle.DrawRect(PixelSizeBox, Color.Black);

        // Everything in the scene is drawn in scene space under the camera; the overlays on top are in screen space.
        var screen = handle.GetTransform();
        var camera = Camera() * screen;
        handle.SetTransform(camera);
        DrawSky(handle);

        // The sky dims and reddens as the moon comes up.
        handle.SetTransform(screen);
        handle.DrawRect(PixelSizeBox, new Color(0.08f, 0f, 0.01f, 0.45f * EaseOut(Phase(0f, MoonEnd))));
        handle.SetTransform(camera);

        DrawGeyser(handle);

        var moon = _sprites.GetFrame(Moon, TimeSpan.FromSeconds(_time));
        var moonSize = MoonSize(moon);
        handle.DrawTextureRect(moon, UIBox2.FromDimensions(new Vector2((W - moonSize.X) / 2f, MoonTop()), moonSize));

        DrawContenderFigures(handle, camera);

        if (_time >= GeyserStart + 0.3f)
            DrawCharacter(handle, camera);

        DrawSplash(handle);

        handle.SetTransform(screen);
        DrawVignette(handle);
        DrawTitle(handle);
        DrawContenderLabels(handle, screen);
    }

    private void DrawSky(DrawingHandleScreen handle)
    {
        // Slow drift, and a margin wide enough that neither the drift nor the tilted camera shows the edge.
        var drift = new Vector2(_time * H * 0.006f, 0f);
        var margin = new Vector2(W, H) * 0.35f;
        var box = new UIBox2(-margin - drift, new Vector2(W, H) + margin - drift);
        foreach (var layer in _sky)
        {
            handle.DrawTextureRect(layer, box);
        }
    }

    #region Light

    /// <summary>
    /// A soft round light: stacked additive discs, faint at the edge and bright in the middle.
    /// </summary>
    private void DrawGlow(DrawingHandleScreen handle, Vector2 center, float radius, Color color, int rings = 20)
    {
        if (radius <= 0f || color.A <= 0f)
            return;

        handle.UseShader(_additive);
        var step = color.A / rings;
        for (var i = 0; i < rings; i++)
        {
            var share = 1f - (float) i / rings;
            handle.DrawCircle(center, radius * share, color.WithAlpha(step));
        }

        handle.UseShader(null);
    }

    private void DrawVignette(DrawingHandleScreen handle)
    {
        const int steps = 14;
        var depth = MathF.Min(W, H) * 0.22f;
        for (var i = 0; i < steps; i++)
        {
            var inset = depth * i / steps;
            var alpha = 0.07f * (1f - (float) i / steps);
            var color = Color.Black.WithAlpha(alpha);
            handle.DrawRect(new UIBox2(0f, 0f, W, inset + 1f), color);
            handle.DrawRect(new UIBox2(0f, H - inset - 1f, W, H), color);
            handle.DrawRect(new UIBox2(0f, 0f, inset + 1f, H), color);
            handle.DrawRect(new UIBox2(W - inset - 1f, 0f, W, H), color);
        }
    }

    /// <summary>
    /// How the character is lit: a dark figure against the blood while thrown up, lit by the moon once on it.
    /// </summary>
    private Color CharacterLight()
    {
        var landed = EaseOut(Phase(FlipEnd - 0.4f, FlipEnd + 0.6f));
        var backlit = new Color(0.45f, 0.32f, 0.32f);
        var lit = new Color(1f, 0.93f, 0.85f);
        return Color.InterpolateBetween(backlit, lit, landed);
    }

    #endregion

    private void DrawGeyser(DrawingHandleScreen handle)
    {
        var origin = DomeTop() + new Vector2(0f, H * 0.05f);
        // Glow first in one additive pass, then the drops themselves, so the shader is switched only twice.
        for (var pass = 0; pass < 2; pass++)
        {
            handle.UseShader(pass == 0 ? _additive : null);
            foreach (var particle in _geyser)
            {
                var age = _time - particle.Born;
                if (age < 0f || age > GeyserLife)
                    continue;

                var point = origin + particle.Velocity * H * age + new Vector2(0f, H * 0.55f) * age * age;
                var alpha = 1f - age / GeyserLife;
                var radius = particle.Radius * H * (1f + age);
                if (pass == 0)
                    handle.DrawCircle(point, radius * 2.6f, BloodLight.WithAlpha(0.07f * alpha));
                else
                    handle.DrawCircle(point, radius, particle.Color.WithAlpha(alpha));
            }
        }

        handle.UseShader(null);

        // The hot base of the jet, right behind the dome.
        var jet = 1f - Phase(GeyserStart + GeyserLength, GeyserStart + GeyserLength + 0.8f);
        var start = Phase(GeyserStart, GeyserStart + 0.25f);
        DrawGlow(handle, origin, H * 0.14f, BloodLight.WithAlpha(0.5f * jet * start));
    }

    private void DrawSplash(DrawingHandleScreen handle)
    {
        var origin = DomeTop();
        foreach (var particle in _splash)
        {
            var age = _time - particle.Born;
            if (age < 0f || age > 0.9f)
                continue;

            var point = origin + particle.Velocity * H * age + new Vector2(0f, H * 0.4f) * age * age;
            handle.DrawCircle(point, particle.Radius * H, particle.Color.WithAlpha(1f - age / 0.9f));
        }
    }

    private void DrawTitle(DrawingHandleScreen handle)
    {
        var alpha = Phase(TitleStart, TitleStart + 0.8f);
        if (alpha <= 0f)
            return;

        var scale = H / 1080f * 1.6f;
        var title = Loc.GetString("architect-mvp-title");
        var titleSize = handle.GetDimensions(_titleFont, title, scale);
        DrawGlow(handle, new Vector2(W / 2f, H * 0.08f + titleSize.Y / 2f), titleSize.X * 0.7f, MoonLight.WithAlpha(0.18f * alpha), 14);
        handle.DrawString(_titleFont, new Vector2((W - titleSize.X) / 2f, H * 0.08f), title, scale, Color.FromHex("#e8c66a").WithAlpha(alpha));

        if (CharacterName.Length == 0)
            return;

        var nameSize = handle.GetDimensions(_nameFont, CharacterName, scale);
        var nameY = H * 0.08f + titleSize.Y * 1.1f;
        handle.DrawString(_nameFont, new Vector2((W - nameSize.X) / 2f, nameY), CharacterName, scale,
            Color.White.WithAlpha(alpha));

        if (!_hasStats)
            return;

        var stats = Loc.GetString("architect-mvp-stats", ("kills", _kills), ("assists", _assists));
        var statsSize = handle.GetDimensions(_nameFont, stats, scale * 0.8f);
        handle.DrawString(_nameFont, new Vector2((W - statsSize.X) / 2f, nameY + nameSize.Y * 1.15f), stats, scale * 0.8f,
            Color.FromHex("#c9a0a0").WithAlpha(alpha));
    }

    /// <summary>
    /// Where a contender kneels: on the dome to the left (first) or right (second) of the Architect,
    /// following the dome's curve and leaning with it.
    /// </summary>
    private (Vector2 Feet, float Tilt) ContenderPlace(int index)
    {
        var dome = DomeTop();
        var radius = H * MoonHeight * 0.765f * DomeRadius;
        var x = (index == 0 ? -1f : 1f) * ContenderSpacing * Scale;
        var drop = radius - MathF.Sqrt(MathF.Max(0f, radius * radius - x * x));
        return (dome + new Vector2(x, drop), MathF.Asin(Math.Clamp(x / radius, -1f, 1f)));
    }

    private void DrawContenderFigures(DrawingHandleScreen handle, Matrix3x2 camera)
    {
        _light = CharacterLight();
        for (var i = 0; i < _contenders.Count && i < 2; i++)
        {
            var launch = ContenderLaunch + ContenderLaunchGap * i;
            if (_time < launch ||
                _contenders[i].Snapshot is not { } snapshot ||
                !_entities.TryGetComponent<SpriteComponent>(snapshot, out var sprite))
            {
                continue;
            }

            CollectLayers(sprite, RsiDirection.South, _contenderLayers);
            var (spot, tilt) = ContenderPlace(i);
            if (_time >= FlipEnd)
            {
                DrawKneeling(handle, camera, spot, tilt, Scale, _contenderLayers);
                continue;
            }

            // In the air: up with the jet, out to their side, then down onto their spot right as the Architect lands.
            var side = i == 0 ? -1f : 1f;
            var dome = DomeTop();
            var apex = new Vector2(dome.X + side * ContenderSpacing * Scale * 2.2f, H * ContenderApex + Scale * 32f);
            Vector2 feet;
            float turn;
            if (_time < launch + ContenderRiseLength)
            {
                var rise = EaseOut(Phase(launch, launch + ContenderRiseLength));
                feet = Vector2.Lerp(dome + new Vector2(0f, H * 0.04f), apex, rise);
                turn = side * 0.12f * rise;
            }
            else
            {
                var drop = Phase(launch + ContenderRiseLength, FlipEnd);
                feet = Vector2.Lerp(apex, spot, drop * drop);
                turn = MathHelper.Lerp(side * 0.12f, tilt, EaseInOut(drop));
            }

            DrawTucked(handle, camera, feet, turn, Scale, _contenderLayers);
        }

        handle.SetTransform(camera);
    }

    /// <summary>
    /// In the air on the way down: one leg straight, the knee they will kneel on pulled up to the body,
    /// arms held a little out to the sides.
    /// </summary>
    private void DrawTucked(DrawingHandleScreen handle, Matrix3x2 camera, Vector2 feet, float tilt, float scale,
        List<(Texture Texture, Color Color, Vector2 TopLeft)> layers)
    {
        var center = feet + Rotate(new Vector2(0f, -14f), tilt) * scale;
        var body = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(tilt) * Matrix3x2.CreateTranslation(center);
        var tucked = ViewerRightLeg with { Box = new UIBox2(16, 24, 32, 28) };

        _drawLayers = layers;
        DrawPiece(handle, camera, body, tucked, 0.15f, RsiDirection.South, shift: new Vector2(0f, -2f));
        DrawPiece(handle, camera, body, ViewerLeftLeg, 0.05f, RsiDirection.South);
        DrawPiece(handle, camera, body, Torso, 0f, RsiDirection.South);
        DrawPiece(handle, camera, body, ViewerRightArm, 0.35f, RsiDirection.South);
        DrawPiece(handle, camera, body, Head, 0f, RsiDirection.South);
        DrawPiece(handle, camera, body, ViewerLeftArm, 0.35f, RsiDirection.South);
        _drawLayers = _layers;
    }

    /// <summary>
    /// A figure down on one knee: the body sinks, one knee is on the ground with the shin folded under,
    /// the other foot stays planted (the lowered torso covers its thigh, so that knee reads as raised) with an arm on it.
    /// </summary>
    private void DrawKneeling(DrawingHandleScreen handle, Matrix3x2 camera, Vector2 feet, float tilt, float scale,
        List<(Texture Texture, Color Color, Vector2 TopLeft)> layers)
    {
        var center = feet + Rotate(new Vector2(0f, -14f), tilt) * scale;
        var body = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(tilt) * Matrix3x2.CreateTranslation(center);
        var sink = new Vector2(0f, 5f);
        var folded = ViewerRightLeg with { Box = new UIBox2(16, 24, 32, 27) };

        _drawLayers = layers;
        DrawPiece(handle, camera, body, folded, 0f, RsiDirection.South, shift: sink);
        DrawPiece(handle, camera, body, ViewerLeftLeg, 0.12f, RsiDirection.South);
        DrawPiece(handle, camera, body, Torso, 0f, RsiDirection.South, shift: sink);
        DrawPiece(handle, camera, body, ViewerRightArm, 0.1f, RsiDirection.South, shift: sink);
        DrawPiece(handle, camera, body, Head, 0f, RsiDirection.South, shift: sink);
        DrawPiece(handle, camera, body, ViewerLeftArm, -0.25f, RsiDirection.South, shift: sink);
        _drawLayers = _layers;
    }

    private void DrawContenderLabels(DrawingHandleScreen handle, Matrix3x2 screen)
    {
        var alpha = Phase(LabelsStart, LabelsStart + 0.8f);
        if (alpha <= 0f)
            return;

        var textScale = H / 1080f * 1.3f;
        for (var i = 0; i < _contenders.Count && i < 2; i++)
        {
            var contender = _contenders[i];
            var x = i == 0 ? W * 0.2f : W * 0.8f;
            var y = H * 0.1f;

            var label = Loc.GetString("architect-mvp-contender");
            DrawCentered(handle, _smallFont, label, x, y, textScale, Color.FromHex("#e8c66a").WithAlpha(alpha));
            y += handle.GetDimensions(_smallFont, label, textScale).Y * 1.1f;
            DrawCentered(handle, _nameFont, contender.Name, x, y, textScale * 0.85f, Color.White.WithAlpha(alpha));
            y += handle.GetDimensions(_nameFont, contender.Name, textScale * 0.85f).Y * 1.05f;
            var stats = Loc.GetString("architect-mvp-stats", ("kills", contender.Kills), ("assists", contender.Assists));
            DrawCentered(handle, _smallFont, stats, x, y, textScale, Color.FromHex("#c9a0a0").WithAlpha(alpha));
        }
    }

    private static void DrawCentered(DrawingHandleScreen handle, Font font, string text, float x, float y, float scale, Color color)
    {
        var size = handle.GetDimensions(font, text, scale);
        handle.DrawString(font, new Vector2(x - size.X / 2f, y), text, scale, color);
    }

    private sealed class Contender(string name, int kills, int assists)
    {
        public readonly string Name = name;
        public readonly int Kills = kills;
        public readonly int Assists = assists;
        public EntityUid? Snapshot;
    }

    #region Character

    private void DrawCharacter(DrawingHandleScreen handle, Matrix3x2 camera)
    {
        if (_character is not { } character || !_entities.TryGetComponent<SpriteComponent>(character, out var sprite))
            return;

        var (feet, turn, facing, pose) = CharacterPose();
        CollectLayers(sprite, facing, _layers);

        // A humanoid's feet are at the sprite's bottom rows, 14 px below its centre.
        var scale = Scale;
        var center = feet + Rotate(new Vector2(0f, -14f), turn) * scale;
        var body = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateRotation(turn) * Matrix3x2.CreateTranslation(center);

        // Red light behind the Architect, from the blood.
        DrawGlow(handle, center, scale * 26f, BloodLight.WithAlpha(0.3f), 16);
        _light = CharacterLight();
        _drawLayers = _layers;

        var old = camera;
        switch (facing)
        {
            case RsiDirection.South:
                // Facing us, the character's right side is on the viewer's left.
                DrawPiece(handle, old, body, ViewerRightLeg, pose.LeftLeg, facing);
                DrawPiece(handle, old, body, ViewerLeftLeg, pose.RightLeg, facing);
                DrawPiece(handle, old, body, Torso, 0f, facing);
                DrawPiece(handle, old, body, ViewerRightArm, pose.LeftArm, facing);
                DrawPiece(handle, old, body, Head, 0f, facing);
                DrawPiece(handle, old, body, ViewerLeftArm, pose.RightArm, facing, withArm: true);
                break;
            case RsiDirection.North:
                // Seen from behind the arms go first, so the body covers whatever crosses it.
                DrawPiece(handle, old, body, ViewerRightArm, pose.RightArm, facing, withArm: true);
                DrawPiece(handle, old, body, ViewerLeftArm, pose.LeftArm, facing);
                DrawPiece(handle, old, body, ViewerRightLeg, pose.RightLeg, facing);
                DrawPiece(handle, old, body, ViewerLeftLeg, pose.LeftLeg, facing);
                DrawPiece(handle, old, body, Torso, 0f, facing);
                DrawPiece(handle, old, body, Head, 0f, facing);
                break;
            default:
                // Side view: the limbs overlap the body, so the sprite turns as one piece.
                DrawPiece(handle, old, body, Whole, 0f, facing, withArm: true);
                break;
        }

        handle.SetTransform(old);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        return Vector2.Transform(v, Matrix3x2.CreateRotation(angle));
    }

    /// <summary>
    /// Front-facing textures of every visible layer, each placed so the sprite is centred on (16, 16).
    /// </summary>
    private static void CollectLayers(SpriteComponent sprite, RsiDirection facing,
        List<(Texture Texture, Color Color, Vector2 TopLeft)> layers)
    {
        layers.Clear();
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible)
                continue;

            Texture? texture = layer.Texture;
            if (texture == null && layer.ActualRsi is { } rsi && rsi.TryGetState(layer.RsiState, out var state))
                texture = state.GetFrame(state.RsiDirections == RsiDirectionType.Dir1 ? RsiDirection.South : facing, 0);

            if (texture == null)
                continue;

            var offset = layer is SpriteComponent.Layer concrete ? concrete.Offset * 32f : Vector2.Zero;
            var topLeft = new Vector2(16f - texture.Width / 2f + offset.X, 16f - texture.Height / 2f - offset.Y);
            layers.Add((texture, layer.Color * sprite.Color, topLeft));
        }
    }

    private void DrawPiece(DrawingHandleScreen handle, Matrix3x2 old, Matrix3x2 body, Piece piece, float angle,
        RsiDirection facing, bool withArm = false, Vector2 shift = default)
    {
        // Pieces are drawn around the sprite centre; limbs turn around their joint first, then the piece may be moved.
        var pivot = piece.Joint - new Vector2(16f, 16f);
        var limb = Matrix3x2.CreateTranslation(-pivot) * Matrix3x2.CreateRotation(angle * piece.Sign) *
                   Matrix3x2.CreateTranslation(pivot + shift);
        handle.SetTransform(limb * body * old);

        foreach (var (texture, color, topLeft) in _drawLayers)
        {
            var left = MathF.Max(piece.Box.Left, topLeft.X);
            var top = MathF.Max(piece.Box.Top, topLeft.Y);
            var right = MathF.Min(piece.Box.Right, topLeft.X + texture.Width);
            var bottom = MathF.Min(piece.Box.Bottom, topLeft.Y + texture.Height);
            if (right <= left || bottom <= top)
                continue;

            var region = new UIBox2(left - topLeft.X, top - topLeft.Y, right - topLeft.X, bottom - topLeft.Y);
            handle.DrawTextureRectRegion(texture, new UIBox2(left - 16f, top - 16f, right - 16f, bottom - 16f), region, color * _light);
        }

        // The Architect's tentacle is worn on the right hand, so it moves with that arm.
        if (withArm && _armState != null)
        {
            var frame = _armState.GetFrame(facing, (int) (_time / 0.2f) % Math.Max(1, _armState.DelayCount));
            handle.DrawTextureRect(frame, new UIBox2(-frame.Width / 2f, -frame.Height / 2f, frame.Width / 2f, frame.Height / 2f), _light);
        }
    }

    #endregion

    private readonly record struct Piece(UIBox2 Box, Vector2 Joint, float Sign);

    private readonly record struct Pose(float RightArm, float LeftArm, float RightLeg, float LeftLeg);

    private readonly record struct Particle(float Born, Vector2 Velocity, float Radius, Color Color);
}
