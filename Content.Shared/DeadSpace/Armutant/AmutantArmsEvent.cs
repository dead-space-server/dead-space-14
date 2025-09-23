<<<<<<< HEAD
using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Armutant;

public sealed partial class ArmutantSwapArmEvent : InstantActionEvent
{
    [DataField]
    public ArmutantArms List;
}
=======
using Content.Shared.Actions;

namespace Content.Shared.DeadSpace.Armutant;

public sealed partial class ArmutantSwapArmEvent : InstantActionEvent { }
>>>>>>> aae74230027de3995009b60cae20059374e38691
public sealed class BloodStreamRecoveryEvent : EntityEventArgs
{
    public EntityUid Entity;
    public BloodStreamRecoveryEvent(EntityUid entity)
    {
        Entity = entity;
    }
}
public sealed class UnCuffableArmEvent : EntityEventArgs
{
    public EntityUid? Entity;
    public UnCuffableArmEvent(EntityUid entity)
    {
        Entity = entity;
    }
}
public sealed class SetNewDestructibleThreshold : EntityEventArgs
{
    public EntityUid Entity;
    public string? DamageType;
    public int DamageAmount;
    public SetNewDestructibleThreshold(EntityUid entity, string? damageType, int damageAmount)
    {
        Entity = entity;
        DamageType = damageType;
        DamageAmount = damageAmount;
    }
}
<<<<<<< HEAD
public sealed partial class EnterArmutantStasisEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId? ExitToStasisEffect;

    [DataField]
    [AutoNetworkedField]
    public float TimeInStasis = 30.0f;
}

// Abilities blade
public sealed partial class BladeDashActionEvent : WorldTargetActionEvent
{
    [DataField]
    public SoundSpecifier? SoundEffectDash;

    [DataField]
    public float MaxDashRange = 10.0f;

    [DataField]
    public float CollisionRadiusDash = 1.5f;

    [DataField]
    public string TypeDamage = "Slash";

    [DataField]
    public int AmountDamage = 25;

    [DataField]
    public float KnockbackForce = 10f;

    [DataField]
    public EntProtoId? SelfEffect;
}
public sealed partial class CreateTalonBladeEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? SoundEffectSpawn;

    [DataField]
    public EntProtoId? TalonBladePrototype;
}

// Abilities fist
public sealed partial class FistStunTentacleToggleEvent : EntityTargetActionEvent
{
    [DataField]
    public SoundSpecifier? SoundEffect;

    [DataField]
    public float StunTime = 2.0f;

    [DataField]
    public EntProtoId? TargetEffect;

    [DataField]
    public string HandEffect = "TentacleArmsHand";
}
=======
public sealed partial class EnterArmutantStasisEvent : InstantActionEvent { }

// Abilities blade
public sealed partial class BladeDashActionEvent : WorldTargetActionEvent { }
public sealed partial class CreateTalonBladeEvent : InstantActionEvent { }

// Abilities fist
public sealed partial class FistStunTentacleToggleEvent : EntityTargetActionEvent { }
>>>>>>> aae74230027de3995009b60cae20059374e38691
public sealed class BeamActiveVoidHold : EntityEventArgs
{
    public string Effect;
    public EntityUid Target;
    public BeamActiveVoidHold(string effect, EntityUid target)
    {
        Effect = effect;
        Target = target;
    }
}
<<<<<<< HEAD
public sealed partial class FistMendSelfToggleEvent : InstantActionEvent
{
    [DataField]
    public float AmountReagent = 10.0f;

    [DataField]
    public string Reagent = "Shrapnel";

    [DataField]
    public EntProtoId? SelfEffect;
}
public sealed partial class FistBuffSpeedToggleEvent : InstantActionEvent
{
    [DataField]
    public int TimeRecovery = 15;

    [DataField]
    public float BuffSpeedSprint = 5.5f;

    [DataField]
    public float BuffSpeedWalk = 3.0f;
}

// Abilities shield
public sealed partial class CreateArmorShieldToggleEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId ArmorPrototype = "ClothingOuterArmorArmutant";

    [DataField]
    public EntProtoId ArmorHelmetPrototype = "ClothingHeadHelmetArmutant";

    [DataField]
    public SoundSpecifier? SoundEffect;
}
public sealed partial class StunShieldToggleEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? SoundEffect;

    [DataField]
    public float Range = 3.0f;

    [DataField]
    public float ShortRange = 0.5f;

    [DataField]
    public EntProtoId? SelfEffectStun;

    [DataField]
    public EntProtoId? EffectTarget;

    [DataField]
    public float KnockbackForce = 15.0f;

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(1.0);
}
public sealed partial class VoidShieldToggleEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? ReflectSound;

    [DataField]
    public EntProtoId? SelfEffectShield;

    [DataField]
    public TimeSpan ActiveTime = TimeSpan.FromSeconds(15.0);
}
=======
public sealed partial class FistMendSelfToggleEvent : InstantActionEvent { }
public sealed partial class FistBuffSpeedToggleEvent : InstantActionEvent { }

// Abilities shield
public sealed partial class CreateArmorShieldToggleEvent : InstantActionEvent { }
public sealed partial class StunShieldToggleEvent : InstantActionEvent { }
public sealed partial class VoidShieldToggleEvent : InstantActionEvent { }
>>>>>>> aae74230027de3995009b60cae20059374e38691

[ByRefEvent]
public record struct StunShieldAttemptEvent(bool Cancelled);

// Abilities gun
<<<<<<< HEAD
public sealed partial class GunSmokeActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId? SpawnPrototype = "GunSmokeInstantEffect";
}
=======
public sealed partial class GunZoomActionEvent : InstantActionEvent { }
public sealed partial class GunSmokeActionEvent : InstantActionEvent { }
>>>>>>> aae74230027de3995009b60cae20059374e38691
