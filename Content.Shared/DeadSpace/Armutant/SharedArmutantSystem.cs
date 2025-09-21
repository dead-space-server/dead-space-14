using System.Numerics;
using Content.Shared.Actions;
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
using Content.Shared.Standing;
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
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly HashSet<EntityUid> _receivers = new();

    public override void Initialize()
    {
        base.Initialize();
        InitializeBlade();
        InitializeFist();
        InitializeShield();
        InitializeGun();
        SubscribeLocalEvent<ArmutantComponent, ArmutantSwapArmEvent>(SwapArms);
        SubscribeLocalEvent<ArmutantComponent, EnterArmutantStasisEvent>(OnEnterStasis);
        SubscribeLocalEvent<ArmutantComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(Entity<ArmutantComponent> ent, ref ComponentInit args)
    {
        var armutantEntity = EnsureComp<ArmutantComponent>(ent);

        AddAbilities(ent, armutantEntity.ArmutantAbility, armutantEntity.ArmutantActionEntities);

        var ev = new SetNewDestructibleThreshold(ent, ent.Comp.DamageTypeGib, ent.Comp.DamageAmountGib);
        RaiseLocalEvent(ent, ev);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ArmutantComponent>();
        while (query.MoveNext(out var uid, out var armutant))
        {
            if (armutant.TimeToExitStasis == null || curTime < armutant.TimeToExitStasis)
                continue;

            var ent = (uid, armutant);
            if (TryExitStasis(ent))
            {
                var ev = new UnCuffableArmEvent(uid);
                RaiseLocalEvent(uid, ev);
                if (armutant.ExitToStasisEffect != null)
                {
                    var effect = Spawn(armutant.ExitToStasisEffect, Transform(uid).Coordinates);
                    _transform.SetParent(effect, uid);
                }
            }

            armutant.TimeToExitStasis = null;
            armutant.ExitToStasisEffect = null;
        }
    }

    private void SwapArms(Entity<ArmutantComponent> ent, ref ArmutantSwapArmEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Handled)
            return;

        ent.Comp.SelectedArm = args.List;

        DoSwap(ent);

        args.Handled = true;
    }

    private void DoSwap(Entity<ArmutantComponent> ent)
    {
        if (ent.Comp.SelectedArm == null)
            return;

        if (_net.IsClient)
            return;

        var armutantActionComp = EnsureComp<ArmutantComponent>(ent);

        var armutantComp = ent.Comp.SelectedArm;

        _audio.PlayPvs(ent.Comp.MeatSound, ent);

        switch (ent.Comp.SelectedArm)
        {
            case ArmutantArms.BladeArm:
                if (!TryToggleItem(ent, ent.Comp.BladeArmPrototype))
                    return;
                if (armutantActionComp.ArmutantActionEntitiesBlade.Count > 0)
                {
                    ClearActiveAbilities(ent, armutantActionComp.ArmutantActionEntitiesBlade);
                    break;
                }
                else
                    AddAbilities(ent, armutantActionComp.ArmutantAbilityBlade, armutantActionComp.ArmutantActionEntitiesBlade);
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
                    RemoveAllArmor(ent, armutantActionComp.EquipmentArmor);
                }
                else
                    AddAbilities(ent, armutantActionComp.ArmutantAbilityShield, armutantActionComp.ArmutantActionEntitiesShield);
                break;
            case ArmutantArms.GunArm:
                if (!TryToggleItem(ent, ent.Comp.GunArmPrototype))
                    return;

                if (armutantActionComp.ArmutantActionEntitiesGun.Count > 0)
                    ClearActiveAbilities(ent, armutantActionComp.ArmutantActionEntitiesGun);
                else
                    AddAbilities(ent, armutantActionComp.ArmutantAbilityGun, armutantActionComp.ArmutantActionEntitiesGun);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        ent.Comp.SelectedArm = null;
    }

    private void OnEnterStasis(Entity<ArmutantComponent> ent, ref EnterArmutantStasisEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("armutant-stasis-enter-fail"), ent, ent);
            return;
        }

        if (_mobState.IsAlive(ent))
        {
            var othersMessage = Loc.GetString("suicide-command-default-text-others", ("name", ent));
            _popup.PopupEntity(othersMessage, ent, Robust.Shared.Player.Filter.PvsExcept(ent), true);

            var selfMessage = Loc.GetString("armutant-stasis-enter");
            _popup.PopupEntity(selfMessage, ent, ent);
        }

        if (!_mobState.IsDead(ent))
            _mobState.ChangeMobState(ent, MobState.Dead);

        ent.Comp.IsInStasis = true;

        ent.Comp.TimeToExitStasis = _timing.CurTime + TimeSpan.FromSeconds(args.TimeInStasis);
        ent.Comp.ExitToStasisEffect = args.ExitToStasisEffect;

        args.Handled = true;
    }

    private bool TryExitStasis(Entity<ArmutantComponent> ent)
    {
        if (!ent.Comp.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("armutant-stasis-exit-fail"), ent, ent);
            return false;
        }

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return false;

        _damage.SetAllDamage(ent, damageable, 0);
        _mobState.ChangeMobState(ent, MobState.Alive);
        var ev = new BloodStreamRecoveryEvent(ent);
        RaiseLocalEvent(ent, ev);
        _popup.PopupEntity(Loc.GetString("armutant-stasis-exit"), ent, ent);

        ent.Comp.IsInStasis = false;

        return true;
    }

    private DamageSpecifier ConverDamageSpecifier(string damageType,
    int damageAmount,
    out DamageSpecifier damage)
    {
        damage = new DamageSpecifier();
        damage.DamageDict.Add(damageType, damageAmount);

        return damage;
    }

    private void AddAbilities(EntityUid ent,
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

    private void ClearActiveAbilities(EntityUid ent,
    List<EntityUid> container
    )
    {
        foreach (var abilityEntity in container)
        {
            _action.RemoveAction(ent, abilityEntity);
        }
        container.Clear();
    }

    private void HandlePullingInteractions(EntityUid ent)
    {
        if (TryComp<PullableComponent>(ent, out var pullable) && _pulling.IsPulled(ent, pullable))
            _pulling.TryStopPull(ent, pullable);

        if (TryComp<PullerComponent>(ent, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullableTarget))
            _pulling.TryStopPull(puller.Pulling.Value, pullableTarget);
    }

    public bool TryToggleItem(Entity<ArmutantComponent> ent, EntProtoId proto, string? clothingSlot = null)
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

    public bool TryInjectReagents(EntityUid uid, List<(string, FixedPoint2)> reagents)
    {
        var solution = new Shared.Chemistry.Components.Solution();
        foreach (var reagent in reagents)
        {
            solution.AddReagent(reagent.Item1, reagent.Item2);
        }

        if (!_solution.TryGetInjectableSolution(uid, out var targetSolution, out _))
            return false;

        if (!_solution.TryAddSolution(targetSolution.Value, solution))
            return false;

        return true;
    }

    private bool TryEquipArmor(Entity<ArmutantComponent> ent,
    EntProtoId proto,
    string slot,
    Dictionary<string, EntityUid> equipment)
    {
        if (equipment.ContainsKey(proto.Id))
            return false;

        var item = Spawn(proto, Transform(ent).Coordinates);

        if (!_inventory.TryEquip(ent, item, slot, force: true))
        {
            QueueDel(item);
            return false;
        }

        equipment.Add(proto.Id, item);
        return true;
    }

    private void RemoveAllArmor(Entity<ArmutantComponent> ent,
    Dictionary<string, EntityUid> equipment)
    {
        foreach (var item in equipment.Values)
        {
            if (EntityManager.EntityExists(item))
            {
                if (_inventory.TryGetSlotContainer(ent, item.ToString(), out _, out var slotDefinition))
                {
                    _inventory.TryUnequip(ent, slotDefinition.Name);
                }
                QueueDel(item);
            }
        }
        equipment.Clear();
    }
}
