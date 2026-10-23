// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

public enum LavalandAshWolfStage : byte
{
    /// <summary>Lying where the storm left it, until someone hits it.</summary>
    Dormant,

    /// <summary>Hit: it twitches a few seconds before the scene.</summary>
    Waking,
    Intro,
    Fight,

    /// <summary>Beaten: it shakes, sparks and blows up a little, then falls and becomes a borg.</summary>
    Dying,
}

public enum LavalandAshWolfAttack : byte
{
    None,
    Rush,
    Shield,
    Leap,
    Sky,
    Retreat,
    Pounce,
}

/// <summary>What happens to whoever a rush or a pounce lands on, once the parry window is over.</summary>
public enum LavalandAshWolfContact : byte
{
    None,
    Rush,
    Pounce,
}

/// <summary>
/// AshWolf LQ-829, the abandoned borg of a secret clan: the secret boss the strong storm brings to the miners'
/// outpost. Tamed (<see cref="Tamed"/>), it is the borg it becomes after the fight: no mind of its own, only the
/// actions of its rush, leap, retreat and pounce for whoever is put in it.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandAshWolfComponent : Component
{
    #region Settings

    [DataField]
    public bool Tamed;

    [DataField]
    public float MaxHealth = 2200f;

    /// <summary>Below this share of its health its rushes come with shadow copies.</summary>
    [DataField]
    public float SecondPhase = 0.45f;

    [DataField]
    public LocId Name = "lavaland-ash-wolf-hud-name";

    [DataField]
    public LocId Hint = "lavaland-ash-wolf-hud-hint";

    [DataField]
    public LocId Subtitle = "lavaland-ash-wolf-subtitle";

    [DataField]
    public float ChaseRange = 26f;

    /// <summary>Frenzied (the strong storm has died down): everything it does is this much faster.</summary>
    [DataField]
    public float FrenzyHaste = 1.8f;

    /// <summary>Frenzied, it sees the players from this far.</summary>
    [DataField]
    public float FrenzyChaseRange = 60f;

    /// <summary>Frenzied, the breath between two attacks.</summary>
    [DataField]
    public float FrenzyPause = 0.2f;

    [DataField]
    public float WatchRange = 16f;

    [DataField]
    public float WalkSpeed = 4.4f;

    [DataField]
    public TimeSpan WakeDelay = TimeSpan.FromSeconds(3);

    /// <summary>Nobody in reach this long: the fight is over, its health bar goes.</summary>
    [DataField]
    public TimeSpan LoseInterest = TimeSpan.FromSeconds(20);

    [DataField]
    public float ParryGrace = 0.25f;

    [DataField]
    public float RushSpeed = 15f;

    [DataField]
    public float RushWindup = 0.9f;

    /// <summary>How high it leaps when it rushes at a player, tiles.</summary>
    [DataField]
    public float RushHop = 1.1f;

    /// <summary>After each of its patterns it is stunned (stars over its head) for this long.</summary>
    [DataField]
    public float PatternStun = 1f;

    [DataField]
    public float RushOvershoot = 2.5f;

    [DataField]
    public float KnockbackDistance = 3f;

    [DataField]
    public DamageSpecifier GrindDamage = new() { DamageDict = { ["Slash"] = 5 } };

    [DataField]
    public int GrindTicks = 6;

    [DataField]
    public float GrindInterval = 0.25f;

    [DataField]
    public float ShieldDuration = 10f;

    [DataField]
    public float ShieldReduction = 0.55f;

    [DataField]
    public float ShieldBlinkInterval = 1.4f;

    /// <summary>How fast it dashes behind a back under the shield, tiles a second.</summary>
    [DataField]
    public float ShieldDashSpeed = 24f;

    [DataField]
    public DamageSpecifier ShieldSlashDamage = new() { DamageDict = { ["Slash"] = 12 } };

    [DataField]
    public float LeapWarning = 2f;

    [DataField]
    public float LeapRise = 0.45f;

    [DataField]
    public float LeapFall = 0.35f;

    [DataField]
    public float LeapStamina = 65f;

    [DataField]
    public float LeapStaminaRadius = 5f;

    [DataField]
    public float LeapShakeRadius = 10f;

    [DataField]
    public DamageSpecifier LeapDamage = new() { DamageDict = { ["Blunt"] = 20 } };

    [DataField]
    public float SkyDuration = 5f;

    [DataField]
    public float SkySawSpeed = 3.6f;

    [DataField]
    public DamageSpecifier SkySawDamage = new() { DamageDict = { ["Slash"] = 15 } };

    /// <summary>A sky saw parried back into it.</summary>
    [DataField]
    public DamageSpecifier ReflectedSawDamage = new() { DamageDict = { ["Slash"] = 15 } };

    [DataField]
    public float RetreatDistance = 4f;

    [DataField]
    public float RetreatSawSpeed = 8f;

    [DataField]
    public DamageSpecifier RetreatSawDamage = new() { DamageDict = { ["Slash"] = 15 } };

    [DataField]
    public float PounceSpeed = 13f;

    [DataField]
    public float PounceShield = 0.9f;

    [DataField]
    public float QteDuration = 5f;

    /// <summary>The client's answer may come this late (its ping).</summary>
    [DataField]
    public float QteGrace = 1.5f;

    [DataField]
    public DamageSpecifier QteSuccessDamage = new() { DamageDict = { ["Blunt"] = 50 } };

    [DataField]
    public float QteKnockback = 5f;

    [DataField]
    public float QteStun = 2.5f;

    [DataField]
    public TimeSpan PounceCooldown = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan SkyCooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public EntProtoId Saw = "LavalandAshWolfSaw";

    [DataField]
    public EntProtoId Shadow = "LavalandAshWolfShadow";

    [DataField]
    public EntProtoId Dust = "LavalandAshWolfDust";

    [DataField]
    public EntProtoId LeapMark = "LavalandAshWolfLeapMark";

    [DataField]
    public EntProtoId SkyMark = "LavalandAshWolfSkyMark";

    [DataField]
    public EntProtoId Landing = "LavalandAshDrakeLanding";

    [DataField]
    public EntProtoId Sparks = "EffectSparks";

    [DataField]
    public EntProtoId Borg = "BorgChassisAshWolf";

    /// <summary>The borg is left broken by the fight: past its critical threshold, to be welded back to life.</summary>
    [DataField]
    public DamageSpecifier BorgDamage = new() { DamageDict = { ["Blunt"] = 130 } };

    [DataField]
    public List<SoundSpecifier> Music = new();

    [DataField]
    public SoundSpecifier? RushSound = new SoundPathSpecifier("/Audio/Effects/metal_scrape1.ogg");

    [DataField]
    public SoundSpecifier? SawSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    [DataField]
    public SoundSpecifier? LandSound = new SoundPathSpecifier("/Audio/Effects/metal_crunch.ogg");

    [DataField]
    public SoundSpecifier? BlastSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");

    [DataField]
    public SoundSpecifier? WakeSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    #endregion

    #region State

    [ViewVariables]
    public LavalandAshWolfStage Stage = LavalandAshWolfStage.Dormant;

    [ViewVariables]
    public TimeSpan StageTime;

    [ViewVariables]
    public bool Active;

    [ViewVariables]
    public TimeSpan LastSeenPlayer;

    [ViewVariables]
    public bool SecondPhaseReached;

    /// <summary>The strong storm that brought it has died down, and it fights as if at its last breath.</summary>
    [ViewVariables]
    public bool Frenzied;

    [ViewVariables]
    public LavalandAshWolfAttack Attack;

    [ViewVariables]
    public LavalandAshWolfAttack LastAttack;

    [ViewVariables]
    public int Step;

    [ViewVariables]
    public TimeSpan StepTime;

    [ViewVariables]
    public TimeSpan AttackEnd;

    [ViewVariables]
    public TimeSpan NextAttack;

    [ViewVariables]
    public TimeSpan NextPounce;

    [ViewVariables]
    public TimeSpan NextSky;

    [ViewVariables]
    public EntityUid? Target;

    /// <summary>Damage taken this share less (the shield, the pounce); 1 - none at all.</summary>
    [ViewVariables]
    public float Reduction;

    /// <summary>Up in the sky, or in the scene: nothing touches it but its own parried saws.</summary>
    [ViewVariables]
    public bool Untouchable;

    /// <summary>Stunned after a parry or a lost quick time event: idle, and a blow knocks it back.</summary>
    [ViewVariables]
    public TimeSpan StaggerUntil;

    /// <summary>The stars over its head are shown.</summary>
    [ViewVariables]
    public bool SeeingStars;

    /// <summary>When the next flames rise from under its feet in the rush's windup.</summary>
    [ViewVariables]
    public TimeSpan NextEmber;

    /// <summary>The slide is a leap: its sprite arcs up, and comes back down when the slide ends.</summary>
    [ViewVariables]
    public bool SlideHop;

    /// <summary>The second phase: a parried rush leaves it open, and only a blow then knocks it away.</summary>
    [ViewVariables]
    public bool KnockOnHit;

    // The slide it is on (rush, jump back, knockback).
    [ViewVariables]
    public bool Sliding;

    [ViewVariables]
    public Vector2 SlideFrom;

    [ViewVariables]
    public Vector2 SlideTo;

    [ViewVariables]
    public TimeSpan SlideStart;

    [ViewVariables]
    public float SlideDuration;

    /// <summary>A rush or pounce stops at the first one it meets.</summary>
    [ViewVariables]
    public LavalandAshWolfContact SlideContact;

    [ViewVariables]
    public EntityUid? Victim;

    [ViewVariables]
    public TimeSpan ContactResolve;

    [ViewVariables]
    public int GrindLeft;

    [ViewVariables]
    public TimeSpan NextGrind;

    [ViewVariables]
    public TimeSpan NextBlink;

    [ViewVariables]
    public TimeSpan PendingSlash;

    /// <summary>It is dashing behind a back under the shield.</summary>
    [ViewVariables]
    public bool ShieldDashing;

    [ViewVariables]
    public int QteId;

    [ViewVariables]
    public TimeSpan QteDeadline;

    [ViewVariables]
    public bool? QteResult;

    [ViewVariables]
    public readonly List<EntityUid> SkySaws = new();

    [ViewVariables]
    public readonly List<EntityUid> Shadows = new();

    [ViewVariables]
    public readonly List<EntityUid> Watchers = new();

    [ViewVariables]
    public TimeSpan NextSpark;

    [ViewVariables]
    public TimeSpan NextBlast;

    [ViewVariables]
    public int HudId;

    [ViewVariables]
    public TimeSpan NextHud;

    [ViewVariables]
    public SoundSpecifier? SelectedMusic;

    public readonly HashSet<ICommonSession> HudRecipients = new();

    /// <summary>Tamed: where its action was aimed.</summary>
    [ViewVariables]
    public Vector2 AimPoint;

    #endregion
}

