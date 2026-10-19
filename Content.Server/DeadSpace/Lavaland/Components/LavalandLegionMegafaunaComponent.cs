// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// One body of the Legion megafauna. When a body runs out of health it splits into smaller bodies;
/// the fight ends only when the last one dies. Only one body at a time carries <see cref="LavalandBossComponent"/>
/// and acts as the arena boss.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandLegionMegafaunaComponent : Component
{
    /// <summary>3 - large, 2 - medium, 1 - small. Larger bodies hit harder and summon more sentinels.</summary>
    [DataField]
    public int Size = 3;

    /// <summary>Health of this body before the arena participant scaling.</summary>
    [DataField]
    public float BodyHealth = 700f;

    /// <summary>Combined health of all bodies this one will still split into.</summary>
    [DataField]
    public float SplitHealthPool;

    [DataField]
    public List<EntProtoId> SplitInto = new();

    /// <summary>Large body that replaces a smaller arena boss when an empty arena resets.</summary>
    [DataField]
    public EntProtoId? ReassemblePrototype;

    [DataField]
    public EntProtoId SkullPrototype = "MobLavalandLegionHead";

    [DataField]
    public EntProtoId SentinelPrototype = "LavalandLegionSentinel";

    [DataField]
    public TimeSpan AttackCooldown = TimeSpan.FromSeconds(2);

    [DataField]
    public float AggroRange = 13f;

    [DataField]
    public TimeSpan ChargeWindup = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ChargeDuration = TimeSpan.FromSeconds(0.6);

    [DataField]
    public float ChargeSpeed = 12f;

    [DataField]
    public float ChargeHitRange = 1.2f;

    [DataField]
    public float ChargeKnockback = 8f;

    /// <summary>Charge damage before size scaling (multiplied by Size / 2).</summary>
    [DataField]
    public DamageSpecifier ChargeDamage = new();

    [DataField]
    public SoundSpecifier? ChargeWindupSound;

    [DataField]
    public SoundSpecifier? ChargeImpactSound;

    [DataField]
    public SoundSpecifier? SentinelSound;

    [DataField]
    public float SentinelSpawnRadius = 4f;

    /// <summary>Loot dropped by a body that dies while other bodies are still alive.</summary>
    [DataField]
    public EntProtoId? PieceReward = "LavalandNecropolisChest";

    [DataField]
    public float PieceRewardChance = 0.2f;

    /// <summary>The first body of this Legion, shared by every body it splits into.</summary>
    [ViewVariables]
    public EntityUid Root;

    [ViewVariables]
    public EntityUid? Arena;

    /// <summary>Fights on its own, without a boss arena (spawned by a mapping marker).</summary>
    [ViewVariables]
    public bool Free;

    /// <summary>Stands still and does not attack until woken: a Legion gate nearby opens or it gets hit.</summary>
    [DataField]
    public bool Dormant;

    /// <summary>Longest lunge at whoever woke the Legion up.</summary>
    [DataField]
    public TimeSpan WakeLungeMax = TimeSpan.FromSeconds(1.5);

    /// <summary>The lunge on waking up tears through gates and walls in its way.</summary>
    [DataField]
    public bool LungeBreaksWalls = true;

    /// <summary>How far ahead of the body the waking lunge breaks things.</summary>
    [DataField]
    public float LungeBreakRange = 1.4f;

    [DataField]
    public SoundSpecifier? LungeBreakSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");

    /// <summary>The waking lunge is under way.</summary>
    [ViewVariables]
    public bool Lunging;

    /// <summary>Time between maneuvers: a roll behind the target's back or a leap to close the distance.</summary>
    [DataField]
    public TimeSpan ManeuverCooldownMin = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan ManeuverCooldownMax = TimeSpan.FromSeconds(4.5);

    /// <summary>Within this distance the body rolls around to the target's back.</summary>
    [DataField]
    public float FlankRange = 8f;

    /// <summary>How far behind the target's back the roll lands.</summary>
    [DataField]
    public float FlankDistance = 1.8f;

    /// <summary>Farther than this the body leaps at the target to close the distance.</summary>
    [DataField]
    public float GapCloseDistance = 9f;

    /// <summary>How far in front of the target the closing leap lands.</summary>
    [DataField]
    public float GapCloseLanding = 2.5f;

    /// <summary>The roll: it spins towards its spot at this speed (tiles per second), no longer than the limit.</summary>
    [DataField]
    public float ManeuverSpeed = 9f;

    [DataField]
    public TimeSpan ManeuverMaxDuration = TimeSpan.FromSeconds(1.2);

    /// <summary>
    /// Chasing a target that runs off (farther than <see cref="GapCloseDistance"/>): rolls come more often,
    /// faster and farther, so a quick player is not lost.
    /// </summary>
    [DataField]
    public TimeSpan ChaseCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public float ChaseSpeed = 13f;

    [DataField]
    public TimeSpan ChaseMaxDuration = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan? ManeuverEndAt;

    public Vector2 ManeuverVelocity;

    [DataField]
    public SoundSpecifier? ManeuverSound = new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg");

    [ViewVariables]
    public TimeSpan NextManeuver;

    /// <summary>
    /// How often an awake body checks whether it is stuck: if it barely moved while its target is away,
    /// it smashes through whatever stands between them.
    /// </summary>
    [DataField]
    public TimeSpan StuckCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>Moving less than this between two checks counts as stuck.</summary>
    [DataField]
    public float StuckDistance = 0.4f;

    /// <summary>A target closer than this is being fought, not chased: standing still is fine.</summary>
    [DataField]
    public float StuckIgnoreRange = 2.5f;

    [ViewVariables]
    public TimeSpan NextStuckCheck;

    [ViewVariables]
    public Vector2 LastStuckPosition;

    /// <summary>The grid of the Legion's own lair: its indestructible walls give way to the Legion itself.</summary>
    [ViewVariables]
    public EntityUid? Lair;

    /// <summary>Stuck checks in a row it has not moved: from the second one on it breaks all around its body.</summary>
    [ViewVariables]
    public int StuckCount;

    /// <summary>
    /// Outside an arena: whoever woke the Legion. Every body hunts them across the map until one of them dies.
    /// </summary>
    [ViewVariables]
    public EntityUid? Pursued;

    /// <summary>Outside an arena: where it came into the world. A raid on the outpost sends it back there.</summary>
    [ViewVariables]
    public Robust.Shared.Map.MapCoordinates? Home;

    /// <summary>Tiles per second it flies home when a raid starts.</summary>
    [DataField]
    public float HomeFlightSpeed = 9f;

    /// <summary>Flying home from a raid.</summary>
    [ViewVariables]
    public bool ReturningHome;

    /// <summary>
    /// Before it flies off from a raid it freezes in fright for this long: shaking, its hands (if any) up over its
    /// head.
    /// </summary>
    [DataField]
    public TimeSpan RaidPanicTime = TimeSpan.FromSeconds(2.5);

    [ViewVariables]
    public TimeSpan? RaidPanicUntil;

    /// <summary>Has fled this raid already: woken at home meanwhile, it fights there instead of fleeing again.</summary>
    [ViewVariables]
    public bool FledRaid;

    /// <summary>Back home from a raid, asleep: the first player within <see cref="AggroRange"/> wakes it.</summary>
    [ViewVariables]
    public bool WaitingAtHome;

    /// <summary>Outside an arena, once the pursued one is gone: other players this close are attacked.</summary>
    [DataField]
    public float PursuitRange = 80f;

    /// <summary>Outside an arena: necropolis gates this close in front of a body are burst open.</summary>
    [DataField]
    public float GateSmashRange = 1.6f;

    [ViewVariables]
    public bool Dying;

    [ViewVariables]
    public TimeSpan NextAttack;

    [ViewVariables]
    public TimeSpan? ChargeStartAt;

    [ViewVariables]
    public TimeSpan? ChargeEndAt;

    public Vector2 ChargeDirection;

    public readonly HashSet<EntityUid> ChargeHit = new();
}

