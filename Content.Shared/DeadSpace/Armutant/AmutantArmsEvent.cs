using Content.Shared.Actions;
using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.Armutant;

public sealed partial class BladeArmutantToggleEvent : InstantActionEvent { }
public sealed partial class FistArmutantToggleEvent : InstantActionEvent { }
public sealed partial class ShieldArmutantToggleEvent : InstantActionEvent { }
public sealed partial class GunArmutantToggleEvent : InstantActionEvent { }
public sealed partial class EnterArmutantStasisEvent : InstantActionEvent { }

// Abilities claws

public sealed partial class CreateTalonBladeEvent : InstantActionEvent { }
public sealed partial class BladeDashActionEvent : WorldTargetActionEvent
{
    public EntityCoordinates? Coords { get; set; }
}

// Abilities shield
public sealed partial class CreateArmorShieldToggleEvent : InstantActionEvent { }
public sealed partial class StunShieldToggleEvent : InstantActionEvent { }
public sealed partial class VoidShieldToggleEvent : InstantActionEvent { }

[ByRefEvent]
public record struct StunShieldAttemptEvent(bool Cancelled);

// Abilities fist
public sealed partial class FistStunAroundToggleEvent : EntityTargetActionEvent { }
public sealed partial class FistMendSelfToggleEvent : InstantActionEvent { }
public sealed partial class FistBuffSpeedToggleEvent : InstantActionEvent { }

// Abilities gun
public sealed partial class GunZoomActionEvent : InstantActionEvent { }
public sealed partial class GunSmokeActionEvent : InstantActionEvent { }

