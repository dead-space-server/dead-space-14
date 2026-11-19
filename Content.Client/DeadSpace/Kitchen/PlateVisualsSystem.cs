// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Items.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Kitchen.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Client.DeadSpace.Kitchen;

public sealed partial class PlateVisualsSystem : EntitySystem
{
    private const string WorldLayerPrefix = "plate-content-";

    [Dependency] private ItemSystem _itemSystem = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, HashSet<string>> _worldLayerKeys = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlateComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PlateComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PlateComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<PlateComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<PlateComponent, VisualsChangedEvent>(OnVisualsChanged);
    }

    private void OnStartup(Entity<PlateComponent> ent, ref ComponentStartup args)
    {
        UpdateWorldVisuals(ent);
    }

    private void OnShutdown(Entity<PlateComponent> ent, ref ComponentShutdown args)
    {
        ClearWorldLayers(ent.Owner);
        _worldLayerKeys.Remove(ent.Owner);
    }

    private void OnEntInserted(Entity<PlateComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId)
            return;

        UpdateWorldVisuals(ent);
    }

    private void OnEntRemoved(Entity<PlateComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId)
            return;

        UpdateWorldVisuals(ent);
    }

    private void OnVisualsChanged(Entity<PlateComponent> ent, ref VisualsChangedEvent args)
    {
        if (args.ContainerId != ent.Comp.SlotId)
            return;

        UpdateWorldVisuals(ent);
    }

    private void UpdateWorldVisuals(Entity<PlateComponent> ent)
    {
        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        ClearWorldLayers(ent.Owner, sprite);

        var keys = new HashSet<string>();
        if (TryGetContentSprite(ent.Owner, ent.Comp, out var contentSprite))
        {
            AddClonedLayers(ent.Owner,
                sprite,
                contentSprite,
                ent.Comp.ContentOffset,
                ent.Comp.ContentScale,
                WorldLayerPrefix,
                keys);
        }

        if (keys.Count > 0)
            _worldLayerKeys[ent.Owner] = keys;

        _itemSystem.VisualsChanged(ent.Owner);
    }

    private void ClearWorldLayers(EntityUid uid, SpriteComponent? sprite = null)
    {
        if (!_worldLayerKeys.Remove(uid, out var keys))
            return;

        if (!Resolve(uid, ref sprite, false))
            return;

        foreach (var key in keys)
        {
            _sprite.RemoveLayer((uid, sprite), key, false);
        }
    }

    private bool TryGetContentSprite(EntityUid plate, PlateComponent component, out SpriteComponent contentSprite)
    {
        var content = _itemSlots.GetItemOrNull(plate, component.SlotId);

        if (content != null &&
            TryComp(content.Value, out SpriteComponent? sprite) &&
            sprite != null)
        {
            contentSprite = sprite;
            return true;
        }

        contentSprite = default!;
        return false;
    }

    private void AddClonedLayers(EntityUid targetUid,
        SpriteComponent targetSprite,
        SpriteComponent sourceSprite,
        Vector2 offset,
        Vector2 scale,
        string keyPrefix,
        ISet<string> keySink)
    {
        var layerIndex = 0;
        var sourceEntity = sourceSprite.Owner;
        Entity<SpriteComponent?> target = (targetUid, targetSprite);

        foreach (var i in Enumerable.Range(0, sourceSprite.AllLayers.Count()))
        {
            if (!_sprite.TryGetLayer((sourceEntity, sourceSprite), i, out var sourceLayer, false) ||
                !sourceLayer.Visible ||
                sourceLayer.Blank ||
                sourceLayer.CopyToShaderParameters != null)
            {
                continue;
            }

            var clone = new SpriteComponent.Layer(sourceLayer, targetSprite);

            var key = $"{keyPrefix}{layerIndex}";
            var index = _sprite.AddLayer(target, clone);
            _sprite.LayerMapSet(target, key, index);

            if (clone.RSI == null && sourceLayer.ActualRsi != null)
            {
                _sprite.LayerSetRsi(clone, sourceLayer.ActualRsi, sourceLayer.State);
                _sprite.LayerSetAnimationTime(clone, sourceLayer.AnimationTime);
            }

            _sprite.LayerSetOffset(clone, clone.Offset + offset);
            _sprite.LayerSetScale(clone, clone.Scale * scale);
            keySink.Add(key);
            layerIndex++;
        }
    }
}
