// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland.Bosses;

/// <summary>How the boss comes into its arena in its intro.</summary>
[Serializable, NetSerializable]
public enum LavalandBossIntroStyle : byte
{
    /// <summary>Ash drake: the players look around, a roar, it falls from the sky in fire and smoke.</summary>
    Fall,

    /// <summary>Bubblegum: blood runs from every corner into its mark, a hand digs out, a shadow flies up, it falls.</summary>
    BloodRitual,

    /// <summary>Colossus: a blast throws the players into the corners, a great blast leaves it standing enraged.</summary>
    Blast,

    /// <summary>Hierophant: everyone gathers in the middle, its warning flares under them, it rolls in and bowls them over.</summary>
    Bowling,

    /// <summary>Legion: something hammers the opening gate, the doors fly apart, it spins in the middle.</summary>
    GateBreak,
}

/// <summary>
/// The boss is introduced once, when the fight would start (the Legion: when its gate is knocked on).
/// The players are frozen and cannot be hurt meanwhile; the fight starts when the scene ends.
/// Times are seconds from the start of the scene.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class LavalandBossIntroConfig
{
    [DataField]
    public LavalandBossIntroStyle Style = LavalandBossIntroStyle.Fall;

    /// <summary>The whole scene; the fight starts after it.</summary>
    [DataField]
    public float Duration = 9.5f;

    [DataField]
    public float WalkInTime = 1.4f;

    [DataField]
    public float WalkInDistance = 2.5f;

    [DataField]
    public float RoarDelay = 3.3f;

    /// <summary>When the boss comes down (the falling styles).</summary>
    [DataField]
    public float FallDelay = 4.1f;

    [DataField]
    public float FallDuration = 1.1f;

    [DataField]
    public float FallHeight = 14f;

    /// <summary>The still boss with its name.</summary>
    [DataField]
    public float TitleDelay = 5.4f;

    [DataField]
    public float TitleDuration = 2.6f;

    [DataField]
    public Color TitleColor = Color.FromHex("#ff7a2e");

    /// <summary>At the end the camera goes back to the players in this long.</summary>
    [DataField]
    public float ReturnTime = 1.2f;

    /// <summary>Close to the players: the camera is shifted (tiles) and zoomed in.</summary>
    [DataField]
    public Vector2 CameraOffset = new(-1.6f, 0.4f);

    [DataField]
    public float CameraZoom = 0.55f;

    /// <summary>Zoom while the boss comes in and is shown.</summary>
    [DataField]
    public float ArenaZoom = 1.05f;

    [DataField]
    public SoundSpecifier? RoarSound;

    [DataField]
    public SoundSpecifier? LandSound;

    /// <summary>Blasts, hits on the gate and the like.</summary>
    [DataField]
    public SoundSpecifier? ImpactSound;

    /// <summary>A line under the boss name.</summary>
    [DataField]
    public LocId? Subtitle;
}

/// <summary>Where the camera looks from a moment of the scene on.</summary>
[Serializable, NetSerializable]
public struct LavalandIntroShot(float time, bool onPlayer, Vector2 point, float zoom)
{
    public float Time = time;

    /// <summary>On the own doll of the viewer (<see cref="Point"/> is then an offset from it).</summary>
    public bool OnPlayer = onPlayer;

    public Vector2 Point = point;
    public float Zoom = zoom;
}

[Serializable, NetSerializable]
public enum LavalandIntroDollAction : byte
{
    /// <summary>Back to normal.</summary>
    None,

    /// <summary>Walks in from <c>Vector</c> (offset) to its place in <c>Length</c> seconds.</summary>
    WalkIn,

    LookAround,
    LookUp,

    /// <summary>Lies on the ground.</summary>
    Lie,

    /// <summary>Gets up from lying in <c>Length</c> seconds.</summary>
    StandUp,

    /// <summary>Backs off by <c>Vector</c> in <c>Length</c> seconds, facing the other way.</summary>
    StepBack,
}