/// <summary>
/// Legion sentinel: aims at the nearest enemy, shows a tracer and then fires a piercing blood pulse.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandLegionSentinelComponent : Component
{
    [DataField]
    public TimeSpan AimDelay = TimeSpan.FromSeconds(1.8);

    [DataField]
    public TimeSpan ShotDelay = TimeSpan.FromSeconds(0.8);

    [DataField]
    public TimeSpan LingerTime = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float VisionRange = 9f;

    [DataField]
    public float BeamRange = 6f;

    [DataField]
    public float BeamWidth = 0.5f;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public EntProtoId TracerPrototype = "LavalandLegionSentinelTracer";

    [DataField]
    public EntProtoId BeamPrototype = "LavalandLegionSentinelBeam";

    [DataField]
    public SoundSpecifier? AimSound;

    [DataField]
    public SoundSpecifier? FireSound;

    public TimeSpan NextStage;
    public int Stage;
    public Vector2 Direction;
}

/// <summary>
/// Staff of storms: calls thunderbolts on a target tile and can dispel ash storms.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandStormStaffComponent : Component
{
    [DataField]
    public int MaxCharges = 3;

    [DataField]
    public int Charges = 3;

    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan BoltDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan DispelTime = TimeSpan.FromSeconds(3);

    /// <summary>After a storm is dispelled the staff cannot dispel another for this long.</summary>
    [DataField]
    public TimeSpan DispelCooldown = TimeSpan.FromMinutes(16);

    [ViewVariables]
    public TimeSpan NextDispel;

    [DataField]
    public float Range = 12f;

    /// <summary>Shock damage at the impact tile; halved on neighbours, doubled during an ash storm.</summary>
    [DataField]
    public DamageSpecifier BoltDamage = new();

    /// <summary>Multiplier against non-humanoid creatures (fauna, bosses).</summary>
    [DataField]
    public float CreatureMultiplier = 3f;

    [DataField]
    public EntProtoId TelegraphPrototype = "LavalandStormStaffTelegraph";

    [DataField]
    public EntProtoId BoltPrototype = "LavalandStormStaffThunderbolt";

    /// <summary>Effect on the extra tiles hit during an ash storm.</summary>
    [DataField]
    public EntProtoId SparkPrototype = "LavalandStormStaffSpark";

    [DataField]
    public SoundSpecifier? AimSound;

    [DataField]
    public SoundSpecifier? BoltSound;

    [DataField]
    public SoundSpecifier? RechargeSound;

    [DataField]
    public SoundSpecifier? DispelSound;

    public readonly List<TimeSpan> RechargeAt = new();
    public readonly List<(TimeSpan At, EntityUid Grid, Vector2i Tile, bool Boosted, EntityUid User)> PendingBolts = new();
}

/// <summary>Serum that stops a legion soul from decaying.</summary>
[RegisterComponent]
public sealed partial class LavalandSoulStabilizerComponent : Component;

/// <summary>A decaying monster organ (legion soul) that a stabilizer can preserve.</summary>
[RegisterComponent]
public sealed partial class LavalandStabilizableComponent : Component
{
    [ViewVariables]
    public bool Stabilized;
}
