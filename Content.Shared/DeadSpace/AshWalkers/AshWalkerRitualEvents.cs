using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.AshWalkers;

public sealed partial class AshWalkerDrawRuneActionEvent : WorldTargetActionEvent
{
    [DataField]
    public AshWalkerRite Rite;
}

[Serializable, NetSerializable]
public sealed partial class AshWalkerDrawRuneDoAfterEvent : DoAfterEvent
{
    public NetCoordinates Coordinates;
    public AshWalkerRite Rite;

    public override DoAfterEvent Clone() => (AshWalkerDrawRuneDoAfterEvent) MemberwiseClone();
}

[Serializable, NetSerializable]
public sealed partial class AshWalkerRitualDoAfterEvent : DoAfterEvent
{
    public List<NetEntity> Sacrifices = new();
    public NetEntity? Subject;
    public NetEntity? Mind;

    public override DoAfterEvent Clone()
    {
        var clone = (AshWalkerRitualDoAfterEvent) MemberwiseClone();
        clone.Sacrifices = new List<NetEntity>(Sacrifices);
        return clone;
    }
}