/// <summary>What the dolls of the players do from a moment of the scene on.</summary>
[Serializable, NetSerializable]
public struct LavalandIntroDollCue(float time, LavalandIntroDollAction action, Vector2 vector = default, float length = 0f)
{
    public float Time = time;
    public LavalandIntroDollAction Action = action;
    public Vector2 Vector = vector;
    public float Length = length;
}

/// <summary>Plays a boss intro for the players watching it.</summary>
[Serializable, NetSerializable]
public sealed class LavalandBossIntroEvent : EntityEventArgs
{
    /// <summary>Everyone frozen for the scene: their dolls act it out.</summary>
    public List<NetEntity> Watchers = new();

    public string Title = string.Empty;
    public string? Subtitle;
    public Color TitleColor = Color.White;
    public float Duration;
    public float TitleDelay;
    public float TitleDuration;
    public float ReturnTime;
    public float StartZoom = 1f;

    public List<LavalandIntroShot> Shots = new();
    public List<LavalandIntroDollCue> Dolls = new();

    /// <summary>Screen shakes: X is the time, Y the strength in tiles.</summary>
    public List<Vector2> Shakes = new();

    /// <summary>
    /// Everything seen in the scene but the players: particles, blasts, blood, the boss coming in. The client
    /// spawns them itself on the clock of the scene, so they are on time whatever the lag; the real boss only
    /// appears when the scene ends, where its copy stands.
    /// </summary>
    public List<LavalandIntroFx> Effects = new();

    /// <summary>Sounds of the scene, played by the client on the clock of the scene.</summary>
    public List<LavalandIntroSound> Sounds = new();
}

[Serializable, NetSerializable]
public struct LavalandIntroSound(float time, SoundSpecifier sound)
{
    public float Time = time;
    public SoundSpecifier Sound = sound;
}

/// <summary>A thing of an intro spawned by the client itself at its moment of the scene.</summary>
[Serializable, NetSerializable]
public struct LavalandIntroFx(
    float time,
    string prototype,
    Vector2 position,
    Vector2 from = default,
    Vector2 to = default,
    float duration = 0f,
    float arc = 0f,
    float spin = 0f,
    bool fadeOut = false,
    bool fadeIn = false)
{
    /// <summary>Seconds into the scene.</summary>
    public float Time = time;

    public string Prototype = prototype;

    /// <summary>Map position.</summary>
    public Vector2 Position = position;

    public Vector2 From = from;
    public Vector2 To = to;

    /// <summary>0: no motion, it just stands there.</summary>
    public float Duration = duration;

    public float Arc = arc;
    public float Spin = spin;
    public bool FadeOut = fadeOut;
    public bool FadeIn = fadeIn;

    /// <summary>Falls faster and faster (from the sky).</summary>
    public bool Accelerate;

    /// <summary>Removed after this long (0: its prototype decides).</summary>
    public float Lifetime;

    /// <summary>Another state for its first sprite layer (the colossus enraged).</summary>
    public string? State;
}

/// <summary>
/// Moves the sprite (not the entity) from <see cref="From"/> to <see cref="To"/>, with an arc, spin and fading:
/// the particles, rolling and flying pieces of the boss intros.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandSpriteMotionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public float Duration = 1f;

    [DataField, AutoNetworkedField]
    public Vector2 From;

    [DataField, AutoNetworkedField]
    public Vector2 To;

    /// <summary>Height of the arc in the middle of the way (tiles), for things thrown up and falling back.</summary>
    [DataField, AutoNetworkedField]
    public float Arc;

    /// <summary>Radians per second.</summary>
    [DataField, AutoNetworkedField]
    public float Spin;

    [DataField, AutoNetworkedField]
    public bool FadeOut;

    /// <summary>Appears out of nothing (the colossus out of the smoke).</summary>
    [DataField, AutoNetworkedField]
    public bool FadeIn;

    /// <summary>Moves faster and faster along the way (falling from the sky).</summary>
    [DataField, AutoNetworkedField]
    public bool Accelerate;
}
