// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Clothing.Components;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed partial class NinjaAppearanceSystem : SharedNinjaAppearanceSystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAppearanceItemComponent, EntParentChangedMessage>(OnItemReparented);
        SubscribeLocalEvent<NinjaAppearanceComponent, GotUnequippedEvent>(OnSuitUnequipped);
    }

    protected override void OnHelmetHiddenChanged(Entity<NinjaAppearanceComponent> ent, bool shown)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid() || !TryComp<HumanoidAppearanceComponent>(wearer, out var humanoid))
            return;

        EntityUid? helmet = null;
        foreach (var item in EnumerateWearerItems(wearer))
        {
            if (TryComp<NinjaAppearanceItemComponent>(item, out var itemComp) &&
                itemComp.ItemType == NinjaAppearanceItemType.Helmet)
            {
                helmet = item;
                break;
            }
        }

        if (helmet is not { } helmetUid ||
            !TryComp<NinjaAppearanceItemComponent>(helmetUid, out var comp) ||
            FindSuit(Transform(helmetUid).ParentUid) is not { } claim || claim.Uid != ent.Owner)
        {
            return;
        }

        if (shown)
            RestoreHideLayerClothing(wearer, helmetUid, comp, humanoid.HideLayersOnEquip);
        else
            RemoveHideLayerClothing(wearer, helmetUid, comp, humanoid.HideLayersOnEquip);
    }

    private void RemoveHideLayerClothing(EntityUid wearer, EntityUid helmet,
        NinjaAppearanceItemComponent comp, HashSet<HumanoidVisualLayers> hideable)
    {
        if (comp.SavedHideLayers != null || !TryComp<HideLayerClothingComponent>(helmet, out var hide))
            return;

        var inSlot = TryComp<ClothingComponent>(helmet, out var clothing)
            ? clothing.InSlotFlag ?? SlotFlags.NONE
            : SlotFlags.NONE;

        if (inSlot == SlotFlags.NONE)
            return;

        comp.SavedHideLayers = new Dictionary<HumanoidVisualLayers, SlotFlags>(hide.Layers);
        ToggleHiddenLayers(wearer, hide.Layers, hideable, inSlot, hidden: false);
        RemComp<HideLayerClothingComponent>(helmet);
    }

    private void RestoreHideLayerClothing(EntityUid wearer, EntityUid helmet,
        NinjaAppearanceItemComponent comp, HashSet<HumanoidVisualLayers> hideable)
    {
        if (comp.SavedHideLayers is not { } saved)
            return;

        var inSlot = TryComp<ClothingComponent>(helmet, out var clothing)
            ? clothing.InSlotFlag ?? SlotFlags.NONE
            : SlotFlags.NONE;

        if (inSlot == SlotFlags.NONE)
            return;

        var hide = EnsureComp<HideLayerClothingComponent>(helmet);
        hide.Layers = saved;
        Dirty(helmet, hide);
        comp.SavedHideLayers = null;

        ToggleHiddenLayers(wearer, saved, hideable, inSlot, hidden: true);
    }

    private void ToggleHiddenLayers(EntityUid wearer, Dictionary<HumanoidVisualLayers, SlotFlags> layers,
        HashSet<HumanoidVisualLayers> hideable, SlotFlags inSlot, bool hidden)
    {
        foreach (var (layer, validSlots) in layers)
        {
            if (hideable.Contains(layer) && validSlots.HasFlag(inSlot))
                _humanoid.SetLayerVisibility(wearer, layer, visible: !hidden, source: inSlot);
        }
    }

    private void OnSuitUnequipped(Entity<NinjaAppearanceComponent> ent, ref GotUnequippedEvent args)
    {
        var colorway = ent.Comp.Colorway;
        var style = ent.Comp.Style;

        foreach (var item in EnumerateWearerItems(args.Equipee))
        {
            if (item == ent.Owner)
                continue;

            if (!TryComp<NinjaAppearanceItemComponent>(item, out var itemComp) ||
                FindSuit(Transform(item).ParentUid) != null)
            {
                continue;
            }

            if (itemComp.FrozenColor != colorway || itemComp.FrozenStyle != style)
            {
                itemComp.FrozenColor = colorway;
                itemComp.FrozenStyle = style;
                Dirty(item, itemComp);
            }
        }
    }

    private IEnumerable<EntityUid> EnumerateWearerItems(EntityUid wearer)
    {
        if (TryComp<InventoryComponent>(wearer, out var inventory))
        {
            var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
            while (enumerator.NextItem(out var item, out _))
            {
                yield return item;
                foreach (var contained in EnumerateContainedItems(item))
                    yield return contained;
            }
        }
    }

    private IEnumerable<EntityUid> EnumerateContainedItems(EntityUid item)
    {
        if (!TryComp<ContainerManagerComponent>(item, out var containerManager))
            yield break;

        foreach (var container in containerManager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                yield return contained;
                foreach (var inner in EnumerateContainedItems(contained))
                    yield return inner;
            }
        }
    }

    private void OnItemReparented(Entity<NinjaAppearanceItemComponent> ent, ref EntParentChangedMessage args)
    {
        if (FindSuit(args.Transform.ParentUid) != null)
            return;

        NinjaColorway? color = null;
        NinjaStyle? style = null;

        if (TryComp<NinjaAppearanceComponent>(ent.Owner, out var own))
        {
            color = own.Colorway;
            style = own.Style;
        }
        else if (FindSuit(args.OldParent) is { } oldSuit)
        {
            color = oldSuit.Comp.Colorway;
            style = oldSuit.Comp.Style;
        }

        if (color is { } frozen && style is { } frozenStyle &&
            (ent.Comp.FrozenColor != frozen || ent.Comp.FrozenStyle != frozenStyle))
        {
            ent.Comp.FrozenColor = frozen;
            ent.Comp.FrozenStyle = frozenStyle;
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private (EntityUid Uid, NinjaAppearanceComponent Comp)? FindSuit(EntityUid? root)
    {
        if (root is not { } parent || !parent.IsValid())
            return null;

        while (parent.IsValid())
        {
            if (!Exists(parent))
                return null;

            if (TryComp<NinjaAppearanceComponent>(parent, out var comp))
                return (parent, comp);

            var enumerator = _inventory.GetSlotEnumerator(parent);
            while (enumerator.NextItem(out var wornItem, out _))
            {
                if (TryComp<NinjaAppearanceComponent>(wornItem, out var wornComp))
                    return (wornItem, wornComp);
            }

            parent = Transform(parent).ParentUid;
        }

        return null;
    }
}