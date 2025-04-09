using Content.Server.DeadSpace.MartialArts;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Content.Shared.Stunnable;
using System;

namespace Content.Shared.DeadSpace.MartialArts;

public abstract partial class SharedMartialArtsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!; // Для дзюдо и будущих кравмаг
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    private readonly HashSet<EntityUid> _receivers = new();
    public override void Initialize()
    {
        base.Initialize();
        InitializeSmokingCarp();
        InitializeArkalyse();

        SubscribeLocalEvent<MartialArtsComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<MartialArtsComponent, ShotAttemptedEvent>(OnShotAttempt);
    }
    private void OnShotAttempt(Entity<MartialArtsComponent> ent, ref ShotAttemptedEvent args)
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
    out DamageSpecifier damage)
    {
        damage = new DamageSpecifier();
        damage.DamageDict.Add(damageType, damageAmount);
    }
    private void AddAbility(EntityUid performer,
    string? action,
    EntityUid container)
    {
        _action.AddAction(performer, action, container);
    }
    private void TransformToItem(EntityUid item,
    string? itemAfter)
    {
        var position = _transform.GetMapCoordinates(item);
        Del(item);
        Spawn(itemAfter, position);
    }
    private void OnActionActivated(Entity<MartialArtsComponent> ent)
    {
        if (ent.Comp.IsDamageAttack)
            _popup.PopupEntity(Loc.GetString("non-active-martial-ability"), ent, ent);
        else
            _popup.PopupEntity(Loc.GetString("active-martial-ability"), ent, ent);

        ent.Comp.IsDamageAttack ^= true;
    }
    private void SelectEnterTechique(Entity<MartialArtsComponent> ent)
    {
        if (_proto.TryIndex<MartialArtPrototype>(ent.Comp.TechDataOne, out var martialArtProtoOne))
            ent.Comp.TechDataOne = martialArtProtoOne.AtackOne;

        if (_proto.TryIndex<MartialArtPrototype>(ent.Comp.TechDataTwo, out var martialArtProtoTwo))
            ent.Comp.TechDataTwo = martialArtProtoTwo.AtackTwo;

        if (_proto.TryIndex<MartialArtPrototype>(ent.Comp.TechDataThree, out var martialArtProtoThree))
            ent.Comp.TechDataThree = martialArtProtoThree.AtackThree;
    }
    private void OnMeleeHit(Entity<MartialArtsComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Handled)
            return;

        if (!_proto.TryIndex<MartialArtPrototype>(ent.Comp.MartialArtsForm.ToString(), out var martialArtsPrototype))
            return;
    }
}
