using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Content.Shared.Stunnable;
using Robust.Shared.Random;
using Content.Shared.DeadSpace.MartialArts.SmokingCarp;

namespace Content.Shared.DeadSpace.MartialArts;

public abstract partial class SharedMartialArtsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    private readonly HashSet<EntityUid> _receivers = new();
    public override void Initialize()
    {
        base.Initialize();
        InitializeSmokingCarp();
        InitializeArkalyse();

        SubscribeLocalEvent<SmokingCarpComponent, ShotAttemptedEvent>(OnShotAttempt);
    }
    private void OnShotAttempt(Entity<SmokingCarpComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.MartialArtsForm != MartialArtsForms.SmokingCarp)
            return;
        _popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }
    private void DamageHit(EntityUid ent,
    EntityUid target,
    string damageType,
    int damageAmount,
    bool ignoreResist,
    out DamageSpecifier damage)
    {
        damage = new DamageSpecifier();
        damage.DamageDict.Add(damageType, damageAmount);

        _damageable.TryChangeDamage(target, damage, ignoreResist);
    }
    private void TransformToItem(EntityUid item,
    string? itemAfter)
    {
        var position = _transform.GetMapCoordinates(item);
        Del(item);
        Spawn(itemAfter, position);
    }
}
