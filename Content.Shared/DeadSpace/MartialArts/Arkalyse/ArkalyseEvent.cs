using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;

namespace Content.Shared.DeadSpace.MartialArts.Arkalyse;

public sealed partial class DamageAtackArkalyseActionEvent : InstantActionEvent;
public sealed partial class MutedAtackArkalyseActionEvent : InstantActionEvent;
public sealed partial class StunedAtackArkalyseActionEvent : InstantActionEvent;
public sealed partial class MeleeHitArkalyseEvent : HandledEntityEventArgs
{
    public IReadOnlyList<EntityUid> HitEntities;

    public readonly EntityUid User;
    public MeleeHitArkalyseEvent(List<EntityUid> hitEntities, EntityUid user)
    {
        HitEntities = hitEntities;
        User = user;
    }
}
