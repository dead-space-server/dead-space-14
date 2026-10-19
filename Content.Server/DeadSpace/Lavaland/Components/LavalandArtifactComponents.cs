// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

#region Mining equipment

/// <summary>Stack of marker beacons: use in hand to place one on your tile, alt-click to change colour.</summary>
[RegisterComponent]
public sealed partial class LavalandMarkerBeaconStackComponent : Component
{
    [DataField]
    public EntProtoId BeaconPrototype = "LavalandMarkerBeacon";

    /// <summary>Colour ids in cycling order; each must have a light colour in <see cref="LightColors"/>.</summary>
    [DataField]
    public List<string> Colors = new();

    [DataField]
    public Dictionary<string, Color> LightColors = new();

    [DataField]
    public string Color = "burgundy";
}

/// <summary>A placed marker beacon. Picking it up returns it to a stack.</summary>
[RegisterComponent]
public sealed partial class LavalandMarkerBeaconComponent : Component
{
    [DataField]
    public EntProtoId StackPrototype = "LavalandMarkerBeaconStack1";

    [DataField]
    public string Color = "burgundy";

    /// <summary>Message left with a screwdriver; shown on examine and on the lavaland sensor tablet.</summary>
    [DataField]
    public string? Message;

    [DataField]
    public int MaxMessageLength = 150;
}

/// <summary>Revives a dead creature once and makes it friendly.</summary>
[RegisterComponent]
public sealed partial class LavalandLazarusInjectorComponent : Component
{
    [DataField]
    public bool Loaded = true;

    /// <summary>Factions the revived creature joins.</summary>
    [DataField]
    public List<ProtoId<NpcFactionPrototype>> Factions = new() { "PetsNT" };

    /// <summary>After an EMP the revived creature keeps its old factions.</summary>
    [DataField]
    public bool Malfunctioning;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>Mining drone: follows its owner, shoots fauna and collects nearby ore while in collect mode.</summary>
[RegisterComponent]
public sealed partial class LavalandMinebotComponent : Component
{
    [DataField]
    public float CollectRange = 1.5f;

    [DataField]
    public TimeSpan CollectInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public bool CollectOre = true;

    /// <summary>The slot of its built-in borg ore bag: collected ore goes into the bag.</summary>
    [DataField]
    public string BagSlot = "minebot-ore-bag";

    [DataField]
    public EntProtoId BagPrototype = "BorgOreBag";

    [DataField]
    public ProtoId<TagPrototype> OreTag = "Ore";

    /// <summary>With no fauna around it digs out ore veins near it with its kinetic accelerator.</summary>
    [DataField]
    public bool MineOre = true;

    [DataField]
    public float MineRange = 6f;

    [DataField]
    public TimeSpan MineInterval = TimeSpan.FromSeconds(1.5);

    /// <summary>Fauna this close keeps it fighting instead of digging.</summary>
    [DataField]
    public float MineHostileRange = 9f;

    [ViewVariables]
    public EntityUid? BoundUser;

    public TimeSpan NextCollect;

    public TimeSpan NextMine;
}

#endregion

#region Necropolis artifacts

/// <summary>Spooky lantern: releases a wisp that lights the way and grants thermal vision.</summary>
[RegisterComponent]
public sealed partial class LavalandWispLanternComponent : Component
{
    [DataField]
    public EntProtoId WispPrototype = "LavalandLanternWisp";

    [DataField]
    public bool GrantThermalVision = true;

    [ViewVariables]
    public EntityUid? Wisp;

    [ViewVariables]
    public EntityUid? Holder;

    public bool AddedThermalVision;

    /// <summary>Speed of the wisp while it leads its owner to the outpost in a guiding day phase.</summary>
    [DataField]
    public float GuideSpeed = 3.5f;

    /// <summary>The wisp stops and waits once its owner falls this far behind...</summary>
    [DataField]
    public float GuideWaitDistance = 5f;

    /// <summary>...and moves on again when the owner comes this close.</summary>
    [DataField]
    public float GuideResumeDistance = 2.5f;

    /// <summary>The wisp hovers once it is this close to the outpost center.</summary>
    [DataField]
    public float GuideArriveDistance = 3f;

    /// <summary>If the owner ends up this far away (teleport, jaunt), the wisp jumps back to them.</summary>
    [DataField]
    public float GuideLostDistance = 25f;

    [ViewVariables]
    public bool Guiding;

    [ViewVariables]
    public bool GuideWaiting;

    /// <summary>
    /// Off guiding duty the wisp is not stuck to its owner: it drifts around them at this distance,
    /// lagging behind when they run, like a free floating hand.
    /// </summary>
    [DataField]
    public float FollowRadius = 1.1f;

    /// <summary>How quickly it catches up with its spot near the owner (higher - tighter).</summary>
    [DataField]
    public float FollowSharpness = 2.5f;

    /// <summary>How fast it circles its owner, radians per second.</summary>
    [DataField]
    public float FollowOrbitSpeed = 0.7f;

    /// <summary>How far it bobs up and down while hovering.</summary>
    [DataField]
    public float FollowBob = 0.2f;

