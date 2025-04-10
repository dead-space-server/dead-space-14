using Content.Shared.Actions;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.SmokingCarp;

public sealed partial class SmokingCarpActionEvent : InstantActionEvent { }
public sealed partial class ReflectCarpEvent : InstantActionEvent { }
public sealed partial class SmokingCarpTripPunchEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public sealed class SmokingCarpSaying(LocId saying) : EntityEventArgs
{
    public LocId Saying = saying;
};
