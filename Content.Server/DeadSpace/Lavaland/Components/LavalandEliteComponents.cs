// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// Pulsing tumor: touching it awakens a random lavaland elite inside a temporary arena.
/// Win and the tumor bursts into loot; flee or die and the elite goes back to sleep.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandEliteTumorComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Elites = new();

    [DataField]
    public int ArenaRadius = 10;

    [DataField]
    public EntProtoId WallPrototype = "LavalandEliteTumorWall";

    /// <summary>How long the activator may stay away from the arena before the fight resets.</summary>
    [DataField]
    public TimeSpan LeashTime = TimeSpan.FromSeconds(10);

    [DataField]
    public SoundSpecifier? ActivateSound;

    [ViewVariables]
    public EntityUid? Elite;

    [ViewVariables]
    public EntityUid? Activator;

    [ViewVariables]
    public TimeSpan? AwaySince;

    public readonly List<EntityUid> Walls = new();
}

/// <summary>Shared state of a lavaland elite: boss HUD, music, attack timer and rewards.</summary>
[RegisterComponent]
public sealed partial class LavalandEliteComponent : Component
{
    [DataField(required: true)]
    public LocId HudName;

    [DataField]
    public float MaxHealth = 1000f;

    [DataField]
    public float AggroRange = 12f;

    [DataField]
    public List<SoundSpecifier> Music = new();

    /// <summary>Spawned on death. Each entry is rolled separately.</summary>
    [DataField]
    public List<LavalandEliteReward> Rewards = new();

    [DataField]
    public TimeSpan HudInterval = TimeSpan.FromSeconds(0.5);

    [ViewVariables]
    public EntityUid? Tumor;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public bool Active;

    [ViewVariables]
    public TimeSpan NextAttack;

    public bool RewardsSpawned;
    public int HudId;
    public SoundSpecifier? SelectedMusic;
    public TimeSpan NextHud;
    public readonly HashSet<ICommonSession> HudRecipients = new();
}

[DataDefinition]
public sealed partial class LavalandEliteReward
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public float Chance = 1f;
}

/// <summary>Goliath broodmother: tentacle patches, children, rage and random tentacles when hurt.</summary>
[RegisterComponent]
public sealed partial class LavalandBroodmotherComponent : Component
{
    [DataField]
    public TimeSpan PatchCooldown = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan ChildrenCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan RageCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CallCooldown = TimeSpan.FromSeconds(6);

    [DataField]
    public EntProtoId TentacleVisual = "EffectGoliathTentacleSpawn";

    [DataField]
    public TimeSpan TentacleDelay = TimeSpan.FromSeconds(0.7);

    [DataField]
    public DamageSpecifier TentacleDamage = new();

    [DataField]
    public TimeSpan TentacleStun = TimeSpan.FromSeconds(1);

    [DataField]
    public EntProtoId ChildPrototype = "MobLavalandBroodmotherChild";

    [DataField]
    public int MaxChildren = 8;

    [DataField]
    public int ChildrenPerSpawn = 2;

    [DataField]
    public TimeSpan RageDuration = TimeSpan.FromSeconds(6.5);

    [DataField]
    public float RageSpeed = 2f;

    /// <summary>Below half health, random tentacles burst around it this often.</summary>
    [DataField]
    public TimeSpan PassiveInterval = TimeSpan.FromSeconds(3);

    [DataField]
    public int PassiveRange = 5;

    public readonly List<EntityUid> Children = new();
    public TimeSpan RageUntil;
    public TimeSpan NextPassive;
}

/// <summary>Broodmother's child: weak, puts a tentacle under its target from range.</summary>
[RegisterComponent]
public sealed partial class LavalandBroodmotherChildComponent : Component
{
    [DataField]
    public TimeSpan TentacleCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public float TentacleRange = 7f;

    [ViewVariables]
    public EntityUid? Mother;

    public TimeSpan NextTentacle;
}

/// <summary>Legionnaire: trampling charge, detachable head, bonfire teleport and smoke.</summary>
[RegisterComponent]
public sealed partial class LavalandLegionnaireComponent : Component
{
    [DataField]
    public TimeSpan ChargeCooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public int ChargeLength = 6;

    [DataField]
    public TimeSpan ChargeWindup = TimeSpan.FromSeconds(0.4);

    [DataField]
    public float ChargeSpeed = 14f;

    [DataField]
    public DamageSpecifier ChargeDamage = new();

    [DataField]
    public TimeSpan ChargeStun = TimeSpan.FromSeconds(2);

    [DataField]
    public float ChargeKnockback = 10f;

    [DataField]
    public EntProtoId ChargeTelegraph = "LavalandLegionnaireChargeTelegraph";

    [DataField]
    public EntProtoId HeadPrototype = "MobLavalandLegionnaireHead";

    [DataField]
    public TimeSpan HeadRegrowTime = TimeSpan.FromSeconds(5);

    [DataField]
    public EntProtoId BonfirePrototype = "LavalandLegionnaireBonfire";

    [DataField]
    public EntProtoId SmokePrototype = "LavalandLegionnaireSmoke";

    [DataField]
    public SoundSpecifier? ChargeSound;

    [DataField]
    public SoundSpecifier? BonfireSound;

    [ViewVariables]
    public EntityUid? Head;

    [ViewVariables]
    public EntityUid? Bonfire;

    [ViewVariables]
    public bool HasHead = true;

    public TimeSpan? RegrowAt;
    public TimeSpan? ChargeStartAt;
    public TimeSpan? ChargeEndAt;
    public Vector2 ChargeDirection;
    public readonly HashSet<EntityUid> ChargeHit = new();
}

/// <summary>The legionnaire's detached head.</summary>
[RegisterComponent]
public sealed partial class LavalandLegionnaireHeadComponent : Component
{
    [ViewVariables]
    public EntityUid? Body;
}

/// <summary>Bone pile that sets on fire whoever stands in it.</summary>
[RegisterComponent]
public sealed partial class LavalandBonfireComponent : Component
{
    [DataField]
    public float FireStacks = 5f;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public EntityUid? Legionnaire;

    public TimeSpan NextBurn;
}

/// <summary>Pandora: hierophant-like blasts that come faster as it gets hurt.</summary>
[RegisterComponent]
public sealed partial class LavalandPandoraComponent : Component
{
    [DataField]
    public EntProtoId BlastVisual = "LavalandHierophantBlast";

    [DataField]
    public EntProtoId TelegraphVisual = "LavalandHierophantTelegraph";

    [DataField]
    public DamageSpecifier BlastDamage = new();

    /// <summary>Every blast is marked on its tile this long before it goes off: time to step aside.</summary>
    [DataField]
    public TimeSpan WarningTime = TimeSpan.FromSeconds(0.8);

    [DataField]
    public int ShotLength = 8;

    [DataField]
    public float TeleportRange = 12f;

    /// <summary>Attack cooldown at full, half and quarter health.</summary>
    [DataField]
    public List<TimeSpan> Cooldowns = new() { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(1) };

    [DataField]
    public SoundSpecifier? TeleportSound;
}
