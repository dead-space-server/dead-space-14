// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// The kinetic machete's marker shot, on the right mouse button in combat mode. A tap fires an ordinary crusher mark;
/// holding the button builds the mark up - its detonation (the crit) grows with the charge. Held too long, the
/// machete blows up in the hand.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[AutoGenerateComponentPause]
public sealed partial class LavalandChargedMarkerComponent : Component
{
    /// <summary>The marker bolt fired.</summary>
    [DataField]
    public EntProtoId Projectile = "BulletCharge";

    [DataField]
    public float ProjectileSpeed = 25f;

    /// <summary>The mark's detonation damage on a tap; a charged shot multiplies it.</summary>
    [DataField]
    public DamageSpecifier MarkDamage = new() { DamageDict = new() { { "Slash", 18 } } };

    /// <summary>Held shorter than this it is a plain tap.</summary>
    [DataField]
    public TimeSpan TapTime = TimeSpan.FromSeconds(0.25);

    /// <summary>Held this long the mark is at <see cref="FullMultiplier"/>.</summary>
    [DataField]
    public TimeSpan FullChargeTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float FullMultiplier = 2f;

    /// <summary>Held past the full charge it keeps growing up to this, until <see cref="OverchargeTime"/>.</summary>
    [DataField]
    public float MaxMultiplier = 2.5f;

    /// <summary>Held this long, it blows up.</summary>
    [DataField]
    public TimeSpan OverchargeTime = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan OverchargeCooldown = TimeSpan.FromSeconds(25);

    [DataField]
    public DamageSpecifier OverchargeDamage = new() { DamageDict = new() { { "Blunt", 10 } } };

    [DataField]
    public SoundSpecifier? ShotSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/kinetic_accel.ogg");

    [DataField]
    public SoundSpecifier? OverchargeSound = new SoundPathSpecifier("/Audio/Effects/explosion_small1.ogg");

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextShot;

    [DataField, AutoNetworkedField]
    public bool Charging;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ChargeStart;

    /// <summary>The mark multiplier for a charge held this long (overcharge not counted).</summary>
    public float MultiplierFor(TimeSpan held)
    {
        if (held < TapTime)
            return 1f;

        if (held <= FullChargeTime)
            return 1f + (FullMultiplier - 1f) * (float) (held / FullChargeTime);

        var over = (float) ((held - FullChargeTime) / (OverchargeTime - FullChargeTime));
        return FullMultiplier + (MaxMultiplier - FullMultiplier) * Math.Clamp(over, 0f, 1f);
    }
}

/// <summary>The right button went down with the machete: start charging.</summary>
[Serializable, NetSerializable]
public sealed class LavalandChargedMarkerStartEvent(NetEntity weapon) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
}

/// <summary>The right button came up: fire at these coordinates.</summary>
[Serializable, NetSerializable]
public sealed class LavalandChargedMarkerReleaseEvent(NetEntity weapon, NetCoordinates target) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
    public readonly NetCoordinates Target = target;
}
