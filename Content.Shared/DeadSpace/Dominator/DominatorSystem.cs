// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Emag.Components;
using Robust.Shared.Utility;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Ghost;
using Content.Shared.Item;

namespace Content.Shared.DeadSpace.Dominator;

public sealed partial class DominatorSystem : EntitySystem
{
    private const string IdSlot = "id";

    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DominatorComponent, ActivateInWorldEvent>(OnInteractHandEvent);
        SubscribeLocalEvent<DominatorComponent, GetVerbsEvent<Verb>>(OnGetVerb);
        SubscribeLocalEvent<DominatorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DominatorComponent, GetVerbsEvent<AlternativeVerb>>(GetAlternativeVerb);
        SubscribeLocalEvent<DominatorComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<DominatorComponent, GotEquippedHandEvent>(OnHandEquip);
        SubscribeLocalEvent<DominatorComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<IdCardComponent, GotEquippedEvent>(OnIdCardEquipped);
        SubscribeLocalEvent<IdCardComponent, GotUnequippedEvent>(OnIdCardUnequipped);
    }

    private void OnExamined(EntityUid uid, DominatorComponent component, ExaminedEvent args)
    {
        if (component.FireModes.Count < 2)
            return;

        var fireMode = GetMode(component);

        if (!_prototypeManager.TryIndex<EntityPrototype>(fireMode.Prototype, out var proto))
            return;

        args.PushMarkup(Loc.GetString("gun-set-fire-mode", ("mode", proto.Name)));

        if (args.IsInDetailsRange && HasComp<EmaggedComponent>(uid))
            args.PushMarkup(Loc.GetString("dominator-compromised-examine"));
    }

    private BatteryWeaponFireMode GetMode(DominatorComponent component)
    {
        return component.FireModes[component.CurrentFireMode];
    }

    private void OnGetVerb(EntityUid uid, DominatorComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (component.FireModes.Count < 2)
            return;

        for (var i = 0; i < component.FireModes.Count; i++)
        {
            var fireMode = component.FireModes[i];
            var entProto = _prototypeManager.Index<EntityPrototype>(fireMode.Prototype);
            var index = i;

            var v = new Verb
            {
                Priority = 1,
                Category = VerbCategory.SelectType,
                Text = entProto.Name,
                Disabled = i == component.CurrentFireMode,
                Impact = LogImpact.Low,
                DoContactInteraction = true,
                Act = () =>
                {
                    SetFireMode(uid, component, index, args.User);
                }
            };

            args.Verbs.Add(v);
        }
    }

    private void OnInteractHandEvent(EntityUid uid, DominatorComponent component, ActivateInWorldEvent args)
    {
        if (component.FireModes.Count < 2)
            return;

        if (TryComp<UseDelayComponent>(uid, out var useDelay)
            && _useDelay.IsDelayed((uid, useDelay)))
            return;

        CycleFireMode(uid, component, args.User);
    }

    private void CycleFireMode(EntityUid uid, DominatorComponent component, EntityUid user)
    {
        if (component.FireModes.Count < 2)
            return;

        var index = (component.CurrentFireMode + 1) % component.FireModes.Count;
        SetFireMode(uid, component, index, user);
    }

    private void SetFireMode(EntityUid uid, DominatorComponent component, int index, EntityUid? user = null)
    {
        if (TryComp<UseDelayComponent>(uid, out var useDelay)
            && _useDelay.IsDelayed((uid, useDelay)))
            return;

        if (!HasComp<EmaggedComponent>(uid))
        {
            if (user != null)
            {
                if (!HasDominatorAccess(component))
                {
                    _popupSystem.PopupClient(Loc.GetString("dominator-permission-denied"), uid, user.Value);
                    return;
                }
            }
        }

        var fireMode = component.FireModes[index];
        component.CurrentFireMode = index;
        Dirty(uid, component);

        if (TryComp(uid, out BatteryAmmoProviderComponent? projectileBatteryAmmoProvider))
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(fireMode.Prototype, out var prototype))
                return;

            projectileBatteryAmmoProvider.Prototype = fireMode.Prototype;
            projectileBatteryAmmoProvider.FireCost = fireMode.FireCost;
            Dirty(uid, projectileBatteryAmmoProvider);
            _gun.UpdateShots((uid, projectileBatteryAmmoProvider));

            if (user != null)
            {
                _popupSystem.PopupClient(Loc.GetString("gun-set-fire-mode", ("mode", prototype.Name)), uid, user.Value);
                _audio.PlayPredicted(fireMode.SwitchSound, uid, user, AudioParams.Default.WithVolume(-4f));
            }
        }

        if (TryComp(uid, out AppearanceComponent? appearance))
        {
            _appearance.QueueUpdate(uid, appearance); // for icon update
            _item.VisualsChanged(uid); // for inhand update
        }

        if (useDelay != null)
            _useDelay.TryResetDelay((uid, useDelay));
    }

    public void GetAlternativeVerb(EntityUid uid, DominatorComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess)
            return;

        if (!args.CanInteract && !HasComp<GhostComponent>(args.User))
            return;

        if (component.OwnerIdCard == null && !HasComp<EmaggedComponent>(uid))
        {
            var setOwnerIdCardVerb = new AlternativeVerb
            {
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/character.svg.192dpi.png")),
                Text = Loc.GetString("dominator-verb-set-owner"),
                Priority = 1,
                Act = () =>
                {
                    if (!args.CanAccess)
                        return;

                    if (!args.CanInteract && !HasComp<GhostComponent>(args.User))
                        return;

                    if (!_idCard.TryFindIdCard(args.User, out var idCard))
                    {
                        _popupSystem.PopupClient(Loc.GetString("dominator-id-card-not-found"), uid, args.User);
                    }
                    else
                    {
                        if (TryComp<UseDelayComponent>(uid, out var useDelay) && !_useDelay.IsDelayed((uid, useDelay)))
                        {
                            component.OwnerIdCard = idCard.Owner;
                            Dirty(uid, component);
                            _audio.PlayPredicted(component.SetOwnerSound, uid, args.User, AudioParams.Default.WithVolume(-4f));
                            if (useDelay != null)
                                _useDelay.TryResetDelay((uid, useDelay));
                        }
                    }
                }
            };
            args.Verbs.Add(setOwnerIdCardVerb);
        }
        if (component.OwnerIdCard != null && !HasComp<EmaggedComponent>(uid))
        {
            var clearOwnerIdCardVerb = new AlternativeVerb
            {
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Text = Loc.GetString("dominator-verb-clear-owner"),
                Priority = 1,
                Act = () =>
                {
                    if (!args.CanAccess)
                        return;

                    if (!args.CanInteract && !HasComp<GhostComponent>(args.User))
                        return;

                    if (!_access.IsAllowed(args.User, uid))
                    {
                        _popupSystem.PopupClient(Loc.GetString("dominator-permission-denied"), uid, args.User);
                    }
                    else
                    {
                        if (TryComp<UseDelayComponent>(uid, out var useDelay) && !_useDelay.IsDelayed((uid, useDelay)))
                        {
                            component.OwnerIdCard = null;
                            Dirty(uid, component);
                            _audio.PlayPredicted(component.ClearOwnerSound, uid, args.User, AudioParams.Default.WithVolume(-4f));
                            if (useDelay != null)
                                _useDelay.TryResetDelay((uid, useDelay));
                        }
                    }
                }
            };
            args.Verbs.Add(clearOwnerIdCardVerb);
        }
    }

    private void OnShotAttempted(EntityUid uid, DominatorComponent component, ref ShotAttemptedEvent args)
    {
        if (HasComp<EmaggedComponent>(uid))
            return;

        if (!HasDominatorAccess(component))
        {
            args.Cancel();
            _popupSystem.PopupClient(Loc.GetString("dominator-permission-denied"), uid, args.User);
            return;
        }

        if (TryComp<UseDelayComponent>(uid, out var useDelay) && _useDelay.IsDelayed((uid, useDelay)))
            return;

        if (TryComp(uid, out BatteryAmmoProviderComponent? projectileBatteryAmmoProvider) && projectileBatteryAmmoProvider.Shots == 0)
        {
            _audio.PlayPredicted(component.LowBatterySound, uid, args.User, AudioParams.Default.WithVolume(-4f));
            if (useDelay != null)
                _useDelay.TryResetDelay((uid, useDelay));
        }
    }

    /// <summary>
    ///     Because of prediction we cant check for id-card in OnShotAttempted, so we check every time entity thats grabs dominator. FIX AND NUKE THIS PLEASE.
    /// </summary>
    private void OnHandEquip(EntityUid uid, DominatorComponent component, GotEquippedHandEvent args)
    {
        RefreshLastHoldingIdCard(uid, component, args.User);
    }

    private void OnIdCardEquipped(Entity<IdCardComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != IdSlot)
            return;

        RefreshHeldDominators(args.Equipee);
    }

    private void OnIdCardUnequipped(Entity<IdCardComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != IdSlot)
            return;

        RefreshHeldDominators(args.Equipee);
    }

    private void RefreshHeldDominators(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp(held, out DominatorComponent? dominator))
                RefreshLastHoldingIdCard(held, dominator, user);
        }
    }

    private void RefreshLastHoldingIdCard(EntityUid uid, DominatorComponent component, EntityUid user)
    {
        EntityUid? idCardUid = null;

        if (_idCard.TryFindIdCard(user, out var idCard))
            idCardUid = idCard.Owner;

        if (component.LastHoldingIdCard == idCardUid)
            return;

        component.LastHoldingIdCard = idCardUid;
        Dirty(uid, component);
    }

    private void OnEmagged(EntityUid uid, DominatorComponent component, ref GotEmaggedEvent args)
    {
        _audio.PlayPredicted(component.EmagSound, uid, args.UserUid);
        args.Handled = true;
    }

    private bool HasDominatorAccess(DominatorComponent component)
    {
        return component.OwnerIdCard != null
            && component.OwnerIdCard == component.LastHoldingIdCard;
    }
}
