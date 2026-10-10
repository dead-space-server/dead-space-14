using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Sectants;

public sealed partial class SectantSleepActionEvent : InstantActionEvent { }

public sealed partial class SectantMaleExecuteActionEvent : EntityTargetActionEvent { }

public sealed partial class SectantMaleRandomBlinkActionEvent : InstantActionEvent { }

public sealed partial class SectantFemaleInvisibilityToggleActionEvent : InstantActionEvent { }

public sealed partial class SectantFemaleHackActionEvent : InstantActionEvent { }

public sealed partial class SectantFemaleHealActionEvent : InstantActionEvent { }

public sealed partial class SectantFemaleBlinkActionEvent : WorldTargetActionEvent { }

public sealed partial class SectantFemaleHallucinateActionEvent : InstantActionEvent { }

/// <summary>Raised when a sectant entity disappears (before deletion).</summary>
public readonly record struct SectantDisappearedEvent(EntityUid Uid, LocId Message, float Radius);
