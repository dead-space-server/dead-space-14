using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.CorporateDjudo.Events;

public sealed class ComboAtackTypeEvent(EntityUid user, EntityUid target, ComboAttackType type) : CancellableEntityEventArgs
{
    public EntityUid Performer { get; } = user;
    public EntityUid Target { get; } = target;
    public ComboAttackType Type { get; } = type;
}

[Serializable, NetSerializable]
public enum ComboAttackType : byte
{
    Harm,
    Disarm
}
