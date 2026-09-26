using System.Linq;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DeadSpace.ItemSwitch.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.ItemSwitch;

/// <summary>
///     Handles items that can toggle between a set of states, granting components,
///     overriding in-hand sprites and switching clothing prefixes per state.
/// </summary>
public abstract class SharedItemSwitchSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;

    private EntityQuery<ItemSwitchComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<ItemSwitchComponent>();

        SubscribeLocalEvent<ItemSwitchComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ItemSwitchComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ItemSwitchComponent, GetVerbsEvent<ActivationVerb>>(OnActivateVerb);
        SubscribeLocalEvent<ItemSwitchComponent, ActivateInWorldEvent>(OnActivate);

        SubscribeLocalEvent<ClothingComponent, ItemSwitchedEvent>(UpdateClothingLayer);
    }

    private void OnInit(Entity<ItemSwitchComponent> ent, ref ComponentInit args)
    {
        Switch((ent, ent.Comp), ent.Comp.State, predicted: ent.Comp.Predictable);
    }

    private void OnUseInHand(Entity<ItemSwitchComponent> ent, ref UseInHandEvent args)
    {
        var comp = ent.Comp;

        if (args.Handled || !comp.OnUse || comp.States.Count == 0)
            return;

        args.Handled = true;

        if (comp.States.TryGetValue(Next(ent), out var state) && state.Hidden)
            return;

        Switch((ent, comp), Next(ent), args.User, predicted: comp.Predictable);
    }

    private void OnActivateVerb(Entity<ItemSwitchComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.OnActivate || ent.Comp.States.Count == 0)
            return;

        var user = args.User;
        var addedVerbs = 0;

        foreach (var state in ent.Comp.States.Where(state => !state.Value.Hidden))
        {
            if (state.Value.Verb == null)
                continue;

            args.Verbs.Add(new ActivationVerb
            {
                Text = Loc.TryGetString(state.Value.Verb, out var title) ? title : state.Value.Verb,
                Category = ItemSwitchVerbCategory.Switch,
                Act = () => Switch((ent.Owner, ent.Comp), state.Key, user, ent.Comp.Predictable)
            });
            addedVerbs++;
        }

        if (addedVerbs > 0)
            args.ExtraCategories.Add(ItemSwitchVerbCategory.Switch);
    }

    private void OnActivate(Entity<ItemSwitchComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !ent.Comp.OnActivate)
            return;

        args.Handled = true;

        if (ent.Comp.States.TryGetValue(Next(ent), out var state) && state.Hidden)
            return;

        Switch((ent.Owner, ent.Comp), Next(ent), args.User, predicted: ent.Comp.Predictable);
    }

    private static string Next(Entity<ItemSwitchComponent> ent)
    {
        var foundCurrent = false;
        foreach (var state in ent.Comp.States.Keys)
        {
            if (foundCurrent)
                return state;

            if (state == ent.Comp.State)
                foundCurrent = true;
        }

        return ent.Comp.States.Keys.First();
    }

    /// <summary>
    ///     Used when an item is attempted to be toggled.
    ///     Sets its state and swaps the state components.
    /// </summary>
    /// <returns>false if the attempt fails for any reason</returns>
    public bool Switch(Entity<ItemSwitchComponent?> ent, string? key, EntityUid? user = null, bool predicted = true)
    {
        if (key == null
            || !_query.Resolve(ent, ref ent.Comp, false)
            || !ent.Comp.States.TryGetValue(key, out var state))
            return false;

        var uid = ent.Owner;
        var comp = ent.Comp;

        if (!comp.Predictable && _netManager.IsClient)
            return true;

        var attempt = new ItemSwitchAttemptEvent
        {
            User = user,
            State = key,
        };
        RaiseLocalEvent(uid, ref attempt);

        var nextAttack = new TimeSpan(0);
        if (TryComp<MeleeWeaponComponent>(ent, out var meleeComp))
            nextAttack = meleeComp.NextAttack;

        if (comp.States.TryGetValue(comp.State, out var prevState)
            && prevState is { RemoveComponents: true, Components: not null })
            EntityManager.RemoveComponents(ent, prevState.Components);

        if (state.Components is not null)
            EntityManager.AddComponents(ent, state.Components);

        if (TryComp(ent, out meleeComp)
            && nextAttack.Ticks != 0)
            meleeComp.NextAttack = nextAttack;

        if (!comp.Predictable)
            predicted = false;

        if (attempt.Cancelled)
        {
            if (predicted)
                _audio.PlayPredicted(state.SoundFailToActivate, uid, user);
            else
                _audio.PlayPvs(state.SoundFailToActivate, uid);

            if (attempt.Popup == null || user == null)
                return false;

            if (predicted)
                _popup.PopupClient(attempt.Popup, uid, user.Value);
            else
                _popup.PopupEntity(attempt.Popup, uid, user.Value);

            return false;
        }

        if (predicted)
            _audio.PlayPredicted(state.SoundStateActivate, uid, user);
        else
            _audio.PlayPvs(state.SoundStateActivate, uid);

        comp.State = key;
        UpdateVisuals((uid, comp), key);
        Dirty(uid, comp);

        var switched = new ItemSwitchedEvent { Predicted = predicted, State = key, User = user };
        RaiseLocalEvent(uid, ref switched);

        return true;
    }

    public virtual void VisualsChanged(Entity<ItemSwitchComponent> ent, string key)
    {
    }

    protected virtual void UpdateVisuals(Entity<ItemSwitchComponent> ent, string key)
    {
        if (TryComp(ent, out AppearanceComponent? appearance))
            _appearance.SetData(ent, ItemSwitchVisuals.Switched, key, appearance);
        _item.SetHeldPrefix(ent, key);

        VisualsChanged(ent, key);
    }

    private void UpdateClothingLayer(Entity<ClothingComponent> ent, ref ItemSwitchedEvent args)
    {
        _clothing.SetEquippedPrefix(ent, args.State, ent.Comp);
    }
}