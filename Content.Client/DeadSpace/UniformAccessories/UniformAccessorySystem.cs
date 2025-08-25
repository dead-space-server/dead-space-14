using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DeadSpace.UniformAccessories;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Containers;

namespace Content.Client.DeadSpace.UniformAccessories;

public sealed class UniformAccessorySystem : SharedUniformAccessorySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public event Action? PlayerAccessoryVisualsUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnHolderGetEquipmentVisuals,
            after: [typeof(ClothingSystem)]);
        SubscribeLocalEvent<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent, AfterAutoHandleStateEvent>(OnHolderAfterState);
        SubscribeLocalEvent<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent, EntInsertedIntoContainerMessage>(
            OnHolderInsertedContainer);
        SubscribeLocalEvent<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnHolderRemovedContainer);
        SubscribeLocalEvent<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent, EquipmentVisualsUpdatedEvent>(OnHolderVisualsUpdated,
            after: [typeof(ClothingSystem)]);
    }

    private void OnHolderGetEquipmentVisuals(Entity<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent> ent,
        ref GetEquipmentVisualsEvent args)
    {
        if (TryComp(_player.LocalEntity, out HumanoidAppearanceComponent? humanoid) && ShouldHideAccessories(humanoid))
            return;

        var clothingSprite = CompOrNull<SpriteComponent>(ent);

        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;

        var index = 0;
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<Shared.DeadSpace.UniformAccessories.UniformAccessoryComponent>(accessory, out var accessoryComp))
                continue;

            var layerKey = GetLayerKey(accessory, accessoryComp, index);

            if (accessoryComp.PlayerSprite is { } specified)
            {
                if (clothingSprite != null && accessoryComp.HasIconSprite)
                {
                    var li = clothingSprite.LayerMapReserveBlank(layerKey);
                    clothingSprite.LayerSetVisible(li, !accessoryComp.Hidden);
                    clothingSprite.LayerSetRSI(li, specified.RsiPath);
                    clothingSprite.LayerSetState(li, specified.RsiState);
                }

                if (args.Layers.All(t => t.Item1 != layerKey))
                {
                    args.Layers.Add((layerKey, new PrototypeLayerData
                    {
                        RsiPath = specified.RsiPath.ToString(),
                        State = specified.RsiState,
                        Visible = !accessoryComp.Hidden,
                    }));
                }

                index++;
                continue;
            }

            var accessorySlot = GetAccessorySlot(accessory) ?? args.Slot;
            var accessoryEv = new GetEquipmentVisualsEvent(args.Equipee, accessorySlot);

            ForceAccessoryRSI(accessory, accessoryEv, layerKey);

            RaiseLocalEvent(accessory, accessoryEv);

            if (accessoryEv.Layers.Count > 0)
            {
                var layerData = accessoryEv.Layers[0].Item2;

                if (clothingSprite != null && accessoryComp.HasIconSprite)
                {
                    var li = clothingSprite.LayerMapReserveBlank(layerKey);
                    clothingSprite.LayerSetVisible(li, !accessoryComp.Hidden);

                    if (layerData.RsiPath != null)
                        clothingSprite.LayerSetRSI(li, layerData.RsiPath);
                    if (layerData.State != null)
                        clothingSprite.LayerSetState(li, layerData.State);
                }

                if (args.Layers.All(t => t.Item1 != layerKey))
                {
                    args.Layers.Add((layerKey, new PrototypeLayerData
                    {
                        RsiPath = layerData.RsiPath,
                        State = layerData.State,
                        TexturePath = layerData.TexturePath,
                        Color = layerData.Color,
                        Scale = layerData.Scale,
                        Visible = !accessoryComp.Hidden && (layerData.Visible ?? true),
                    }));
                }
            }

            index++;
        }

        PlayerAccessoryVisualsUpdated?.Invoke();
    }

    private string? GetAccessorySlot(EntityUid uid)
    {
        if (TryComp<ClothingComponent>(uid, out var clothing))
        {
            if (!string.IsNullOrEmpty(clothing.InSlot))
                return clothing.InSlot;

            foreach (SlotFlags f in Enum.GetValues(typeof(SlotFlags)))
            {
                if (f == SlotFlags.NONE)
                    continue;
                if ((clothing.Slots & f) != 0)
                    return f.ToString().ToLowerInvariant();
            }
        }

        if (TryComp<Shared.DeadSpace.UniformAccessories.UniformAccessoryComponent>(uid, out var accComp) &&
            !string.IsNullOrEmpty(accComp.Category))
            return accComp.Category.ToLowerInvariant();

        return null;
    }

    private void ForceAccessoryRSI(EntityUid accessory, GetEquipmentVisualsEvent ev, string layerKey)
    {
        var (rsiPath, state) = GetAccessorySpriteInfo(accessory);
        if (string.IsNullOrEmpty(rsiPath))
            return;

        ev.Layers.Clear();

        var layerData = new PrototypeLayerData
        {
            RsiPath = rsiPath,
            Visible = true,
        };

        var clothingVisualsEv = new GetEquipmentVisualsEvent(ev.Equipee, ev.Slot);
        RaiseLocalEvent(accessory, clothingVisualsEv);

        if (clothingVisualsEv.Layers.Count > 0)
            layerData.State = clothingVisualsEv.Layers[0].Item2.State;
        else if (TryComp<ClothingComponent>(accessory, out var clothing))
        {
            if (!string.IsNullOrEmpty(clothing.EquippedState))
                layerData.State = clothing.EquippedState;
            else if (!string.IsNullOrEmpty(clothing.EquippedPrefix))
            {
                var slotSuffix = ev.Slot.ToUpperInvariant();
                layerData.State = $"{clothing.EquippedPrefix}-equipped-{slotSuffix}";
            }
            else
                layerData.State = $"equipped-{ev.Slot.ToUpperInvariant()}";
        }
        else
            layerData.State = $"equipped-{ev.Slot.ToUpperInvariant()}";

        ev.Layers.Add((layerKey, layerData));
    }

    private (string? RsiPath, string? State) GetAccessorySpriteInfo(EntityUid uid)
    {
        if (TryComp<ClothingComponent>(uid, out var clothing) && !string.IsNullOrEmpty(clothing.RsiPath))
            return (clothing.RsiPath, clothing.EquippedState);

        if (TryComp<SpriteComponent>(uid, out var sprite) && sprite.BaseRSI != null)
            return (sprite.BaseRSI.Path.ToString(), null);

        return (null, null);
    }

    private void OnHolderAfterState(Entity<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnHolderInsertedContainer(Entity<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent> ent,
        ref EntInsertedIntoContainerMessage args)
    {
        _item.VisualsChanged(ent);

        if (TryComp<Shared.DeadSpace.UniformAccessories.UniformAccessoryComponent>(args.Entity, out var acc) && acc.DrawOnItemIcon)
            UpdateItemIconOverlay(ent.Owner, args.Entity, true);
    }

    private void OnHolderRemovedContainer(Entity<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent> ent,
        ref EntRemovedFromContainerMessage args)
    {
        var item = args.Entity;

        if (!TryComp<Shared.DeadSpace.UniformAccessories.UniformAccessoryComponent>(item, out var accessoryComp))
            return;

        var index = 0;
        foreach (var accessory in args.Container.ContainedEntities)
        {
            if (accessory == item)
                break;
            index++;
        }

        var layerKey = GetLayerKey(item, accessoryComp, index);
        if (TryComp(ent.Owner, out SpriteComponent? clothingSprite) &&
            clothingSprite.LayerMapTryGet(layerKey, out var clothingLayer))
            clothingSprite.LayerSetVisible(clothingLayer, false);

        _item.VisualsChanged(ent);

        if (accessoryComp.DrawOnItemIcon)
            UpdateItemIconOverlay(ent.Owner, args.Entity, false);
    }

    private void UpdateItemIconOverlay(EntityUid holder, EntityUid accessory, bool add)
    {
        if (!TryComp<SpriteComponent>(holder, out var itemSprite))
            return;

        var key = $"AccessoryIcon_{accessory}";

        if (!TryComp<Shared.DeadSpace.UniformAccessories.UniformAccessoryComponent>(accessory, out var acc) || !acc.DrawOnItemIcon)
            return;

        if (add)
        {
            if (!itemSprite.LayerMapTryGet(key, out var layer))
                layer = itemSprite.LayerMapReserveBlank(key);

            if (acc.PlayerSprite is { } specified)
            {
                itemSprite.LayerSetRSI(layer, specified.RsiPath);
                itemSprite.LayerSetState(layer, specified.RsiState);
                itemSprite.LayerSetVisible(layer, !acc.Hidden);
            }
            else
            {
                var accessorySlot = GetAccessorySlot(accessory) ?? "outerClothing";
                var ev = new GetEquipmentVisualsEvent(holder, accessorySlot);
                ForceAccessoryRSI(accessory, ev, key);

                if (ev.Layers.Count > 0)
                {
                    var layerData = ev.Layers[0].Item2;

                    try
                    {
                        if (layerData.RsiPath != null)
                            itemSprite.LayerSetRSI(layer, layerData.RsiPath);
                        if (layerData.State != null)
                            itemSprite.LayerSetState(layer, layerData.State);
                        itemSprite.LayerSetVisible(layer, !acc.Hidden);
                    }
                    catch
                    {
                        itemSprite.LayerSetVisible(layer, false);
                    }
                }
            }
        }
        else
        {
            if (itemSprite.LayerMapTryGet(key, out var li))
                itemSprite.RemoveLayer(li);
        }
    }

    private void OnHolderVisualsUpdated(Entity<Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent> ent,
        ref EquipmentVisualsUpdatedEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;

        if (!TryComp(args.Equipee, out SpriteComponent? sprite))
            return;

        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<Shared.DeadSpace.UniformAccessories.UniformAccessoryComponent>(accessory, out var acc))
                continue;

            var key = acc.LayerKey;
            if (string.IsNullOrEmpty(key))
                continue;

            if (!args.RevealedLayers.Contains(key))
                continue;

            if (!sprite.LayerMapTryGet(key, out var layer) ||
                !sprite.TryGetLayer(layer, out var layerData))
                continue;

            var data = layerData.ToPrototypeData();
            sprite.RemoveLayer(layer);

            layer = sprite.LayerMapReserveBlank(key);
            sprite.LayerSetData(layer, data);
        }
    }

    private bool ShouldHideAccessories(HumanoidAppearanceComponent humanoid)
    {
        return false;
    }

    private string GetLayerKey(EntityUid uid, Shared.DeadSpace.UniformAccessories.UniformAccessoryComponent component, int index)
    {
        if (!string.IsNullOrEmpty(component.LayerKey))
            return component.LayerKey!;

        return $"Accessory_{uid.Id}_{index}";
    }
}
