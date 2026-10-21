// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// Absolute rage of a lavaland boss or mini-boss, triggered by player-built structures
/// inside its arena or near it. Added automatically to every boss.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBossRageComponent : Component
{
    /// <summary>Remaining attack cooldowns are multiplied by this value while enraged.</summary>
    [DataField]
    public float CooldownMultiplier = 0.4f;

    /// <summary>Damage dealt to players near the enraged boss by non-player sources.</summary>
    [DataField]
    public float DamageMultiplier = 2f;

    [DataField]
    public float SpeedMultiplier = 1.35f;

    /// <summary>Structure detection radius for bosses without an arena.</summary>
    [DataField]
    public float StructureRadius = 12f;

    /// <summary>Radius in which players get the damage penalty for bosses without an arena.</summary>
    [DataField]
    public float VictimRadius = 30f;

    /// <summary>How long the boss stays enraged after the last forbidden structure is gone.</summary>
    [DataField]
    public TimeSpan CalmDelay = TimeSpan.FromSeconds(20);

    /// <summary>While enraged the boss rushes at the nearest player, straight through anything built.</summary>
    [DataField]
    public float ChargeSpeed = 9f;

    /// <summary>Stops this far from the player and hits.</summary>
    [DataField]
    public float ChargeReach = 1.4f;

    [DataField]
    public Content.Shared.Damage.DamageSpecifier ChargeDamage = new()
    {
        DamageDict = new() { { "Blunt", 20 } },
    };

    [DataField]
    public TimeSpan ChargeHitCooldown = TimeSpan.FromSeconds(1);

    /// <summary>How far a hit throws the player back.</summary>
    [DataField]
    public float ChargeKnockback = 2.5f;

    [ViewVariables]
    public bool Enraged;

    [ViewVariables]
    public TimeSpan NextChargeHit;

    /// <summary>The boss AI was switched off for the charge and must be switched back on.</summary>
    public bool DisabledAi;

    [ViewVariables]
    public TimeSpan LastViolation;

    public bool AddedLight;

    /// <summary>Last cooldown value written per timer, so each scheduled attack is shortened only once.</summary>
    public readonly Dictionary<string, TimeSpan> CompressedTimers = new();
}

/// <summary>
/// A player standing next to an enraged boss. Damage from non-player sources is amplified.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandBossRageVictimComponent : Component
{
    [ViewVariables]
    public EntityUid Boss;

    [ViewVariables]
    public float Multiplier = 1f;

    [ViewVariables]
    public TimeSpan ExpiresAt;
}
