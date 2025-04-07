using System.Linq;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.CorporateDjudo.Components;
using Content.Shared.DeadSpace.CorporateDjudo.Events;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Hands;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;
using Content.Shared.StatusEffect;
using Content.Shared.Clothing;
using Content.Server.DeadSpace.Arkalyse.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.CombatMode;
using Content.Server.Hands.Systems;

namespace Content.Server.DeadSpace.CorporateDjudo.Systems;

public sealed partial class CorporateDjudoSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CorporateDjudoBeltComponent, ClothingGotEquippedEvent>(OnBeltEquip);
        SubscribeLocalEvent<CorporateDjudoBeltComponent, ClothingGotUnequippedEvent>(OnBeltUnequip);
        SubscribeLocalEvent<CorporateDjudoComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CorporateDjudoComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<CorporateDjudoComponent, DidEquipHandEvent>(YouNotCanUseBatonEquip);
        SubscribeLocalEvent<CorporateDjudoComponent, DidUnequipHandEvent>(YouNotCanUseBatonUnequip);
        SubscribeLocalEvent<CorporateDjudoComponent, BlindCorporateDjudoEvent>(ToBlindCheck);
        SubscribeLocalEvent<CorporateDjudoComponent, MeleeHitEvent>(ToBlindHit);
    }
    // При надевании пояса проверяет, есть ли у человека компонент аркалиса
    // или человек уже может пользоваться дзюдо,
    // если нет, то дается компонент
    private void OnBeltEquip(Entity<CorporateDjudoBeltComponent> ent, ref ClothingGotEquippedEvent args)
    {
        var user = args.Wearer;

        if (!HasComp<ArkalyseDamageComponent>(user) || !HasComp<CorporateDjudoComponent>(user))
            EnsureComp<CorporateDjudoComponent>(user);
        else
            return;
    }
    // При снятии пояса проверяется, есть ли у человека компонент дзюдо,
    // если да, то мы его снимаем, также есть дополнительная проверка на возможность использования батона,
    // если указано true, то мы игнорируем пояс
    private void OnBeltUnequip(Entity<CorporateDjudoBeltComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        var user = args.Wearer;

        var canUseBaton = EnsureComp<CorporateDjudoComponent>(user);

        if (canUseBaton.CanUseBaton == true)
            return;

        if (HasComp<CorporateDjudoComponent>(user))
            RemComp<CorporateDjudoComponent>(user);
        else
            return;
    }
    // При наделения игрока компонентом ему выдается action ослепления и меняются стандартные параметры
    private void OnComponentInit(Entity<CorporateDjudoComponent> user, ref ComponentInit args)
    {
        _action.AddAction(user.Owner, ref user.Comp.ActionToBlindAttackEntity, user.Comp.ActionToBlindId);
        _popup.PopupEntity(Loc.GetString("equip-djudo-belt"), user, user);

        var meleeWeaponComponent = EnsureComp<MeleeWeaponComponent>(user);

        if (meleeWeaponComponent.Damage is null)
            meleeWeaponComponent.Damage = new DamageSpecifier();

        if (!(user.Comp.AddDamage is null))
            meleeWeaponComponent.Damage += user.Comp.AddDamage;

        var combatMode = _entitySystemManager.GetEntitySystem<SharedCombatModeSystem>();
        combatMode.SetDisarmFailChance(user.Owner, user.Comp.SetChanceDisarm);
    }
    //При убирания компонента у игрока, у него забираются изменения,
    //также идет настройка стандартной скорости, это сделано для того,
    //чтобы если игрок держит батон и снимает другой рукой пояс, он не оставался обездвиженным навсегда
    private void OnComponentShutdown(Entity<CorporateDjudoComponent> user, ref ComponentShutdown args)
    {
        _action.RemoveAction(user.Owner, user.Comp.ActionToBlindAttackEntity);
        _popup.PopupEntity(Loc.GetString("unequip-djudo-belt"), user, user);

        var meleeWeaponComponent = EnsureComp<MeleeWeaponComponent>(user);
        if (!(user.Comp.AddDamage is null))
            meleeWeaponComponent.Damage -= user.Comp.AddDamage;

        var combatMode = _entitySystemManager.GetEntitySystem<SharedCombatModeSystem>();
        combatMode.SetDisarmFailChance(user.Owner, user.Comp.DefaultChanceDisarm);

        _speed.ChangeBaseSpeed(user, 2.5f, 4.5f, 20f);
    }
    // Метод который проверяет есть ли в руке прототип Stunbaton,
    // если да, то убираем возможность передвигаться и запускаем метод StartStunBatonDamageCycle
    private void YouNotCanUseBatonEquip(Entity<CorporateDjudoComponent> user, ref DidEquipHandEvent args)
    {
        if (!user.Comp.CanUseBaton)
        {
            if (!TryComp(args.Equipped, out MetaDataComponent? metaData))
                return;

            if (metaData.EntityPrototype?.ID == "Stunbaton")
            {
                _speed.ChangeBaseSpeed(user, 0f, 0f, 20f);

                StartStunBatonDamageCycle(user, args.Equipped);
            }
        }
    }
    // Асинхронный метод, который все время проверяет, держит ли игрок указанный прототип,
    // если да, то каждые 2 секунды он получает 5 урона удушьем и popup сообщения, которые видит только носитель
    private void StartStunBatonDamageCycle(Entity<CorporateDjudoComponent> user, EntityUid stunbaton)
    {
        Timer.Spawn(TimeSpan.FromSeconds(2), () =>
        {
            if (!Exists(user) || !Exists(stunbaton))
                return;

            if (_mobState.IsDead(user))
            {
                _popup.PopupEntity(Loc.GetString("you-not-can-use-it-dead"), user, user);
                return;
            }

            if (!_hands.IsHolding(user.Owner, stunbaton))
                return;

            if (!(user.Comp.StunBatonUse is null))
                _damage.TryChangeDamage(user, user.Comp.StunBatonUse, true, false);

            _popup.PopupEntity(Loc.GetString("you-not-can-use-it"), user, user);

            StartStunBatonDamageCycle(user, stunbaton);
        });
    }
    // Метод который запускается при выкидывания из руки прототипа Stunbaton, скорость возвращается к стандартной
    private void YouNotCanUseBatonUnequip(Entity<CorporateDjudoComponent> user, ref DidUnequipHandEvent args)
    {
        if (!user.Comp.CanUseBaton)
        {
            if (!TryComp(args.Unequipped, out MetaDataComponent? metaData))
                return;

            if (metaData.EntityPrototype?.ID == "Stunbaton")
            {
                _speed.ChangeBaseSpeed(user, 2.5f, 4.5f, 20f);
                _popup.PopupEntity(Loc.GetString("you-not-can-use-it-unquip"), user, user);
                return;
            }
        }
    }
    // Проверка на активность Action'а
    private void ToBlindCheck(Entity<CorporateDjudoComponent> user, ref BlindCorporateDjudoEvent args)
    {
        if (args.Handled)
            return;

        user.Comp.IsReadyAtack = !user.Comp.IsReadyAtack;

        args.Handled = true;
    }
    // Запуск события MeleeHitEvent, идет проверка пользователь ли это и имеется ли компонент MobStateComponent у цели,
    // если есть и цель жива, мы накладываем эффекты слепоты и блюра, а активность Action'a делаем false
    private void ToBlindHit(Entity<CorporateDjudoComponent> user, ref MeleeHitEvent args)
    {
        if (user.Comp.IsReadyAtack && args.HitEntities.Any())
        {
            foreach (var target in args.HitEntities)
            {
                if (args.User == target)
                    continue;

                if (!TryComp<MobStateComponent>(target, out var mobState))
                    continue;

                if (!TryComp(target, out StatusEffectsComponent? status))
                    return;

                if (mobState.CurrentState == MobState.Alive)
                {
                    _status.TryAddStatusEffect<TemporaryBlindnessComponent>(
                        target,
                        "TemporaryBlindness",
                        TimeSpan.FromSeconds(2),
                        true,
                        status);

                    _status.TryAddStatusEffect<BlurryVisionComponent>(
                        target,
                        "BlurryVision",
                        TimeSpan.FromSeconds(6),
                        false,
                        status);
                }
                user.Comp.IsReadyAtack = false;
            }
        }
    }
}