    [ViewVariables]
    public float FollowPhase;
}

/// <summary>One of two linked cubes: using it teleports you to the other one.</summary>
[RegisterComponent]
public sealed partial class LavalandWarpCubeComponent : Component
{
    /// <summary>If set, the paired cube is spawned on map init.</summary>
    [DataField]
    public EntProtoId? SpawnPair;

    [DataField]
    public EntProtoId Effect = "LavalandWarpCubeEffect";

    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables]
    public EntityUid? Linked;
}

/// <summary>
/// Paradox bag: two linked bags share their contents. Opening one pulls everything from its twin into it.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandParadoxBagComponent : Component
{
    /// <summary>If set, the twin bag is spawned on map init.</summary>
    [DataField]
    public EntProtoId? SpawnPair;

    [ViewVariables]
    public EntityUid? Linked;
}

/// <summary>Takes its user out of reality for a while: invulnerable, untargetable and unable to act.</summary>
[RegisterComponent]
public sealed partial class LavalandImmortalityTalismanComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public EntProtoId HolePrototype = "LavalandRealityHole";

    public TimeSpan NextUse;
}

/// <summary>The hole a talisman user hides in.</summary>
[RegisterComponent]
public sealed partial class LavalandRealityHoleComponent : Component
{
    [ViewVariables]
    public EntityUid? User;

    public TimeSpan ReleaseAt;
}

/// <summary>Ship in a bottle: releases a lava boat and can take it back.</summary>
[RegisterComponent]
public sealed partial class LavalandShipInBottleComponent : Component
{
    [DataField]
    public EntProtoId BoatPrototype = "LavaBoat";

    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables]
    public EntityUid? Boat;
}

/// <summary>Single-use potion that adds components to its drinker (flight potion grants wings).</summary>
[RegisterComponent]
public sealed partial class LavalandPotionComponent : Component
{
    [DataField]
    public ComponentRegistry AddedComponents = new();

    /// <summary>Only humanoids can drink it.</summary>
    [DataField]
    public bool HumanoidOnly = true;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public LocId Message = "lavaland-potion-drink";

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>Rod of Asclepius: swear the oath and become a pacifist healer bound to the rod.</summary>
[RegisterComponent]
public sealed partial class LavalandRodOfAsclepiusComponent : Component
{
    [DataField]
    public List<LocId> OathLines = new();

    [DataField]
    public TimeSpan LineDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public float AuraRange = 7f;

    [DataField]
    public TimeSpan AuraInterval = TimeSpan.FromSeconds(2.6);

    /// <summary>Healing applied to everyone alive in range every aura tick.</summary>
    [DataField]
    public DamageSpecifier AuraHeal = new();

    /// <summary>Extra healing per second for the bearer.</summary>
    [DataField]
    public DamageSpecifier SelfHeal = new();

    [ViewVariables]
    public bool Activated;

    [ViewVariables]
    public int OathProgress;

    [ViewVariables]
    public EntityUid? Bearer;

    public TimeSpan NextAura;
    public TimeSpan NextSelfHeal;
}

/// <summary>Memento mori: once linked, its wearer cannot die, but taking it off turns them to ash.</summary>
[RegisterComponent]
public sealed partial class LavalandMementoMoriComponent : Component
{
    [DataField]
    public TimeSpan LinkTime = TimeSpan.FromSeconds(4);

    [DataField]
    public EntProtoId AshPrototype = "Ash";

    [ViewVariables]
    public EntityUid? LinkedUser;
}

/// <summary>Wearer linked to a memento mori; keeps the thresholds it had before.</summary>
[RegisterComponent]
public sealed partial class LavalandMementoBoundComponent : Component
{
    [ViewVariables]
    public EntityUid Pendant;

    public FixedPoint2? OldCritThreshold;
    public FixedPoint2? OldDeadThreshold;
}

/// <summary>Berserker armour: getting hurt charges berserk; when full, berserk reduces damage taken
/// and speeds up its wearer, but they cannot use guns.</summary>
[RegisterComponent]
public sealed partial class LavalandBerserkerSuitComponent : Component
{
    [DataField]
    public float MaxCharge = 100f;

    [DataField]
    public float DamageToCharge = 0.75f;

    [DataField]
    public float DrainPerSecond = 5f;

    /// <summary>Damage modifiers applied to the wearer while berserk.</summary>
    [DataField]
    public DamageModifierSet ActiveModifiers = new();

    [DataField]
    public float SpeedMultiplier = 1.25f;

    [DataField]
    public EntProtoId Action = "ActionLavalandBerserk";

    [DataField]
    public SoundSpecifier? ActivateSound;

    [ViewVariables]
    public float Charge;

    [ViewVariables]
    public bool Active;

    [ViewVariables]
    public EntityUid? Wearer;

    public EntityUid? ActionEntity;
}

/// <summary>Wearer of berserker armour.</summary>
[RegisterComponent]
public sealed partial class LavalandBerserkerWearerComponent : Component
{
    [ViewVariables]
    public EntityUid Suit;
}

#endregion

/// <summary>A boss on its way to tear apart a player kept alive by a memento mori.</summary>
[RegisterComponent]
public sealed partial class LavalandBossExecutingComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    /// <summary>It strikes by then, there or not.</summary>
    [ViewVariables]
    public TimeSpan Until;
}
