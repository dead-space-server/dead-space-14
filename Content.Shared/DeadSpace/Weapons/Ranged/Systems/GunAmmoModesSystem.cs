// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Weapons.Ranged.Components;
using Content.Shared.DeadSpace.Weapons.Ranged.Events;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.DeadSpace.Weapons.Ranged.Systems;

/// <summary>
/// Fire modes of a gun (<see cref="GunAmmoModesComponent"/>), switched with the middle mouse button in combat mode: each fires the gun's
/// own self-recharging shots or the ammo loaded into one of its slots.
/// </summary>
public sealed class GunAmmoModesSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RechargeBasicEntityAmmoSystem _recharge = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunAmmoModesComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GunAmmoModesComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<GunAmmoModesComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<GunAmmoModesComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<GunAmmoModesComponent, RechargeBasicEntityAmmoCooldownEvent>(OnRechargeCooldown);
        SubscribeLocalEvent<GunAmmoModesComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<GunAmmoModesComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<GunAmmoModesComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<GunAmmoModesComponent, GetAmmoCountEvent>(OnAmmoCount);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.MouseMiddle, new PointerInputCmdHandler(OnMiddleClick))
            .Register<GunAmmoModesSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<GunAmmoModesSystem>();
        base.Shutdown();
    }

    private void OnMapInit(Entity<GunAmmoModesComponent> ent, ref MapInitEvent args)
    {
        ApplyMode(ent);
    }

    private void OnHandleState(Entity<GunAmmoModesComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // The bolt the gun fires is not networked: the client sets it itself to predict the shot.
        ApplyProto(ent);
        _gun.RefreshAmmoCount(ent);
    }

    #region Switching

    /// <summary>The middle mouse button in combat mode switches the mode of the gun in the active hand.</summary>
    private bool OnMiddleClick(ICommonSession? session, EntityCoordinates coordinates, EntityUid target)
    {
        if (_net.IsClient ||
            session?.AttachedEntity is not { Valid: true } user ||
            !_combatMode.IsInCombatMode(user) ||
            _hands.GetActiveItem(user) is not { } gun ||
            !TryComp<GunAmmoModesComponent>(gun, out var modes) ||
            modes.Modes.Count < 2 ||
            !_actionBlocker.CanInteract(user, gun))
        {
            return false;
        }

        SetMode((gun, modes), (modes.Selected + 1) % modes.Modes.Count, user);
        return true;
    }

    /// <summary>Switches the gun's mode; server-side, the switch reaches the client with the component's state.</summary>
    public void SetMode(Entity<GunAmmoModesComponent> ent, int index, EntityUid? user = null)
    {
        if (_net.IsClient || index < 0 || index >= ent.Comp.Modes.Count || index == ent.Comp.Selected)
            return;

        ent.Comp.Selected = index;
        Dirty(ent);
        ApplyMode(ent);

        _audio.PlayPvs(ent.Comp.SwitchSound, ent);
        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString("gun-ammo-modes-switched",
                    ("mode", Loc.GetString(ent.Comp.Modes[index].Name))),
                ent,
                user.Value);
        }
    }

    private void ApplyMode(Entity<GunAmmoModesComponent> ent)
    {
        ApplyProto(ent);
        _gun.RefreshModifiers(ent.Owner);

        // The charge on its way comes at the new mode's pace, and the counter shows the new mode's ammo.
        _recharge.RestartCharge(ent.Owner);
        _gun.RefreshAmmoCount(ent);
    }

    private void ApplyProto(Entity<GunAmmoModesComponent> ent)
    {
        if (ent.Comp.Current?.Proto is { } proto && TryComp<BasicEntityAmmoProviderComponent>(ent, out var basic))
            basic.Proto = proto;
    }

    private void OnExamined(Entity<GunAmmoModesComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Current is not { } mode || ent.Comp.Modes.Count < 2)
            return;

        args.PushMarkup(Loc.GetString("gun-ammo-modes-examine", ("mode", Loc.GetString(mode.Name))));
    }

    #endregion

    #region The mode's shot

    private void OnRefreshModifiers(Entity<GunAmmoModesComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.Current is not { } mode)
            return;

        args.FireRate *= mode.FireRateMultiplier;
        if (mode.SoundGunshot != null)
            args.SoundGunshot = mode.SoundGunshot;
    }

    private void OnRechargeCooldown(Entity<GunAmmoModesComponent> ent, ref RechargeBasicEntityAmmoCooldownEvent args)
    {
        if (ent.Comp.Current is { } mode)
            args.Cooldown *= mode.RechargeMultiplier;
    }

    private void OnAttemptShoot(Entity<GunAmmoModesComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled ||
            ent.Comp.Current is not { Slot: null, Cost: > 1 } mode ||
            !TryComp<BasicEntityAmmoProviderComponent>(ent, out var basic) ||
            basic.Count is not { } count ||
            count >= mode.Cost)
        {
            return;
        }

        // Not enough charge for this mode's shot: wait for the rest.
        args.Cancelled = true;
    }

    private void OnShot(Entity<GunAmmoModesComponent> ent, ref GunShotEvent args)
    {
        // The shot took one charge; the mode's dearer shot takes the rest.
        if (ent.Comp.Current is { Slot: null, Cost: > 1 } mode)
            _gun.ChangeBasicEntityAmmoCount(ent.Owner, -Math.Min(mode.Cost - 1, GetBasicCount(ent)));
    }

    private int GetBasicCount(EntityUid uid)
    {
        return TryComp<BasicEntityAmmoProviderComponent>(uid, out var basic) && basic.Count is { } count ? count : 0;
    }

    #endregion

    #region Ammo from a slot

    private void OnTakeAmmo(Entity<GunAmmoModesComponent> ent, ref TakeAmmoEvent args)
    {
        if (ent.Comp.Current?.Slot is not { } slotId ||
            !_itemSlots.TryGetSlot(ent, slotId, out var slot) ||
            slot.Item is not { } item)
        {
            return;
        }

        if (TryComp<CartridgeAmmoComponent>(item, out var cartridge))
        {
            // A single shell or grenade: out of the slot it goes, to be fired and its casing thrown out.
            if (cartridge.Spent || args.Shots <= 0 || slot.ContainerSlot == null)
                return;

            _containers.Remove(item, slot.ContainerSlot);
            args.Ammo.Add((item, cartridge));
            return;
        }

        // A magazine (or anything else that hands out ammo itself).
        RaiseLocalEvent(item, args);
    }

    private void OnAmmoCount(Entity<GunAmmoModesComponent> ent, ref GetAmmoCountEvent args)
    {
        if (ent.Comp.Current?.Slot is not { } slotId || !_itemSlots.TryGetSlot(ent, slotId, out var slot))
            return;

        if (slot.Item is not { } item)
        {
            args.Count = 0;
            args.Capacity = 1;
            return;
        }

        if (TryComp<CartridgeAmmoComponent>(item, out var cartridge))
        {
            args.Count = cartridge.Spent ? 0 : 1;
            args.Capacity = 1;
            if (!cartridge.Spent)
            {
                args.NextAmmoEntity = item;
                args.NextAmmoPrototype = cartridge.Prototype;
            }

            return;
        }

        RaiseLocalEvent(item, ref args);
    }

    #endregion
}
