using Content.Shared.Actions;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Armutant;

public sealed partial class ArmutantSwapArmEvent : InstantActionEvent { }
[Serializable, NetSerializable]
public sealed class BloodStreamRecoveryEvent(EntityUid ent) : EntityEventArgs
{
    public EntityUid Entity = ent;
}
[Serializable, NetSerializable]
public sealed class UnCuffableArmEvent(EntityUid ent) : EntityEventArgs
{
    public EntityUid Entity = ent;
}
[Serializable, NetSerializable]
public sealed class SetNewDestructibleThreshold(EntityUid ent, string damageType, int damageAmount) : EntityEventArgs
{
    public EntityUid Entity = ent;
    public string DamageType = damageType;
    public int DamageAmount = damageAmount;
}
public sealed partial class EnterArmutantStasisEvent : InstantActionEvent { }

// Abilities claws
public sealed partial class BladeDashActionEvent : WorldTargetActionEvent { }
public sealed partial class CreateTalonBladeEvent : InstantActionEvent { }

// Abilities fist
public sealed partial class FistStunTentacleToggleEvent : EntityTargetActionEvent { }
[Serializable, NetSerializable]
public sealed class BeamActiveVoidHold(string effect, EntityUid target) : EntityEventArgs
{
    public string Effect = effect;
    public EntityUid Target = target;
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

