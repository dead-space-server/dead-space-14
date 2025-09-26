using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DeadSpace.Ports.UniformAccessories;
using Content.Shared.DeadSpace.Ports.UniformAccessories.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Containers;

namespace Content.Client.DeadSpace.Ports.UniformAccessories;

public sealed class UniformAccessorySystem : SharedUniformAccessorySystem
{
    private const string ContainerId = "rmc_uniform_accessories";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UniformAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnHolderGetEquipmentVisuals,
            after: [typeof(ClothingSystem)]);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, AfterAutoHandleStateEvent>(OnHolderAfterState);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntInsertedIntoContainerMessage>(
            OnHolderInsertedContainer);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnHolderRemovedContainer);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, EquipmentVisualsUpdatedEvent>(OnHolderVisualsUpdated,
            after: [typeof(ClothingSystem)]);
    }

    private void OnHolderGetEquipmentVisuals(Entity<UniformAccessoryHolderComponent> holder,
        ref GetEquipmentVisualsEvent eventArgs)
    {
        if (!_container.TryGetContainer(holder, ContainerId, out var container))
            return;

        var clothingSprite = CompOrNull<SpriteComponent>(holder);
        var layerIndex = 0;

        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<UniformAccessoryComponent>(accessory, out var accessoryComp))
                continue;

            var layerKey = GetLayerKey(accessory, accessoryComp, layerIndex);
            var layerData = GetLayerData(accessory, accessoryComp, eventArgs.Slot);

            if (layerData == null)
            {
                layerIndex++;
                continue;
            }

            ApplyLayerData(clothingSprite, eventArgs, layerKey, layerData, accessoryComp);
            layerIndex++;
        }
    }

    private void ApplyLayerData(SpriteComponent? clothingSprite,
        GetEquipmentVisualsEvent eventArgs,
        string layerKey,
        PrototypeLayerData layerData,
        UniformAccessoryComponent accessoryComp)
    {
        if (clothingSprite != null && accessoryComp.HasIconSprite)
        {
            var layerIndex = clothingSprite.LayerMapReserveBlank(layerKey);
            clothingSprite.LayerSetVisible(layerIndex, !accessoryComp.Hidden);
            if (layerData.RsiPath != null)
                clothingSprite.LayerSetRSI(layerIndex, layerData.RsiPath);
            if (layerData.State != null)
                clothingSprite.LayerSetState(layerIndex, layerData.State);
        }

        if (!eventArgs.Layers.Any(t => t.Item1 == layerKey))
        {
            eventArgs.Layers.Add((layerKey, new PrototypeLayerData
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

    private PrototypeLayerData? GetLayerData(EntityUid accessory, UniformAccessoryComponent accessoryComp, string slot)
    {
        if (accessoryComp.PlayerSprite is { } specified)
        {
            return new PrototypeLayerData
            {
                RsiPath = specified.RsiPath.ToString(),
                State = specified.RsiState,
                Visible = !accessoryComp.Hidden,
            };
        }

        var accessorySlot = GetAccessorySlot(accessory) ?? slot;
        var accessoryEv = new GetEquipmentVisualsEvent(accessory, accessorySlot);
        ForceAccessoryRSI(accessory, accessoryEv);
        return accessoryEv.Layers.Count > 0 ? accessoryEv.Layers[0].Item2 : null;
    }

    private void ForceAccessoryRSI(EntityUid accessory, GetEquipmentVisualsEvent eventArgs)
    {
        var (rsiPath, state) = GetAccessorySpriteInfo(accessory);
        if (string.IsNullOrEmpty(rsiPath))
            return;

        eventArgs.Layers.Clear();
        var layerData = new PrototypeLayerData { RsiPath = rsiPath, Visible = true };

        if (TryComp<ClothingComponent>(accessory, out var clothing))
        {
            layerData.State = !string.IsNullOrEmpty(clothing.EquippedState)
                ? clothing.EquippedState
                : !string.IsNullOrEmpty(clothing.EquippedPrefix)
                    ? $"{clothing.EquippedPrefix}-equipped-{eventArgs.Slot.ToUpperInvariant()}"
                    : $"equipped-{eventArgs.Slot.ToUpperInvariant()}";
        }
        else
        {
            var clothingVisualsEv = new GetEquipmentVisualsEvent(eventArgs.Equipee, eventArgs.Slot);
            RaiseLocalEvent(accessory, clothingVisualsEv);
            layerData.State = clothingVisualsEv.Layers.Count > 0
                ? clothingVisualsEv.Layers[0].Item2.State
                : $"equipped-{eventArgs.Slot.ToUpperInvariant()}";
        }

        eventArgs.Layers.Add(($"Accessory_{accessory.Id}", layerData));
    }

    private (string? RsiPath, string? State) GetAccessorySpriteInfo(EntityUid uid)
    {
        if (TryComp<ClothingComponent>(uid, out var clothing) && !string.IsNullOrEmpty(clothing.RsiPath))
            return (clothing.RsiPath, clothing.EquippedState);
        if (TryComp<SpriteComponent>(uid, out var sprite) && sprite.BaseRSI != null)
            return (sprite.BaseRSI.Path.ToString(), null);
        return (null, null);
    }

    private string? GetAccessorySlot(EntityUid uid)
    {
        if (TryComp<ClothingComponent>(uid, out var clothing))
        {
            if (!string.IsNullOrEmpty(clothing.InSlot))
                return clothing.InSlot;
            foreach (SlotFlags slot in Enum.GetValues(typeof(SlotFlags)))
            {
                if (slot == SlotFlags.NONE)
                    continue;
                if ((clothing.Slots & slot) != 0)
                    return slot.ToString().ToLowerInvariant();
            }
        }

        if (TryComp<UniformAccessoryComponent>(uid, out var accComp) && !string.IsNullOrEmpty(accComp.Category))
            return accComp.Category.ToLowerInvariant();
        return null;
    }

    private void OnHolderAfterState(Entity<UniformAccessoryHolderComponent> holder,
        ref AfterAutoHandleStateEvent eventArgs)
    {
        _item.VisualsChanged(holder);
    }

    private void OnHolderInsertedContainer(Entity<UniformAccessoryHolderComponent> holder,
        ref EntInsertedIntoContainerMessage eventArgs)
    {
        _item.VisualsChanged(holder);
        if (TryComp<UniformAccessoryComponent>(eventArgs.Entity, out var acc) && acc.DrawOnItemIcon)
            UpdateItemIconOverlay(holder.Owner, eventArgs.Entity, true);
    }

    private void OnHolderRemovedContainer(Entity<UniformAccessoryHolderComponent> holder,
        ref EntRemovedFromContainerMessage eventArgs)
    {
        var item = eventArgs.Entity;
        if (!TryComp<UniformAccessoryComponent>(item, out var accessoryComp))
            return;

        var layerIndex = 0;
        foreach (var accessory in eventArgs.Container.ContainedEntities)
        {
            if (accessory == item)
                break;
            layerIndex++;
        }

        var layerKey = GetLayerKey(item, accessoryComp, layerIndex);
        if (TryComp(holder.Owner, out SpriteComponent? clothingSprite) &&
            clothingSprite.LayerMapTryGet(layerKey, out var clothingLayer))
            clothingSprite.LayerSetVisible(clothingLayer, false);

        _item.VisualsChanged(holder);
        if (accessoryComp.DrawOnItemIcon)
            UpdateItemIconOverlay(holder.Owner, item, false);
    }

    private void UpdateItemIconOverlay(EntityUid holder, EntityUid accessory, bool add)
    {
        if (!TryComp<SpriteComponent>(holder, out var itemSprite))
            return;

        var key = $"AccessoryIcon_{accessory}";
        if (!TryComp<UniformAccessoryComponent>(accessory, out var acc) || !acc.DrawOnItemIcon)
            return;

        if (add)
        {
            var layer = itemSprite.LayerMapReserveBlank(key);
            if (acc.PlayerSprite is { } specified)
            {
                itemSprite.LayerSetRSI(layer, specified.RsiPath.ToString());
                itemSprite.LayerSetState(layer, specified.RsiState);
                itemSprite.LayerSetVisible(layer, !acc.Hidden);
            }
            else
            {
                var accessorySlot = GetAccessorySlot(accessory) ?? "outerClothing";
                var ev = new GetEquipmentVisualsEvent(holder, accessorySlot);
                ForceAccessoryRSI(accessory, ev);
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
            if (itemSprite.LayerMapTryGet(key, out var layer))
                itemSprite.RemoveLayer(layer);
        }
    }

    private void OnHolderVisualsUpdated(Entity<UniformAccessoryHolderComponent> holder,
        ref EquipmentVisualsUpdatedEvent eventArgs)
    {
        if (!_container.TryGetContainer(holder, ContainerId, out var container))
            return;

        if (!TryComp(eventArgs.Equipee, out SpriteComponent? sprite))
            return;

        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<UniformAccessoryComponent>(accessory, out var acc) || string.IsNullOrEmpty(acc.LayerKey))
                continue;

            if (!eventArgs.RevealedLayers.Contains(acc.LayerKey))
                continue;

            if (!sprite.LayerMapTryGet(acc.LayerKey, out var layer) ||
                !sprite.TryGetLayer(layer, out var layerData))
                continue;

            var data = layerData.ToPrototypeData();
            sprite.RemoveLayer(layer);
            layer = sprite.LayerMapReserveBlank(acc.LayerKey);
            sprite.LayerSetData(layer, data);
        }
    }

    private string GetLayerKey(EntityUid uid, UniformAccessoryComponent component, int layerIndex)
    {
        return !string.IsNullOrEmpty(component.LayerKey) ? component.LayerKey : $"Accessory_{uid.Id}_{layerIndex}";
    }
}
