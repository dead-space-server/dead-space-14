// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Clothing;
using Content.Client.DeadSpace.Clothing;
using Content.Client.DeadSpace.Ninja.Components;
using Content.Client.Inventory;
using Content.Client.Strip;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaDisguiseClientSystem : SharedNinjaDisguiseSystem
{
    private const float IntegrityCheckInterval = 0.5f;

    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly ClientClothingSystem _clothing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private readonly HashSet<EntityUid> _disguisedWearers = new();
    private readonly Dictionary<EntityUid, HashSet<string>> _disguiseHiddenSlots = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _wearerProxies = new();
    private readonly Dictionary<(EntityUid Wearer, string Slot), (EntityUid Proxy, Vector2 SlotOffset)> _disguiseProxies = new();
    private readonly Dictionary<(EntityUid Wearer, string Slot), HashSet<string>> _disguiseSlotKeys = new();
    private readonly Dictionary<EntityUid, HashSet<string>> _activeDisguiseKeys = new();
    private readonly HashSet<EntityUid> _activeProxies = new();
    private readonly HashSet<EntityUid> _busyWearers = new();
    private readonly List<EntityUid> _wearerScratch = new();
    private readonly List<(EntityUid Wearer, string Slot)> _slotScratch = new();

    private float _integrityAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _integrityAccumulator += frameTime;
        if (_integrityAccumulator < IntegrityCheckInterval)
            return;

        _integrityAccumulator %= IntegrityCheckInterval;

        if (_disguisedWearers.Count == 0)
            return;

        _wearerScratch.Clear();
        _wearerScratch.AddRange(_disguisedWearers);

        foreach (var wearer in _wearerScratch)
        {
            SyncDisguisedState(wearer);

            if (!_disguisedWearers.Contains(wearer))
                continue;

            SyncMarkedItems(wearer);
            ReassertDisguise(wearer);
        }

        _wearerScratch.Clear();
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaDisguiseComponent, AfterAutoHandleStateEvent>(OnSuitState);
        SubscribeLocalEvent<NinjaDisguiseComponent, ComponentShutdown>(OnSuitShutdown);
        SubscribeLocalEvent<ItemComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: new[] { typeof(ClientClothingSystem) });

        SubscribeLocalEvent<NinjaDisguiseWearerComponent, AppearanceChangeEvent>(OnAppearanceChanged,
            after: new[] { typeof(ClientClothingSystem) });

        SubscribeLocalEvent<NinjaDisguiseItemComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated,
            after: new[] { typeof(ClientHideLayerClothingSystem) });

        SubscribeLocalEvent<NinjaDisguiseWearerComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<NinjaDisguiseWearerComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnItemInserted(Entity<NinjaDisguiseWearerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        var wearer = ent.Owner;
        if (!IsDisguised(wearer))
            return;

        if (args.Entity.IsValid() && Exists(args.Entity) && !HasComp<NinjaDisguiseItemComponent>(args.Entity))
            AddComp<NinjaDisguiseItemComponent>(args.Entity);

        ReassertDisguise(wearer);
    }

    private void OnItemRemoved(Entity<NinjaDisguiseWearerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Entity.IsValid() && Exists(args.Entity) && HasComp<NinjaDisguiseItemComponent>(args.Entity))
            RemComp<NinjaDisguiseItemComponent>(args.Entity);
    }

    private void OnAppearanceChanged(Entity<NinjaDisguiseWearerComponent> ent, ref AppearanceChangeEvent args)
    {
        var wearer = ent.Owner;
        SyncDisguisedState(wearer);

        if (IsDisguised(wearer))
            ReassertDisguise(wearer);
    }

    private void OnEquipmentVisualsUpdated(Entity<NinjaDisguiseItemComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (_disguisedWearers.Contains(args.Equipee))
            ReassertDisguise(args.Equipee);
    }

    private void SyncDisguisedState(EntityUid wearer)
    {
        if (_busyWearers.Contains(wearer))
            return;

        if (TryGetActiveDisguise(wearer, out _, out var comp))
        {
            _disguisedWearers.Add(wearer);

            if (!_wearerProxies.ContainsKey(wearer) && TryGetActiveEntry(comp, out var entry))
            {
                EnsureWearerMarked(wearer);
                ApplyClothing(wearer, entry);
            }

            return;
        }

        if (!_disguisedWearers.Remove(wearer))
            return;

        RestoreClothing(wearer);
    }

    private void ReassertDisguise(EntityUid wearer)
    {
        if (!_busyWearers.Add(wearer))
            return;

        try
        {
            foreach (var ((proxyWearer, slot), _) in _disguiseProxies)
            {
                if (proxyWearer == wearer)
                    RepairSlotDisguise(wearer, slot);
            }

            ApplyDisguiseLayerVisibility(wearer);
        }
        finally
        {
            _busyWearers.Remove(wearer);
        }
    }

    private void RepairSlotDisguise(EntityUid wearer, string slot)
    {
        if (IsHiddenSlot(wearer, slot))
            return;

        if (!_disguiseProxies.TryGetValue((wearer, slot), out var entry) || !Exists(entry.Proxy))
            return;

        if (SlotLayersIntact(wearer, slot))
            return;

        _clothing.RenderEquipment(wearer, entry.Proxy, slot, slotOffset: entry.SlotOffset);
        SyncSlotKeys(wearer, slot);
    }

    private bool SlotLayersIntact(EntityUid wearer, string slot)
    {
        if (!TryComp<InventorySlotsComponent>(wearer, out var slots) ||
            !TryComp<SpriteComponent>(wearer, out var sprite))
        {
            return true;
        }

        if (!slots.VisualLayerKeys.TryGetValue(slot, out var revealed))
            return true;

        if (!_disguiseSlotKeys.TryGetValue((wearer, slot), out var expected) || expected.Count == 0)
            return false;

        if (expected.Count != revealed.Count)
            return false;

        foreach (var key in revealed)
        {
            if (!expected.Contains(key))
                return false;

            if (!_sprite.LayerMapTryGet((wearer, sprite), key, out _, false))
                return false;
        }

        return true;
    }

    private void SyncSlotKeys(EntityUid wearer, string slot)
    {
        if (!TryComp<InventorySlotsComponent>(wearer, out var slots) ||
            !slots.VisualLayerKeys.TryGetValue(slot, out var revealed))
        {
            return;
        }

        if (!_disguiseSlotKeys.TryGetValue((wearer, slot), out var tracked) ||
            tracked.Count != revealed.Count)
        {
            _disguiseSlotKeys[(wearer, slot)] = tracked = new HashSet<string>(revealed);
        }
        else
        {
            tracked.Clear();
            tracked.UnionWith(revealed);
        }

        var keys = ActiveKeys(wearer);
        foreach (var key in tracked)
            keys.Add(key);
    }

    private void OnSuitState(Entity<NinjaDisguiseComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryGetWearer(ent, out var wearer) && TryGetActiveEntry(ent.Comp, out var entry))
        {
            _disguisedWearers.Add(wearer);
            EnsureWearerMarked(wearer);
            ApplyClothing(wearer, entry);
            DirtyStripUi(wearer);
            return;
        }

        ClearDisguise(ent);
    }

    private void OnSuitShutdown(Entity<NinjaDisguiseComponent> ent, ref ComponentShutdown args)
    {
        ClearDisguise(ent);
    }

    private void ClearDisguise(Entity<NinjaDisguiseComponent> ent)
    {
        if (!TryGetWearer(ent, out var wearer))
            return;

        _disguisedWearers.Remove(wearer);
        RestoreClothing(wearer);
        DirtyStripUi(wearer);
    }

    private bool TryGetWearer(Entity<NinjaDisguiseComponent> ent, out EntityUid wearer)
    {
        wearer = Transform(ent.Owner).ParentUid;
        return wearer.IsValid() && Exists(wearer);
    }

    private static bool TryGetActiveEntry(NinjaDisguiseComponent comp, out NinjaDisguiseEntry entry)
    {
        if (comp.Disguised && comp.ActiveIndex is { } index && index >= 0 && index < comp.Entries.Count)
        {
            entry = comp.Entries[index];
            return true;
        }

        entry = default!;
        return false;
    }

    private void DirtyStripUi(EntityUid wearer)
    {
        EntityManager.System<StrippableSystem>().UpdateUi(wearer);
    }

    private void EnsureWearerMarked(EntityUid wearer)
    {
        if (!HasComp<NinjaDisguiseWearerComponent>(wearer))
            AddComp<NinjaDisguiseWearerComponent>(wearer);
    }

    private void RemoveWearerMark(EntityUid wearer)
    {
        if (HasComp<NinjaDisguiseWearerComponent>(wearer))
            RemComp<NinjaDisguiseWearerComponent>(wearer);
    }

    private void SyncMarkedItems(EntityUid wearer)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return;

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item, out _))
        {
            if (item.IsValid() && Exists(item) && !HasComp<NinjaDisguiseItemComponent>(item))
                AddComp<NinjaDisguiseItemComponent>(item);
        }
    }

    private void UnmarkItems(EntityUid wearer)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return;

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item, out _))
        {
            if (item.IsValid() && HasComp<NinjaDisguiseItemComponent>(item))
                RemComp<NinjaDisguiseItemComponent>(item);
        }
    }

    public bool IsDisguised(EntityUid wearer)
    {
        return _disguisedWearers.Contains(wearer);
    }

    public bool TryGetDisguiseSlotProxy(EntityUid wearer, string slot, out EntityUid proxy)
    {
        proxy = default;
        if (!_disguiseProxies.TryGetValue((wearer, slot), out var entry) || !Exists(entry.Proxy))
            return false;

        proxy = entry.Proxy;
        return true;
    }

    public bool TryGetDisguiseSlotProto(EntityUid wearer, string slot, out EntProtoId protoId)
    {
        protoId = default;
        if (!TryGetActiveDisguise(wearer, out _, out var comp) ||
            !TryGetActiveEntry(comp, out var entry))
        {
            return false;
        }

        foreach (var item in entry.Inventory)
        {
            if (item.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase) && item.ItemId is { } itemId)
            {
                protoId = itemId;
                return true;
            }
        }

        return false;
    }

    private void ApplyClothing(EntityUid wearer, NinjaDisguiseEntry entry)
    {
        if (!_busyWearers.Add(wearer))
            return;

        try
        {
            if (!TryComp<SpriteComponent>(wearer, out var sprite))
                return;

            ClearEquipmentLayers(wearer, sprite);
            _activeDisguiseKeys.Remove(wearer);
            ClearSlotKeys(wearer);
            DeleteProxies(wearer);

            SetHiddenSlots(wearer, entry.HiddenClothingSlots);
            SyncMarkedItems(wearer);

            foreach (var item in entry.Inventory)
            {
                if (!_proto.HasIndex<EntityPrototype>(item.ItemId))
                    continue;

                if (IsHiddenSlot(wearer, item.Slot))
                    continue;

                var proxy = Spawn(item.ItemId.Id, MapCoordinates.Nullspace);
                if (!HasComp<ClothingComponent>(proxy))
                {
                    QueueDel(proxy);
                    continue;
                }

                AddComp<NinjaDisguiseItemComponent>(proxy);
                AddProxy(wearer, proxy);
                _disguiseProxies[(wearer, item.Slot)] = (proxy, item.SlotOffset);

                _clothing.RenderEquipment(wearer, proxy, item.Slot, slotOffset: item.SlotOffset);

                SyncSlotKeys(wearer, item.Slot);
            }

            ApplyDisguiseLayerVisibility(wearer);
        }
        finally
        {
            _busyWearers.Remove(wearer);
        }
    }

    private void ApplyDisguiseLayerVisibility(EntityUid wearer)
    {
        if (!TryComp<InventorySlotsComponent>(wearer, out var slots) ||
            !TryComp<SpriteComponent>(wearer, out var sprite) ||
            !_activeDisguiseKeys.TryGetValue(wearer, out var activeKeys))
        {
            return;
        }

        foreach (var (slot, layerKeys) in slots.VisualLayerKeys)
        {
            var visible = !IsHiddenSlot(wearer, slot);
            foreach (var key in layerKeys)
            {
                if (!activeKeys.Contains(key))
                    continue;

                if (_sprite.LayerMapTryGet((wearer, sprite), key, out var layer, false))
                    _sprite.LayerSetVisible((wearer, sprite), layer, visible);
            }
        }
    }

    private bool IsHiddenSlot(EntityUid wearer, string slot)
    {
        return _disguiseHiddenSlots.TryGetValue(wearer, out var hidden) && ContainsSlot(hidden, slot);
    }

    private void SetHiddenSlots(EntityUid wearer, IEnumerable<string> slots)
    {
        var hidden = new HashSet<string>();
        foreach (var slot in slots)
            hidden.Add(slot);

        if (hidden.Count == 0)
        {
            _disguiseHiddenSlots.Remove(wearer);
            return;
        }

        _disguiseHiddenSlots[wearer] = hidden;
    }

    private static bool ContainsSlot(IEnumerable<string> slots, string slot)
    {
        foreach (var candidate in slots)
        {
            if (candidate.Equals(slot, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void RestoreClothing(EntityUid wearer)
    {
        if (TryComp<SpriteComponent>(wearer, out var sprite))
        {
            RemoveActiveDisguiseLayers(wearer, sprite);
        }

        _disguiseHiddenSlots.Remove(wearer);
        ClearSlotKeys(wearer);
        DeleteProxies(wearer);
        UnmarkItems(wearer);
        RemoveWearerMark(wearer);

        if (TryComp<InventoryComponent>(wearer, out var inventory))
            _clothing.InitClothing(wearer, inventory);
    }

    private void RemoveActiveDisguiseLayers(EntityUid wearer, SpriteComponent sprite)
    {
        if (!_activeDisguiseKeys.Remove(wearer, out var keys))
            return;

        foreach (var key in keys)
            _sprite.RemoveLayer((wearer, sprite), key, logMissing: false);

        if (TryComp(wearer, out InventorySlotsComponent? slots))
        {
            foreach (var revealed in slots.VisualLayerKeys.Values)
                revealed.RemoveWhere(key => keys.Contains(key));
        }
    }

    private void AddProxy(EntityUid wearer, EntityUid proxy)
    {
        if (!_wearerProxies.TryGetValue(wearer, out var owned))
            _wearerProxies[wearer] = owned = new HashSet<EntityUid>();

        owned.Add(proxy);
        _activeProxies.Add(proxy);
    }

    private void DeleteProxies(EntityUid wearer)
    {
        if (_wearerProxies.Remove(wearer, out var owned))
        {
            foreach (var proxy in owned)
            {
                _activeProxies.Remove(proxy);

                if (Exists(proxy))
                    Del(proxy);
            }
        }

        _slotScratch.Clear();
        foreach (var (key, _) in _disguiseProxies)
        {
            if (key.Wearer == wearer)
                _slotScratch.Add(key);
        }

        foreach (var key in _slotScratch)
            _disguiseProxies.Remove(key);

        _slotScratch.Clear();
    }

    private HashSet<string> ActiveKeys(EntityUid wearer)
    {
        if (!_activeDisguiseKeys.TryGetValue(wearer, out var keys))
            _activeDisguiseKeys[wearer] = keys = new HashSet<string>();

        return keys;
    }

    private void ClearSlotKeys(EntityUid wearer)
    {
        _slotScratch.Clear();
        foreach (var (key, _) in _disguiseSlotKeys)
        {
            if (key.Wearer == wearer)
                _slotScratch.Add(key);
        }

        foreach (var key in _slotScratch)
            _disguiseSlotKeys.Remove(key);

        _slotScratch.Clear();
    }

    private void ClearEquipmentLayers(EntityUid wearer, SpriteComponent sprite)
    {
        if (!TryComp<InventorySlotsComponent>(wearer, out var slots))
            return;

        foreach (var revealed in slots.VisualLayerKeys.Values)
        {
            foreach (var key in revealed)
                _sprite.RemoveLayer((wearer, sprite), key, logMissing: false);

            revealed.Clear();
        }
    }

    private void OnGetEquipmentVisuals(Entity<ItemComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (_activeProxies.Contains(ent.Owner))
            return;

        if (!_disguisedWearers.Contains(args.Equipee))
            return;

        args.Layers.Clear();

        RestoreSlotDisguise(args.Equipee, args.Slot);
    }

    private void RestoreSlotDisguise(EntityUid wearer, string slot)
    {
        if (!_disguiseProxies.TryGetValue((wearer, slot), out var entry) ||
            !Exists(entry.Proxy) ||
            !TryComp<SpriteComponent>(wearer, out _))
        {
            return;
        }

        _clothing.RenderEquipment(wearer, entry.Proxy, slot, slotOffset: entry.SlotOffset);
        SyncSlotKeys(wearer, slot);
    }
}
