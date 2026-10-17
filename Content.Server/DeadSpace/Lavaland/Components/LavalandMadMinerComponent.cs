// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandMadMinerComponent : Component
{
    [DataField] public float MaxHealth = 1250f;
    [DataField] public float NoticeRange = 7f;
    [DataField] public float ChaseRange = 160f;
    [DataField] public float BlinkDistance = 4.5f;

    /// <summary>
    /// Farther than this from the target the miner does not walk there (no long paths through rock): he jumps closer.
    /// Keep it below the AI vision radius so the AI takes over after the jump.
    /// </summary>
    [DataField] public float CatchUpDistance = 20f;

    /// <summary>How far from the target the catch-up jump lands.</summary>
    [DataField] public float CatchUpLanding = 7f;

    [DataField] public TimeSpan CatchUpCooldown = TimeSpan.FromSeconds(4);
    public TimeSpan NextCatchUp;
    [DataField] public TimeSpan BlinkCooldown = TimeSpan.FromSeconds(1.25);
    [DataField] public TimeSpan BlinkDuration = TimeSpan.FromSeconds(0.45);
    [DataField] public TimeSpan BlinkStallTimeout = TimeSpan.FromSeconds(0.15);
    [DataField] public TimeSpan FeintCooldown = TimeSpan.FromSeconds(3.5);
    [DataField] public TimeSpan ShootCooldown = TimeSpan.FromSeconds(1.4);
    [DataField] public TimeSpan HookCooldown = TimeSpan.FromSeconds(7);
    [DataField] public float HookAttackChance = 0.22f;
    [DataField] public TimeSpan HookDuration = TimeSpan.FromSeconds(5);
    [DataField] public float HookForce = 5f;
    [DataField] public float HookMaxLength = 10f;
    [DataField] public float HookStretchForce = 0.5f;
    [DataField] public float HookRetreatDistance = 9f;
    [DataField] public float HookSplashRadius = 3.5f;
    [DataField] public TimeSpan HookRemoveTime = TimeSpan.FromSeconds(1);
    [DataField] public float HookImpactRange = 1.35f;
    [DataField] public float HookKnockback = 9f;
    [DataField] public float HookRetaliationSpeed = 1.3f;
    [DataField] public float SplitPursuitNearRadius = 8f;
    [DataField] public float SplitPursuitFarDistance = 14f;
    [DataField] public float SplitPursuitDistanceGap = 8f;
    [DataField] public float SplitPursuitVeryFarDistance = 25f;
    [DataField] public TimeSpan SplitPursuitCooldown = TimeSpan.FromSeconds(10);
    [DataField] public DamageSpecifier HookImpactDamage = new();
    [DataField] public EntProtoId Projectile = "LavalandMadMinerKineticBolt";
    [DataField] public EntProtoId HookProjectile = "LavalandMadMinerHookVisual";
    [DataField] public float ProjectileSpeed = 24f;
    [DataField] public List<SoundSpecifier> Music = new();
    [DataField] public SoundSpecifier? BlinkSound;
    [DataField] public SoundSpecifier? HookSound;
    [DataField] public EntProtoId? ChainHandReward = "UnitologyWeaponHandHookImplanter";
    [DataField] public float ChainHandChance = 0.5f;
    [DataField] public EntProtoId? ExplosivePkaReward = "PKAUpgradeExplosiveLavaland";
    [DataField] public float ExplosivePkaChance = 0.85f;
    /// <summary>Rewards that always drop on death.</summary>
    [DataField] public List<EntProtoId> ExtraRewards = new();
    [DataField] public float SpeechRadius = 12f;
    [DataField] public Color SpeechColor = Color.FromHex("#ff3030");
    [DataField] public TimeSpan SpeechCooldownMin = TimeSpan.FromSeconds(6);
    [DataField] public TimeSpan SpeechCooldownMax = TimeSpan.FromSeconds(11);
    [DataField] public float SpeechDuration = 4f;
    [DataField] public List<LocId> SpeechLines = new();
    [DataField] public TimeSpan HudInterval = TimeSpan.FromSeconds(0.5);
    [DataField] public float RageHookDamageMultiplier = 2f;

    // DS14: death is not the end - a while after it falls, it rises again as a necromorph.
    /// <summary>How long it lies dead before it rises.</summary>
    [DataField] public TimeSpan RiseDelay = TimeSpan.FromSeconds(5);
    /// <summary>Health of the necromorph it rises as.</summary>
    [DataField] public float RisenHealth = 175f;
    [DataField] public DamageSpecifier RisenDamage = new() { DamageDict = new() { { "Slash", 22 }, { "Structural", 30 } } };
    [DataField] public float RisenSpeed = 3.6f;
    [DataField] public EntProtoId RisenBlood = "LavalandBubblegumBloodSplatter";
    [DataField] public int RisenBloodCount = 9;
    [DataField] public SoundSpecifier? RisenBloodSound = new SoundPathSpecifier("/Audio/Effects/gib1.ogg");
    [DataField] public SoundSpecifier? RisenScream = new SoundPathSpecifier("/Audio/_DeadSpace/Necromorfs/necro-scream-other-1.ogg");
    [DataField] public LocId RisenName = "lavaland-mad-miner-risen-name";
    /// <summary>The body starts twitching this long before it rises.</summary>
    [DataField] public TimeSpan RiseShakeTime = TimeSpan.FromSeconds(1.8);
    [ViewVariables] public bool RiseShaking;
    [ViewVariables] public TimeSpan? RiseAt;
    [ViewVariables] public bool Risen;
    /// <summary>The second stage's theme, started from its middle and faded in.</summary>
    [DataField] public SoundSpecifier? RisenMusic = new SoundPathSpecifier("/Audio/_DeadSpace/Necromorfs/plasma_cutters_are_your_friend.ogg");
    /// <summary>A limb torn off it: its own sprite with everything but the limb cut away.</summary>
    [DataField] public EntProtoId SeveredLimb = "LavalandMadMinerSeveredLimb";
    [DataField] public float SeveredLimbImpulse = 6f;
    /// <summary>Its speed before it lost a leg to a plasma cutter.</summary>
    [ViewVariables] public float RisenWalkSpeed;
    [ViewVariables] public float RisenSprintSpeed;

    [ViewVariables] public EntityUid? Target;
    [ViewVariables] public readonly Dictionary<EntityUid, EntityUid> HookTargets = new();
    [ViewVariables] public EntityUid? HookAttacker;
    public readonly Dictionary<EntityUid, Vector2> SplitDragOffsets = new();
    public TimeSpan SplitDragUntil;
    public TimeSpan NextSplitPursuit;
    [ViewVariables] public bool Active;
    [ViewVariables] public bool RewardsSpawned;
    [ViewVariables] public TimeSpan NextThink;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextBlink;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextFeint;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextShot;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan NextHook;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan HookEnds;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] public TimeSpan HookPullStarts;
    public TimeSpan NextSpeech;
    public TimeSpan NextHud;
    public readonly HashSet<ICommonSession> HudRecipients = new();
    public int AttackPattern;
    public int HudId;
    public SoundSpecifier? SelectedMusic;
    public MapCoordinates SpawnCoordinates;
}
