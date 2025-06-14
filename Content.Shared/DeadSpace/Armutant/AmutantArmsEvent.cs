using Content.Shared.Actions;

namespace Content.Shared.DeadSpace.Armutant;

public sealed partial class ArmutantSwapArmEvent : InstantActionEvent { }
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
public sealed partial class EnterArmutantStasisEvent : InstantActionEvent { }

// Abilities blade
public sealed partial class BladeDashActionEvent : WorldTargetActionEvent { }
public sealed partial class CreateTalonBladeEvent : InstantActionEvent { }

// Abilities fist
public sealed partial class FistStunTentacleToggleEvent : EntityTargetActionEvent { }
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
public sealed partial class FistMendSelfToggleEvent : InstantActionEvent { }
public sealed partial class FistBuffSpeedToggleEvent : InstantActionEvent { }

// Abilities shield
public sealed partial class CreateArmorShieldToggleEvent : InstantActionEvent { }
public sealed partial class StunShieldToggleEvent : InstantActionEvent { }
public sealed partial class VoidShieldToggleEvent : InstantActionEvent { }

[ByRefEvent]
public record struct StunShieldAttemptEvent(bool Cancelled);

// Abilities gun
public sealed partial class GunZoomActionEvent : InstantActionEvent { }
public sealed partial class GunSmokeActionEvent : InstantActionEvent { }
