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

    private void OnHolderMapInit(Entity<UniformAccessoryHolderComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        if (ent.Comp.StartingAccessories is not { } startingAccessories)
            return;
        foreach (var accessoryId in startingAccessories)
        {
            SpawnInContainerOrDrop(accessoryId, ent.Owner, ent.Comp.ContainerId);
        }
    }

    private void OnHolderInteractUsing(Entity<UniformAccessoryHolderComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp(args.Used, out UniformAccessoryComponent? accessory))
            return;
        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        args.Handled = true;
        if (!ent.Comp.AllowedCategories.Contains(accessory.Category))
        {
            _popup.PopupClient(Loc.GetString("rmc-uniform-accessory-fail-not-allowed"),
                args.User,
                args.User,
                PopupType.SmallCaution);
            return;
        }
        var counts = new Dictionary<string, int>();
        foreach (var inserted in container.ContainedEntities)
        {
            if (!TryComp<UniformAccessoryComponent>(inserted, out var ins))
                continue;
            counts[ins.Category] = counts.GetValueOrDefault(ins.Category) + 1;
        }
        if (counts.TryGetValue(accessory.Category, out var amount) && accessory.Limit <= amount)
        {
            _popup.PopupClient(Loc.GetString("rmc-uniform-accessory-fail-limit"),
                args.User,
                args.User,
                PopupType.SmallCaution);
            return;
        }
        _container.Insert(args.Used, container);
        _item.VisualsChanged(ent);
    }

    private void OnHolderGotEquipped(Entity<UniformAccessoryHolderComponent> ent, ref GotEquippedEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;
        _item.VisualsChanged(ent);
    }

    private void OnHolderGetVerbs(Entity<UniformAccessoryHolderComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container)
            || container.ContainedEntities.Count == 0)
            return;

        if (args.Verbs.Any(v => v.Category?.Text == Loc.GetString("rmc-uniform-accessory-remove")))
            return;

        var user = args.User;
        var category = new VerbCategory(Loc.GetString("rmc-uniform-accessory-remove"), null);

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
                    var ev = new RemoveAccessoryEvent(ent, accessory, user);
                    RaiseLocalEvent(ev);
                },
                Priority = 0
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnRemoveAccessory(RemoveAccessoryEvent args)
    {
        if (!_container.TryGetContainer(args.Holder, "rmc_uniform_accessories", out var container))
            return;
        if (_container.Remove(args.Accessory, container))
        {
            _hands.TryPickupAnyHand(args.User, args.Accessory);
            _item.VisualsChanged(args.Holder);
        }
    }

    private sealed partial class RemoveAccessoryEvent : EntityEventArgs
    {
        public readonly EntityUid Holder;
        public readonly EntityUid Accessory;
        public readonly EntityUid User;

        public RemoveAccessoryEvent(Entity<UniformAccessoryHolderComponent> holder, EntityUid accessory, EntityUid user)
        {
            Holder = holder;
            Accessory = accessory;
            User = user;
        }
    }

    public bool BelongsToUser(NetEntity user, EntityUid target)
    {
        return user == GetNetEntity(target);
    }
}
