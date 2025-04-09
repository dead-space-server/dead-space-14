using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Dataset;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.SmokingCarp;

public sealed partial class PowerPunchCarpEvent : InstantActionEvent { }
public sealed partial class SmokePunchCarpEvent : InstantActionEvent { }
public sealed partial class TripPunchCarpEvent : InstantActionEvent { }
public sealed partial class ReflectCarpEvent : InstantActionEvent { }
public sealed partial class MeleeHitSmokingCarpEvent : HandledEntityEventArgs
{
    public IReadOnlyList<EntityUid> HitEntities;

    public readonly EntityUid User;
    public MeleeHitSmokingCarpEvent(List<EntityUid> hitEntities, EntityUid user)
    {
        HitEntities = hitEntities;
        User = user;
    }
}

[Serializable, NetSerializable]
public sealed class SmokingCarpSaying(ProtoId<LocalizedDatasetPrototype> saying) : EntityEventArgs
{
    public ProtoId<LocalizedDatasetPrototype> Saying = saying;
};
