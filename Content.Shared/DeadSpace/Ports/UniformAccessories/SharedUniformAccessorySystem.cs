using System.Linq;
using Content.Shared.DeadSpace.Ports.UniformAccessories.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared.DeadSpace.Ports.UniformAccessories;

public abstract partial class SharedUniformAccessorySystem : EntitySystem
{
    private const string ContainerId = "rmc_uniform_accessories";
    private const string RemoveCategoryKey = "rmc-uniform-accessory-remove";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniformAccessoryHolderComponent, MapInitEvent>(OnHolderMapInit);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, InteractUsingEvent>(OnHolderInteractUsing);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, GotEquippedEvent>(OnHolderGotEquipped);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, GetVerbsEvent<Verb>>(OnHolderGetVerbs);
        SubscribeLocalEvent<RemoveAccessoryEvent>(OnRemoveAccessory);
    }

    private void OnHolderMapInit(Entity<UniformAccessoryHolderComponent> holder, ref MapInitEvent eventArgs)
    {
        _container.EnsureContainer<Container>(holder, ContainerId);
        if (holder.Comp.StartingAccessories is not { } startingAccessories)
            return;
        foreach (var accessoryId in startingAccessories)
        {
            SpawnInContainerOrDrop(accessoryId, holder.Owner, ContainerId);
        }
    }

    private void OnHolderInteractUsing(Entity<UniformAccessoryHolderComponent> holder, ref InteractUsingEvent eventArgs)
    {
        if (!TryComp(eventArgs.Used, out UniformAccessoryComponent? accessory))
            return;

        var container = _container.EnsureContainer<Container>(holder, ContainerId);
        eventArgs.Handled = true;

        if (!holder.Comp.AllowedCategories.Contains(accessory.Category))
        {
            _popup.PopupClient(Loc.GetString("rmc-uniform-accessory-fail-not-allowed"),
                eventArgs.User,
                eventArgs.User,
                PopupType.SmallCaution);
            return;
        }

        var categoryCounts = new Dictionary<string, int>();
        foreach (var entity in container.ContainedEntities)
        {
            if (!TryComp<UniformAccessoryComponent>(entity, out var comp))
                continue;
            categoryCounts[comp.Category] = categoryCounts.GetValueOrDefault(comp.Category) + 1;
        }

        if (categoryCounts.TryGetValue(accessory.Category, out var count) && accessory.Limit <= count)
        {
            _popup.PopupClient(Loc.GetString("rmc-uniform-accessory-fail-limit"),
                eventArgs.User,
                eventArgs.User,
                PopupType.SmallCaution);
            return;
        }

        _container.Insert(eventArgs.Used, container);
        _item.VisualsChanged(holder);
    }

    private void OnHolderGotEquipped(Entity<UniformAccessoryHolderComponent> holder, ref GotEquippedEvent eventArgs)
    {
        if (!_container.TryGetContainer(holder, ContainerId, out _))
            return;
        _item.VisualsChanged(holder);
    }

    private void OnHolderGetVerbs(Entity<UniformAccessoryHolderComponent> holder, ref GetVerbsEvent<Verb> eventArgs)
    {
        if (!eventArgs.CanAccess || !eventArgs.CanInteract)
            return;

        if (!_container.TryGetContainer(holder, ContainerId, out var container) ||
            container.ContainedEntities.Count == 0)
            return;

        var removeCategoryText = Loc.GetString(RemoveCategoryKey);
        if (eventArgs.Verbs.Any(v => v.Category?.Text == removeCategoryText))
            return;

        var interactor = eventArgs.User;
        var category = new VerbCategory(removeCategoryText, null);

        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<MetaDataComponent>(accessory, out var meta))
                continue;

            var verb = new Verb
            {
                Text = meta.EntityName,
                IconEntity = GetNetEntity(accessory),
                Category = category,
                Act = () =>
                {
                    var ev = new RemoveAccessoryEvent(holder, accessory, interactor);
                    RaiseLocalEvent(ev);
                },
                Priority = 0,
            };
            eventArgs.Verbs.Add(verb);
        }
    }

    private void OnRemoveAccessory(RemoveAccessoryEvent eventArgs)
    {
        if (!_container.TryGetContainer(eventArgs.Holder, ContainerId, out var container))
            return;

        if (_container.Remove(eventArgs.Accessory, container))
        {
            _hands.TryPickupAnyHand(eventArgs.User, eventArgs.Accessory);
            _item.VisualsChanged(eventArgs.Holder);
        }
    }

    private sealed class RemoveAccessoryEvent : EntityEventArgs
    {
        public readonly EntityUid Accessory;
        public readonly EntityUid Holder;
        public readonly EntityUid User;

        public RemoveAccessoryEvent(Entity<UniformAccessoryHolderComponent> holder, EntityUid accessory, EntityUid user)
        {
            Holder = holder;
            Accessory = accessory;
            User = user;
        }
    }
}
