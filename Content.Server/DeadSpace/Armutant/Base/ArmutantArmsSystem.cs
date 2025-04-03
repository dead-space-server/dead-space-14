using Content.Server.DeadSpace.Armutant.Abilities.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Content.Server.Popups;
using Content.Shared.DeadSpace.Armutant;
using Content.Shared.Actions;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Server.Body.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible;
using System.Linq;
using Robust.Server.GameObjects;
using Content.Shared.Cuffs.Components;
using Content.Server.Cuffs;

namespace Content.Server.DeadSpace.Armutant.Base;

public sealed class ArmutantArmsSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly CuffableSystem _cuffable = default!;

    public EntProtoId BladeArmPrototype = "BladeArmutant";
    public EntProtoId FistArmPrototype = "FistArmutant";
    public EntProtoId ShieldArmPrototype = "ShieldArmutant";
    public EntProtoId GunArmPrototype = "GunArmutant";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmutantArmsComponent, BladeArmutantToggleEvent>(OnToggleClaws);
        SubscribeLocalEvent<ArmutantArmsComponent, ShieldArmutantToggleEvent>(OnToggleShield);
        SubscribeLocalEvent<ArmutantArmsComponent, FistArmutantToggleEvent>(OnToggleFist);
        SubscribeLocalEvent<ArmutantArmsComponent, GunArmutantToggleEvent>(OnToggleGun);
        SubscribeLocalEvent<ArmutantArmsComponent, EnterArmutantStasisEvent>(OnEnterStasis);
        SubscribeLocalEvent<ArmutantArmsComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ArmutantArmsComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnComponentInit(EntityUid uid, ArmutantArmsComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionArmutantBladeEntity, component.ActionToggleBlade, uid);
        _action.AddAction(uid, ref component.ActionArmutantShieldEntity, component.ActionToggleShield, uid);
        _action.AddAction(uid, ref component.ActionArmutantFistEntity, component.ActionToggleFist, uid);
        _action.AddAction(uid, ref component.ActionArmutantGunEntity, component.ActionToggleGun, uid);
        _action.AddAction(uid, ref component.ActionEnterStasisEntity, component.ActionToggleEnterStasis, uid);

        SetDestructibleThreshold(uid, component.newDamageValue);
    }
    private void OnComponentShutdown(EntityUid uid, ArmutantArmsComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionArmutantBladeEntity);
        _action.RemoveAction(uid, component.ActionArmutantShieldEntity);
        _action.RemoveAction(uid, component.ActionArmutantFistEntity);
        _action.RemoveAction(uid, component.ActionArmutantGunEntity);
        _action.RemoveAction(uid, component.ActionEnterStasisEntity);
    }
    private void OnToggleClaws(EntityUid uid, ArmutantArmsComponent component, ref BladeArmutantToggleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryToggleItem(uid, BladeArmPrototype, component))
            return;

        args.Handled = true;

        if (component.MeatSound != null)
        {
            _audio.PlayPvs(component.MeatSound, uid);
        }
    }
    private void OnToggleShield(EntityUid uid, ArmutantArmsComponent component, ref ShieldArmutantToggleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryToggleItem(uid, ShieldArmPrototype, component))
            return;

        args.Handled = true;

        if (component.MeatSound != null)
        {
            _audio.PlayPvs(component.MeatSound, uid);
        }
    }
    private void OnToggleFist(EntityUid uid, ArmutantArmsComponent component, ref FistArmutantToggleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryToggleItem(uid, FistArmPrototype, component))
            return;

        args.Handled = true;

        if (component.MeatSound != null)
        {
            _audio.PlayPvs(component.MeatSound, uid);
        }
    }
    private void OnToggleGun(EntityUid uid, ArmutantArmsComponent component, ref GunArmutantToggleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryToggleItem(uid, GunArmPrototype, component))
            return;

        args.Handled = true;

        if (component.MeatSound != null)
        {
            _audio.PlayPvs(component.MeatSound, uid);
        }
    }
    private void OnEnterStasis(EntityUid uid, ArmutantArmsComponent component, ref EnterArmutantStasisEvent args)
    {
        if (component.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("armutant-stasis-enter-fail"), uid, uid);
            return;
        }

        if (_mobState.IsAlive(uid))
        {
            var othersMessage = Loc.GetString("suicide-command-default-text-others", ("name", uid));
            _popup.PopupEntity(othersMessage, uid, Robust.Shared.Player.Filter.PvsExcept(uid), true);

            var selfMessage = Loc.GetString("armutant-stasis-enter");
            _popup.PopupEntity(selfMessage, uid, uid);
        }

        if (!_mobState.IsDead(uid) || _mobState.IsCritical(uid))
            _mobState.ChangeMobState(uid, MobState.Dead);

        component.IsInStasis = true;

        Timer.Spawn(TimeSpan.FromSeconds(30), () =>
        {
            OnExitStasis(uid, component);
            if (TryComp<CuffableComponent>(uid, out var cuffs) && cuffs.Container.ContainedEntities.Count > 0)
            {
                _cuffable.Uncuff(uid, cuffs.LastAddedCuffs, cuffs.LastAddedCuffs);
            }

            var effect = Spawn(component.SelfEffect, Transform(uid).Coordinates);
            _transformSystem.SetParent(effect, uid);
        });

        args.Handled = true;
    }
    private void OnExitStasis(EntityUid uid, ArmutantArmsComponent component)
    {
        if (!component.IsInStasis)
        {
            _popup.PopupEntity(Loc.GetString("armutant-stasis-exit-fail"), uid, uid);
            return;
        }

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        _damage.SetAllDamage(uid, damageable, 0);
        _mobState.ChangeMobState(uid, MobState.Alive);
        _blood.TryModifyBloodLevel(uid, 1000);
        _blood.TryModifyBleedAmount(uid, -1000);
        _popup.PopupEntity(Loc.GetString("armutant-stasis-exit"), uid, uid);

        component.IsInStasis = false;
    }
    private void SetDestructibleThreshold(EntityUid uid, int newDamageValue)
    {
        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return;

        var bluntThreshold = destructible.Thresholds
            .OfType<DamageThreshold>()
            .FirstOrDefault(t => t.Trigger is DamageTypeTrigger damageTrigger &&
                                 damageTrigger.DamageType == "Blunt");

        if (bluntThreshold != null)
        {
            if (bluntThreshold.Trigger is DamageTypeTrigger damageTrigger)
            {
                damageTrigger.Damage = 1200;
            }
        }
        else
        {
            var newThreshold = new DamageThreshold
            {
                Trigger = new DamageTypeTrigger
                {
                    DamageType = "Blunt",
                    Damage = newDamageValue
                }
            };

            destructible.Thresholds.Add(newThreshold);
        }
    }
    public bool TryToggleItem(EntityUid uid, EntProtoId proto, ArmutantArmsComponent comp, string? clothingSlot = null)
    {
        if (!comp.Equipment.TryGetValue(proto.Id, out var item))
        {
            item = Spawn(proto, Transform(uid).Coordinates);
            if (clothingSlot != null)
            {
                if (!_inventory.TryEquip(uid, (EntityUid)item, clothingSlot, force: true))
                {
                    QueueDel(item);
                    return false;
                }
                comp.Equipment.Add(proto.Id, item);
                return true;
            }
            else if (!_hands.TryForcePickupAnyHand(uid, (EntityUid)item))
            {
                _popup.PopupEntity(Loc.GetString("armutant-fail-hands"), uid, uid);
                QueueDel(item);
                return false;
            }
            comp.Equipment.Add(proto.Id, item);
            return true;
        }

        QueueDel(item);
        comp.Equipment.Remove(proto.Id);

        return true;
    }
}
