// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>Staff of lava from the ash drake: turns floor into lava after a delay, or lava back into floor.</summary>
[RegisterComponent]
public sealed partial class LavalandLavaStaffComponent : Component
{
    [DataField]
    public EntProtoId LavaPrototype = "FloorLavaEntity";

    [DataField]
    public EntProtoId WarningPrototype = "LavalandLavaStaffWarning";

    [DataField]
    public float Range = 9f;

    [DataField]
    public TimeSpan CreateDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan CreateCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan ResetCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier? Sound;

    public TimeSpan NextUse;
    public EntityUid? Warning;
}

/// <summary>Extra effects of the cleaving saw against lavaland creatures (bleed stacks, open-mode bonus).</summary>
[RegisterComponent]
public sealed partial class LavalandCleavingSawEffectsComponent : Component
{
    /// <summary>Extra damage against lavaland creatures while the saw is open.</summary>
    [DataField]
    public DamageSpecifier OpenBonusDamage = new();

    [DataField]
    public int BleedStacksPerHit = 3;

    /// <summary>Damage dealt when the bleed stacks reach their threshold.</summary>
    [DataField]
    public DamageSpecifier BleedBurstDamage = new();
}

/// <summary>Bleed stacks left by the cleaving saw. Reaching the threshold bursts for heavy damage.</summary>
[RegisterComponent]
public sealed partial class LavalandSawBleedComponent : Component
{
    [DataField]
    public int Threshold = 10;

    [DataField]
    public TimeSpan DecayDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan DecayInterval = TimeSpan.FromSeconds(0.6);

    [DataField]
    public DamageSpecifier BurstDamage = new();

    [ViewVariables]
    public int Stacks;

    public TimeSpan LastStack;
    public TimeSpan NextDecay;
}

/// <summary>
/// Cain and Abel from the colossus: consecutive hits on large creatures grow a combo (+15% damage each, up to 6),
/// each combo step adds an orbiting wisp. Getting hurt resets the combo. Wisps can be fired at a distant target.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandCainAndAbelComponent : Component
{
    [DataField]
    public int MaxCombo = 6;

    [DataField]
    public float DamageBoost = 1.15f;

    [DataField]
    public EntProtoId WispPrototype = "LavalandCainAndAbelWisp";

    [DataField]
    public EntProtoId WispProjectile = "LavalandCainAndAbelWispBolt";

    [DataField]
    public float WispOrbitRadius = 0.7f;

    [DataField]
    public float WispProjectileSpeed = 15f;

    [DataField]
    public float WispFireRange = 9f;

    [DataField]
    public SoundSpecifier? WispFireSound;

    [ViewVariables]
    public int Combo;

    [ViewVariables]
    public EntityUid? Wielder;

    public readonly List<EntityUid> Wisps = new();

    /// <summary>Thrown dagger: false - crystals around the hit spot, true - dash to the hit spot.</summary>
    [DataField]
    public bool LaunchMode;

    [DataField]
    public EntProtoId ThrowAction = "ActionLavalandCainAndAbelThrow";

    [DataField]
    public EntProtoId DaggerProjectile = "LavalandCainAndAbelDagger";

    [DataField]
    public float DaggerSpeed = 20f;

    [DataField]
    public TimeSpan CrystalsCooldown = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan LaunchCooldown = TimeSpan.FromSeconds(7);

    [DataField]
    public EntProtoId CrystalPrototype = "LavalandCainAndAbelCrystal";

    [DataField]
    public EntProtoId EngravedPrototype = "LavalandCainAndAbelEngraved";

    [DataField]
    public int CrystalRingRadius = 2;

    public EntityUid? ThrowActionEntity;
    public TimeSpan NextThrow;
}

/// <summary>Holder of Cain and Abel; taking damage breaks the combo.</summary>
[RegisterComponent]
public sealed partial class LavalandCainAndAbelWielderComponent : Component
{
    [ViewVariables]
    public EntityUid Weapon;
}

/// <summary>Thrown Cain and Abel dagger: on hit engraves the spot for crystals or a dash.</summary>
[RegisterComponent]
public sealed partial class LavalandCainAndAbelDaggerComponent : Component
{
    [ViewVariables]
    public EntityUid Weapon;

    [ViewVariables]
    public EntityUid Thrower;

    [ViewVariables]
    public bool LaunchMode;

    public bool Triggered;
}

/// <summary>Crystal left by Cain and Abel: stuns and hurts whoever touches it, much more if they are large.</summary>
[RegisterComponent]
public sealed partial class LavalandDaggerCrystalComponent : Component
{
    [DataField]
    public DamageSpecifier LargeDamage = new();

    [DataField]
    public DamageSpecifier SmallDamage = new();

    [DataField]
    public TimeSpan Stun = TimeSpan.FromSeconds(1.6);

    [DataField]
    public float Radius = 0.5f;

    [ViewVariables]
    public EntityUid? Thrower;

    public readonly HashSet<EntityUid> Hit = new();
}

/// <summary>
/// Soulscythe from bubblegum. Using it calls a ghost to inhabit it: the possessed scythe moves on its own,
/// spending blood, and gains blood from every hit. Only its master can pick it up, and it tears itself out of
/// whoever's hands it is in when it wants to. It only has its power within <see cref="ActiveRange"/> of its master;
/// killing the master puts it back to sleep.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandSoulscytheComponent : Component
{
    /// <summary>Components added when the scythe is awakened (ghost role setup).</summary>
    [DataField]
    public ComponentRegistry AwakenComponents = new();

    [DataField]
    public float MaxBlood = 100f;

    [DataField]
    public float BloodPerHit = 10f;

    [DataField]
    public float MoveCostPerTile = 1f;

    /// <summary>Farther than this from whoever awakened it, it can neither move nor strike.</summary>
    [DataField]
    public float ActiveRange = 15f;

    /// <summary>How often it can tear itself out of somebody's hands.</summary>
    [DataField]
    public TimeSpan BreakFreeCooldown = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float BreakFreeThrow = 2.5f;

    [ViewVariables]
    public bool OutOfRange;

    public TimeSpan NextBreakFree;

    [ViewVariables]
    public float Blood;

    [ViewVariables]
    public bool Awakening;

    [ViewVariables]
    public bool Possessed;

    [ViewVariables]
    public EntityUid? Master;

    public float Moved;
    public Robust.Shared.Map.MapCoordinates LastPosition;
}

/// <summary>Gloves that mine rock when their wearer walks into it.</summary>
[RegisterComponent]
public sealed partial class LavalandConcussiveGauntletsComponent : Component
{
    [DataField]
    public TimeSpan BumpCooldown = TimeSpan.FromSeconds(0.4);
}

/// <summary>Wearer of concussive gauntlets.</summary>
[RegisterComponent]
public sealed partial class LavalandBumpMinerComponent : Component
{
    [ViewVariables]
    public EntityUid Gloves;

    public TimeSpan NextMine;
}
