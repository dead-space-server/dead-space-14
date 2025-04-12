using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Armutant;

public abstract partial class SharedArmutantSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    public override void Initialize()
    {
        base.Initialize();
        InitializeBlade();
        InitializeFist();

        SubscribeLocalEvent<ArmutantComponent, ArmutantSwapArmEvent>(SwapArms);
        SubscribeLocalEvent<ArmutantComponent, EnterArmutantStasisEvent>(OnEnterStasis);

        SubscribeLocalEvent<ArmutantComponent, ComponentInit>(OnComponentInit);
    }
    private void OnComponentInit(Entity<ArmutantComponent> ent, ref ComponentInit args)
    {
        var armutantEntity = EnsureComp<ArmutantComponent>(ent);

        AddAbilities(ent, armutantEntity.ArmutantAbility, armutantEntity.ArmutantActionEntities);

        var ev = new SetNewDestructibleThreshold(ent, ent.Comp.DamageTypeGib, ent.Comp.DamageAmountGib); // Делаем вызов события для смены параметров гибба
        RaiseLocalEvent(ent, ev);
    }
    private void SwapArms(Entity<ArmutantComponent> ent, ref ArmutantSwapArmEvent args) // Логика смены рук
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantActionComponent>(actionEnt, out var armutantActionComp))
            return;

        ent.Comp.SelectedArm = armutantActionComp.List; // Передаем список способностей в основной компонент
        ent.Comp.SelectedArmComp = armutantActionComp;

        DoSwap(ent);

        args.Handled = true;
    }
    private void DoSwap(Entity<ArmutantComponent> ent) // Логика выдачи способностей
    {
        if (ent.Comp.SelectedArmComp == null)
            return;

        if (_net.IsClient)
            return;

        var armutantActionComp = EnsureComp<ArmutantComponent>(ent);

        var armutantComp = ent.Comp.SelectedArmComp;

        _audio.PlayPvs(armutantComp.MeatSound, ent);

        switch (ent.Comp.SelectedArm)
        {
            case ArmutantArms.BladeArm:
                if (!TryToggleItem(ent, ent.Comp.BladeArmPrototype)) // Надеваем или снимаем руку
                    return;
                if (armutantActionComp.ArmutantActionEntitiesBlade.Count > 0) // Если список имеет данные, удаляем все способности и выходим из кейса
                {
                    ClearActiveAbilities(ent, armutantActionComp.ArmutantActionEntitiesBlade);
                    break;
                }
                else
                    AddAbilities(ent, armutantActionComp.ArmutantAbilityBlade, armutantActionComp.ArmutantActionEntitiesBlade); // Если список был пустым, то добавляем способности
                break;
            case ArmutantArms.FistArm:
                if (!TryToggleItem(ent, ent.Comp.FistArmPrototype))
                    return;
                if (armutantActionComp.ArmutantActionEntitiesFist.Count > 0)
                {
                    ClearActiveAbilities(ent, armutantActionComp.ArmutantActionEntitiesFist);
                    break;
                }
                else
                    AddAbilities(ent, armutantActionComp.ArmutantAbilityFist, armutantActionComp.ArmutantActionEntitiesFist);
                break;
            case ArmutantArms.ShieldArm:
                if (!TryToggleItem(ent, ent.Comp.ShieldArmPrototype))
                    return;
                if (armutantActionComp.ArmutantActionEntitiesShield.Count > 0)
                {
                    ClearActiveAbilities(ent, armutantActionComp.ArmutantActionEntitiesShield);
                    break;
                }
                else
                    AddAbilities(ent, armutantActionComp.ArmutantAbilityShield, armutantActionComp.ArmutantActionEntitiesShield);
                break;
            case ArmutantArms.GunArm:
                if (!TryToggleItem(ent, ent.Comp.GunArmPrototype))
                    return;
                if (armutantActionComp.ArmutantActionEntitiesGun.Count > 0)
                {
                    ClearActiveAbilities(ent, armutantActionComp.ArmutantActionEntitiesGun);
                    break;
                }
                else
                    AddAbilities(ent, armutantActionComp.ArmutantAbilityGun, armutantActionComp.ArmutantActionEntitiesGun);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        ent.Comp.SelectedArm = null;
        ent.Comp.SelectedArmComp = null;
    }
    private void OnEnterStasis(Entity<ArmutantComponent> ent, ref EnterArmutantStasisEvent args) // Логика входа в стазис
    {
        if (args.Handled)
            return;

        var actionEnt = args.Action.Owner;
        if (!TryComp<ArmutantActionComponent>(actionEnt, out var armutantActionComp))
            return;

        if (ent.Comp.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("armutant-stasis-enter-fail"), ent, ent);
            return;
        }

        if (_mobState.IsAlive(ent)) // Если человек живой, меняем его состояние на Dead и пишем от его лица что он совершил суицид
        {
            var othersMessage = Loc.GetString("suicide-command-default-text-others", ("name", ent));
            _popup.PopupEntity(othersMessage, ent, Robust.Shared.Player.Filter.PvsExcept(ent), true);

            var selfMessage = Loc.GetString("armutant-stasis-enter");
            _popup.PopupEntity(selfMessage, ent, ent);
        }

        if (!_mobState.IsDead(ent) || _mobState.IsCritical(ent))
            _mobState.ChangeMobState(ent, MobState.Dead);

        ent.Comp.IsInStasis = true; // Указываем что он в стазисе

        Timer.Spawn(TimeSpan.FromSeconds(armutantActionComp.TimeInStasis), () => // Как проходит время, вытаскиваем его из стазиса, спавним эффект и снимаем наручники, если они есть
        {
            OnExitStasis(ent);

            var ev = new UnCuffableArmEvent(ent);
            RaiseLocalEvent(ent, ev);

            var effect = Spawn(armutantActionComp.ExitToStasisEffect, Transform(ent).Coordinates);
            _transform.SetParent(effect, ent);
        });

        args.Handled = true;
    }
    private void OnExitStasis(Entity<ArmutantComponent> ent)
    {
        if (!ent.Comp.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("armutant-stasis-exit-fail"), ent, ent);
            return;
        }

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        _damage.SetAllDamage(ent, damageable, 0);
        _mobState.ChangeMobState(ent, MobState.Alive);
        var ev = new BloodStreamRecoveryEvent(ent);
        RaiseLocalEvent(ent, ev);
        _popup.PopupEntity(Loc.GetString("armutant-stasis-exit"), ent, ent);

        ent.Comp.IsInStasis = false;
    }
    private DamageSpecifier ConverDamageSpecifier(string damageType, // Сделан для удобства редактирования прототипов
    int damageAmount,
    out DamageSpecifier damage)
    {
        damage = new DamageSpecifier();
        damage.DamageDict.Add(damageType, damageAmount);

        return damage;
    }
    private void AddAbilities(EntityUid ent, // Логика добавления способностей
    List<EntProtoId> ability,
    List<EntityUid> container)
    {
        foreach (var actionId in ability)
        {
            var actions = _action.AddAction(ent, actionId);
            if (actions != null)
                container.Add(actions.Value);
        }
    }
    private void ClearActiveAbilities(EntityUid ent, // Тоже самое, но удаление
    List<EntityUid> container
    )
    {
        foreach (var abilityEntity in container)
        {
            _action.RemoveAction(ent, abilityEntity);
        }
        container.Clear();
    }
    private void HandlePullingInteractions(EntityUid ent) // Логика для рывка, убирает все взаимодействия с перетаскиванием перед рывком
    {
        if (TryComp<PullableComponent>(ent, out var pullable) && _pulling.IsPulled(ent, pullable))
            _pulling.TryStopPull(ent, pullable);

        if (TryComp<PullerComponent>(ent, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullableTarget))
            _pulling.TryStopPull(puller.Pulling.Value, pullableTarget);
    }
    public bool TryToggleItem(Entity<ArmutantComponent> ent, EntProtoId proto, string? clothingSlot = null) // Логика снятия и надевания предметов
    {
        if (!ent.Comp.Equipment.TryGetValue(proto.Id, out var item))
        {
            item = Spawn(proto, Transform(ent).Coordinates);
            if (clothingSlot != null)
            {
                if (!_inventory.TryEquip(ent, (EntityUid)item, clothingSlot, force: true))
                {
                    QueueDel(item);
                    return false;
                }
                ent.Comp.Equipment.Add(proto.Id, item);
                return true;
            }
            else if (!_hands.TryForcePickupAnyHand(ent, (EntityUid)item))
            {
                _popup.PopupEntity(Loc.GetString("armutant-fail-hands"), ent, ent);
                QueueDel(item);
                return false;
            }
            ent.Comp.Equipment.Add(proto.Id, item);
            return true;
        }

        QueueDel(item);
        ent.Comp.Equipment.Remove(proto.Id);

        return true;
    }
    public bool TryInjectReagents(EntityUid uid, List<(string, FixedPoint2)> reagents) // Вводит указанный реагент в сущность
    {
        var solution = new Shared.Chemistry.Components.Solution();
        foreach (var reagent in reagents)
        {
            solution.AddReagent(reagent.Item1, reagent.Item2);
        }

        if (!_solution.TryGetInjectableSolution(uid, out var targetSolution, out var _))
            return false;

        if (!_solution.TryAddSolution(targetSolution.Value, solution))
            return false;

        return true;
    }
}
