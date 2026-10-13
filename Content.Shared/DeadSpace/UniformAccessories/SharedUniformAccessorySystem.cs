using Content.Shared.DeadSpace.UniformAccessories.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.UniformAccessories;

public abstract class SharedUniformAccessorySystem : EntitySystem
{
    private const string RemoveCategoryKey = "uniform-accessory-remove";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniformAccessoryHolderComponent, MapInitEvent>(OnHolderMapInit);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, InteractUsingEvent>(OnHolderInteractUsing);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, GotEquippedEvent>(OnHolderGotEquipped);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, GetVerbsEvent<Verb>>(OnHolderGetVerbs);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, ExaminedEvent>(OnExamineAccessories);
        SubscribeLocalEvent<RemoveAccessoryEvent>(OnRemoveAccessory);
    }

    private void OnHolderMapInit(Entity<UniformAccessoryHolderComponent> holder, ref MapInitEvent args)
    {
        holder.Comp.AccessoryContainer =
            _container.EnsureContainer<Container>(holder, UniformAccessoryHolderComponent.ContainerId);
        UpdateExamineData(holder);
    }

    private void OnHolderInteractUsing(Entity<UniformAccessoryHolderComponent> holder, ref InteractUsingEvent args)
    {
        if (!TryComp(args.Used, out UniformAccessoryComponent? accessory))
            return;

        var container = holder.Comp.AccessoryContainer;
        if (container == null)
            return;

        args.Handled = true;

        if (!holder.Comp.AllowedCategories.Contains(accessory.Category))
        {
            _popup.PopupClient(Loc.GetString("uniform-accessory-fail-not-allowed"),
                args.User,
                args.User,
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
            _popup.PopupClient(Loc.GetString("uniform-accessory-fail-limit"),
                args.User,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        if (TerminatingOrDeleted(args.Used) || TerminatingOrDeleted(holder))
            return;

        if (Transform(holder).GridUid is { } grid && TerminatingOrDeleted(grid))
            return;

        _container.Insert(args.Used, container);
        UpdateExamineData(holder);
        _item.VisualsChanged(holder);
    }

    private void OnHolderGotEquipped(Entity<UniformAccessoryHolderComponent> holder, ref GotEquippedEvent args)
    {
        if (holder.Comp.AccessoryContainer == null)
            return;
        _item.VisualsChanged(holder);
    }

    private void OnHolderGetVerbs(Entity<UniformAccessoryHolderComponent> holder, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var container = holder.Comp.AccessoryContainer;
        if (container == null || container.ContainedEntities.Count == 0)
            return;

        var removeCategoryText = Loc.GetString(RemoveCategoryKey);
        foreach (var verb in args.Verbs)
        {
            if (verb.Category?.Text == removeCategoryText)
                return;
        }

        var interactor = args.User;
        var category = new VerbCategory(removeCategoryText, null);

        foreach (var accessory in container.ContainedEntities)
        {
            var meta = Comp<MetaDataComponent>(accessory);

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
            args.Verbs.Add(verb);
        }
    }

    private void OnRemoveAccessory(RemoveAccessoryEvent args)
    {
        var container = CompOrNull<UniformAccessoryHolderComponent>(args.Holder)?.AccessoryContainer;
        if (container == null)
            return;

        if (_container.Remove(args.Accessory, container))
        {
            if (TryComp<UniformAccessoryHolderComponent>(args.Holder, out var holder))
                UpdateExamineData((args.Holder, holder));
            _hands.TryPickupAnyHand(args.User, args.Accessory);
            _item.VisualsChanged(args.Holder);
        }
    }

    private void OnExamineAccessories(Entity<UniformAccessoryHolderComponent> holder, ref ExaminedEvent args)
    {
        // Equipped clothing is stored inside the wearer's inventory container. For another player
        // IsInDetailsRange is false even when the stripping UI allows examining that clothing.
        // The examine system has already checked that the target itself can be examined.
        if (!TryGetAccessoriesMarkup(holder.Comp, out var accessoriesList))
            return;

        args.PushMarkup(Loc.GetString("uniform-accessory-examine-holder", ("accessories", accessoriesList)));
    }

    private bool TryGetAccessoriesMarkup(UniformAccessoryHolderComponent holder, out string accessoriesList)
    {
        accessoriesList = string.Empty;
        if (holder.ExamineNames.Count == 0)
            return false;

        var accessories = new List<string>();
        for (var i = 0; i < holder.ExamineNames.Count; i++)
        {
            var colorHex = i < holder.ExamineColors.Count ? holder.ExamineColors[i] : "#FFFF55";
            accessories.Add($"[color={colorHex}]{holder.ExamineNames[i]}[/color]");
        }

        if (accessories.Count == 0)
            return false;

        accessoriesList = string.Join(", ", accessories);
        return true;
    }

    private void UpdateExamineData(Entity<UniformAccessoryHolderComponent> holder)
    {
        if (_net.IsClient)
            return;

        holder.Comp.ExamineNames.Clear();
        holder.Comp.ExamineColors.Clear();

        if (holder.Comp.AccessoryContainer != null)
        {
            foreach (var accessory in holder.Comp.AccessoryContainer.ContainedEntities)
            {
                if (!TryComp(accessory, out MetaDataComponent? metaData))
                    continue;

                holder.Comp.ExamineNames.Add(metaData.EntityName);
                holder.Comp.ExamineColors.Add(
                    TryComp<UniformAccessoryComponent>(accessory, out var acc) && acc.Color != null
                        ? acc.Color.Value.ToHex()
                        : "#FFFF55");
            }
        }

        Dirty(holder);
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
