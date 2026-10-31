// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.MartialArts.CQC;

public sealed partial class CQCPowerPunchEvent : InstantActionEvent { }
public sealed partial class CQCEyeAttackEvent : InstantActionEvent { }
public sealed partial class CQCRelaxEvent : InstantActionEvent { }
public sealed partial class CQCStepPunchEvent : InstantActionEvent { }
public sealed partial class CQCConcentrationEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed class CQCSaying(LocId saying) : EntityEventArgs
{
    public LocId Saying = saying;
};