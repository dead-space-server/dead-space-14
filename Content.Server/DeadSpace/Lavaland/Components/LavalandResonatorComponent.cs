using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Lavaland.Components;

[RegisterComponent]
public sealed partial class LavalandResonatorComponent : Component
{
    [DataField]
    public string FieldPrototype = "LavalandResonanceField";

    [DataField]
    public int MaxFields = 4;

    [DataField]
    public float ShortBurstDelay = 1.8f;

    [DataField]
    public float LongBurstDelay = 3f;

    [DataField]
    public bool UseLongBurstDelay;

    [DataField]
    public float LavalandDamageMultiplier = 2f;

    [DataField]
    public float DamageRadius = 1.15f;

    [DataField]
    public bool IgnoreCreatorDamage = true;

    [DataField]
    public string? BurstProjectilePrototype;

    [DataField]
    public int BurstProjectileCount;

    [DataField]
    public float BurstProjectileSpeed = 9f;

    [DataField]
    public float BonusYieldChance;

    [DataField]
    public int BonusYieldMultiplier = 1;

    [DataField]
    public DamageSpecifier FieldDamage = new()
    {
        DamageDict =
        {
            { "Blunt", FixedPoint2.New(2.5) },
        },
    };

    /// <summary>
    /// Each resonator hit a boss took within <see cref="BossDamageWindow"/> lowers further resonator damage by this fraction.
    /// </summary>
    [DataField]
    public float BossDamageFalloff;

    [DataField]
    public float BossDamageMinMultiplier = 0.1f;

    [DataField]
    public TimeSpan BossDamageWindow = TimeSpan.FromMinutes(1);

    [DataField]
    public SoundSpecifier PlaceSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");

    [DataField]
    public SoundSpecifier BurstSound = new SoundPathSpecifier("/Audio/Weapons/plasma_cutter.ogg");

    [ViewVariables]
    public readonly HashSet<EntityUid> Fields = new();
}
