using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Timing.Components;
using Content.Shared.Timing.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Ninja.Systems;

/// <summary>
/// Handles (un)equipping and provides some API functions.
/// </summary>
public abstract partial class SharedNinjaSuitSystem : EntitySystem
{
<<<<<<< ours
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    //DS14-start
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _net = default!;
    //DS14-end
||||||| base
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
=======
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] private SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
>>>>>>> theirs

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSuitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaSuitComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NinjaSuitComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<NinjaSuitComponent, ToggleClothingCheckEvent>(OnCloakCheck);
        SubscribeLocalEvent<NinjaSuitComponent, CheckItemCreatorEvent>(OnStarCheck);
        SubscribeLocalEvent<NinjaSuitComponent, CreateItemAttemptEvent>(OnCreateStarAttempt);
        SubscribeLocalEvent<NinjaSuitComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<NinjaSuitComponent> ent, ref ClothingGotEquippedEvent args)
    {
        var user = args.Wearer;
        if (_ninja.NinjaQuery.TryComp(user, out var ninja))
            NinjaEquipped(ent, (user, ninja));
    }

    protected virtual void NinjaEquipped(Entity<NinjaSuitComponent> ent, Entity<SpaceNinjaComponent> user)
    {
        // mark the user as wearing this suit, used when being attacked among other things
        _ninja.AssignSuit(user, ent);
    }

    //DS14-start
    private void OnMapInit(Entity<NinjaSuitComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.RecallKatanaActionEntity, comp.RecallKatanaAction);
        _actionContainer.EnsureAction(uid, ref comp.OpenSpiderOSActionEntity, comp.OpenSpiderOSAction);
        Dirty(uid, comp);
    }

    /// <summary>
    /// Add all the actions when a suit is equipped by a ninja.
    /// The katana recall action is granted separately while the suit is activated.
    /// </summary>
    private void OnGetItemActions(Entity<NinjaSuitComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ent.Comp.OpenSpiderOSActionEntity);
    }
    //DS14-end

    /// <summary>
    /// Only add toggle cloak action when equipped by a ninja.
    /// </summary>
    private void OnCloakCheck(Entity<NinjaSuitComponent> ent, ref ToggleClothingCheckEvent args)
    {
        if (!_ninja.IsNinja(args.User))
            args.Cancelled = true;
    }

    private void OnStarCheck(Entity<NinjaSuitComponent> ent, ref CheckItemCreatorEvent args)
    {
        if (!_ninja.IsNinja(args.User))
            args.Cancelled = true;
    }

    //DS14-start
    private void OnCreateStarAttempt(Entity<NinjaSuitComponent> ent, ref CreateItemAttemptEvent args)
    {
        if (TryComp<SpiderOSComponent>(ent, out var spiderOS) && !spiderOS.SuitActivated)
            args.Cancelled = true;
    }
    //DS14-end

    /// <summary>
    /// Call the shared and serverside code for when anyone unequips a suit.
    /// </summary>
    private void OnUnequipped(Entity<NinjaSuitComponent> ent, ref GotUnequippedEvent args)
    {
        var user = args.EquipTarget;
        if (_ninja.NinjaQuery.TryComp(user, out var ninja))
            UserUnequippedSuit(ent, (user, ninja));
    }

    //DS14-start
    /// <summary>
    /// Force uncloaks the user and disables suit abilities.
    /// </summary>
    public void RevealNinja(Entity<NinjaSuitComponent?> ent, EntityUid user)
    {
        if (!_net.IsServer)
            return;

        if (!Resolve(ent, ref ent.Comp))
            return;

        var uid = ent.Owner;
        var comp = ent.Comp;

<<<<<<< ours
        var revealed = false;
||||||| base
        // previously cloaked, disable abilities for a short time
        _audio.PlayPredicted(comp.RevealSound, uid, user);
        Popup.PopupClient(Loc.GetString("ninja-revealed"), user, user, PopupType.MediumCaution);
        _useDelay.TryResetDelay(uid, id: comp.DisableDelayId);
    }
=======
        // previously cloaked, disable abilities for a short time
        _audio.PlayPredicted(comp.RevealSound, uid, user);
        Popup.PopupEntity(Loc.GetString("ninja-revealed"), user, user, PopupType.MediumCaution);
        _useDelay.TryResetDelay(uid, id: comp.DisableDelayId);
    }
>>>>>>> theirs

        if (TryComp<NinjaCloakComponent>(uid, out var cloak) && cloak.Enabled)
        {
            cloak.Enabled = false;
            _actions.StartUseDelay(cloak.ActionEntity);
            Dirty(uid, cloak);
            revealed = true;
        }

        if (TryComp<NinjaDisguiseComponent>(uid, out var disguise) && disguise.Disguised)
        {
            revealed = true;
            var revealedEvent = new NinjaDisguiseRevealedEvent();
            RaiseLocalEvent(uid, ref revealedEvent);
        }

        if (!revealed)
            return;

        RaiseNetworkEvent(new PlayNinjaRevealEffectMessage(GetNetEntity(uid)));
        _audio.PlayPvs(comp.RevealSound, uid);
        Popup.PopupEntity(Loc.GetString("ninja-revealed"), user, user, PopupType.MediumCaution);
    }
    //DS14-end

    /// <summary>
    /// Called when a suit is unequipped, not necessarily by a space ninja.
    /// In the future it might be changed to also have explicit deactivation via toggle.
    /// </summary>
    protected virtual void UserUnequippedSuit(Entity<NinjaSuitComponent> ent, Entity<SpaceNinjaComponent> user)
    {
        // mark the user as not wearing a suit
        _ninja.AssignSuit(user, null);

        //DS14-start
        if (TryComp<NinjaCloakComponent>(ent, out var cloak) && cloak.Enabled)
        {
            cloak.Enabled = false;
            Dirty(ent, cloak);
        }
        //DS14-end
    }
}