public enum LavalandAshWolfSawKind : byte
{
    /// <summary>Spirals out from a landing; sticks in whoever it meets for a while, then goes on.</summary>
    Spiral,

    /// <summary>Falls from the sky and chases its player; a parry sends it back into the wolf.</summary>
    Homing,

    /// <summary>Flies straight (the retreat), cutting whoever stands in its way.</summary>
    Straight,

    /// <summary>Parried: flies back into the wolf.</summary>
    Returning,
}

/// <summary>A saw of the Ash Wolf; moved by the server each tick, it cuts rock and ore in its way.</summary>
[RegisterComponent]
public sealed partial class LavalandAshWolfSawComponent : Component
{
    [ViewVariables]
    public EntityUid Wolf;

    [ViewVariables]
    public LavalandAshWolfSawKind Kind;

    [ViewVariables]
    public TimeSpan Start;

    [ViewVariables]
    public TimeSpan End;

    [ViewVariables]
    public Vector2 Origin;

    [ViewVariables]
    public Vector2 Velocity;

    [ViewVariables]
    public float Angle;

    [ViewVariables]
    public EntityUid? Chasing;

    /// <summary>Stuck in someone: ticks of damage left, and when the next comes.</summary>
    [ViewVariables]
    public EntityUid? StuckIn;

    [ViewVariables]
    public int StuckTicks;

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>A homing saw met its player: parried or cut, decided after the grace.</summary>
    [ViewVariables]
    public TimeSpan? ContactResolve;

    [ViewVariables]
    public TimeSpan NextHit;

    [ViewVariables]
    public readonly HashSet<EntityUid> Hit = new();
}

/// <summary>A shadow copy of the rushing wolf (second phase): it only looks the part.</summary>
[RegisterComponent]
public sealed partial class LavalandAshWolfShadowComponent : Component
{
    [ViewVariables]
    public Vector2 From;

    [ViewVariables]
    public Vector2 To;

    [ViewVariables]
    public TimeSpan Start;

    [ViewVariables]
    public float Duration;
}
