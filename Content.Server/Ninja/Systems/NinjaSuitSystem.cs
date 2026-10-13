using Content.Server.DeadSpace.Ninja.Components;
using Content.Server.DeadSpace.Ninja.Systems;
using Content.Server.Ninja.Events;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell.Components;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Ninja.Systems;

/// <summary>
/// Handles power cell upgrading and actions.
/// TODO: Move all of this to shared and predict it
/// </summary>
public sealed class NinjaSuitSystem : SharedNinjaSuitSystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    //DS14-start
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly AutoDustSystem _autoDust = default!;
    //DS14-end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSuitComponent, ContainerIsInsertingAttemptEvent>(OnSuitInsertAttempt);
        SubscribeLocalEvent<NinjaSuitComponent, RecallKatanaEvent>(OnRecallKatana);
        //DS14-start
        SubscribeLocalEvent<NinjaSuitComponent, OpenSpiderOSEvent>(OnOpenOS);

        // DS14-start
        SubscribeLocalEvent<NinjaSuitComponent, EntInsertedIntoContainerMessage>(OnSuitCellInserted);
        SubscribeLocalEvent<NinjaSuitComponent, EntRemovedFromContainerMessage>(OnSuitCellRemoved);
        // DS14-end
        //DS14-end
    }

    protected override void NinjaEquipped(Entity<NinjaSuitComponent> ent, Entity<SpaceNinjaComponent> user)
    {
        base.NinjaEquipped(ent, user);

        _ninja.SetSuitPowerAlert(user);

        // raise event to let ninja components get starting battery
        _ninja.GetNinjaBattery(user.Owner, out var uid, out var _);

        if (uid is not { } battery_uid)
            return;

        var ev = new NinjaBatteryChangedEvent(battery_uid, ent.Owner);
        RaiseLocalEvent(ent, ref ev);
        RaiseLocalEvent(user, ref ev);
    }

    private void OnSuitInsertAttempt(EntityUid uid, NinjaSuitComponent comp, ContainerIsInsertingAttemptEvent args)
    {
        // this is for handling battery upgrading, not stopping actions from being added
        // if another container like ActionsContainer is specified, don't handle it
        if (TryComp<PowerCellSlotComponent>(uid, out var slot) && args.Container.ID != slot.CellSlotId)
            return;

        // no power cell for some reason??? allow it
        if (!_powerCell.TryGetBatteryFromSlot(uid, out var battery))
            return;

        if (!TryComp<BatteryComponent>(args.EntityUid, out var inserting))
        {
            args.Cancel();
            return;
        }

        var user = Transform(uid).ParentUid;

        // can only upgrade power cell, not swap to recharge instantly otherwise ninja could just swap batteries with flashlights in maints for easy power
        //DS14-start
        if (GetCellScore(inserting) <= GetCellScore(battery.Value))
        //DS14-end
        {
            args.Cancel();
            Popup.PopupEntity(Loc.GetString("ninja-cell-downgrade"), user, user);
            return;
        }

        // tell ninja abilities that use battery to update it so they don't use charge from the old one
        if (!_ninja.IsNinja(user))
            return;

        var ev = new NinjaBatteryChangedEvent(args.EntityUid, uid);
        RaiseLocalEvent(uid, ref ev);
        RaiseLocalEvent(user, ref ev);
    }

    // DS14-start
    private void OnSuitCellInserted(EntityUid uid, NinjaSuitComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (TryComp<PowerCellSlotComponent>(uid, out var slot) && args.Container.ID != slot.CellSlotId)
            return;

        if (!TryComp<BatterySelfRechargerComponent>(args.Entity, out var recharger))
            return;

        var disabled = EnsureComp<NinjaSuitBatteryComponent>(args.Entity);
        disabled.AutoRechargeRate = recharger.AutoRechargeRate;
        disabled.AutoRechargePauseTime = recharger.AutoRechargePauseTime;
        disabled.NextAutoRecharge = recharger.NextAutoRecharge;

        RemComp<BatterySelfRechargerComponent>(args.Entity);
    }

    private void OnSuitCellRemoved(EntityUid uid, NinjaSuitComponent comp, EntRemovedFromContainerMessage args)
    {
        if (TryComp<PowerCellSlotComponent>(uid, out var slot) && args.Container.ID != slot.CellSlotId)
            return;

        if (!TryComp<NinjaSuitBatteryComponent>(args.Entity, out var disabled))
            return;

        var recharger = EnsureComp<BatterySelfRechargerComponent>(args.Entity);
        recharger.AutoRechargeRate = disabled.AutoRechargeRate;
        recharger.AutoRechargePauseTime = disabled.AutoRechargePauseTime;
        recharger.NextAutoRecharge = disabled.NextAutoRecharge;
        Dirty(args.Entity, recharger);

        RemComp<NinjaSuitBatteryComponent>(args.Entity);

        _battery.RefreshChargeRate(args.Entity);
    }
    // DS14-end

    private float GetCellScore(BatteryComponent battcomp)
    {
        // DS14-start
        return battcomp.MaxCharge;
        // DS14-end
    }

    protected override void UserUnequippedSuit(Entity<NinjaSuitComponent> ent, Entity<SpaceNinjaComponent> user)
    {
        base.UserUnequippedSuit(ent, user);

        // remove power indicator
        _ninja.SetSuitPowerAlert(user);
    }

    private void OnRecallKatana(Entity<NinjaSuitComponent> ent, ref RecallKatanaEvent args)
    {
        var (uid, comp) = ent;
        var user = args.Performer;
        if (!_ninja.NinjaQuery.TryComp(user, out var ninja) || ninja.Katana == null)
            return;

        args.Handled = true;

        var katana = ninja.Katana.Value;
        var coords = _transform.GetWorldPosition(katana);
        var distance = (_transform.GetWorldPosition(user) - coords).Length();

        // DS14-start
        if (!_ninja.GetNinjaBattery(user, out _, out var battery))
        {
            Popup.PopupEntity(Loc.GetString("ninja-no-power"), user, user);
            return;
        }
        // DS14-end

        var chargeNeeded = distance * comp.RecallCharge;

        // DS14-start
        if (chargeNeeded >= battery.MaxCharge)
            chargeNeeded = battery.MaxCharge * comp.RecallOverMaxChargeRatio;
        // DS14-end

        if (!_ninja.TryUseCharge(user, chargeNeeded))
        {
            Popup.PopupEntity(Loc.GetString("ninja-no-power"), user, user);
            return;
        }

        // TODO: teleporting into belt slot
        var message = _hands.TryPickupAnyHand(user, katana)
            ? "ninja-katana-recalled"
            : "ninja-hands-full";

        Popup.PopupEntity(Loc.GetString(message), user, user);
    }

    //DS14-start
    private void OnOpenOS(Entity<NinjaSuitComponent> ent, ref OpenSpiderOSEvent args)
    {
        var (uid, _) = ent;

        // Без этого UI открывался бы любому, кто поднял костюм с пола.
        if (!_ninja.IsNinja(args.Performer))
        {
            if (!TryComp<AutoDustMarkerComponent>(args.Performer, out var marker))
                return;

            _autoDust.ActivateAutoDust(args.Performer, marker);
            return;
        }

        if (!_uiSystem.HasUi(uid, SpiderOSUiKey.Key))
            return;

        _uiSystem.TryToggleUi(uid, SpiderOSUiKey.Key, args.Performer);
    }
    //DS14-end
}
