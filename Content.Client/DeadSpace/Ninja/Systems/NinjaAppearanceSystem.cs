using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Client.PDA;
using Content.Client.Toggleable;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Containers;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed partial class NinjaAppearanceSystem : SharedNinjaAppearanceSystem
{
    private static readonly string[] ColorPrefixes = { "red-", "blue-", "green-" };

    private const string StylePrefix = "new-";

    private const string DeactivatedIconState = "deactivated-icon";

    private readonly Dictionary<EntityUid, string?[]> _savedIconStates = new();

    private readonly Dictionary<EntityUid, (string Prefix, NinjaStyle Style, bool Visible)> _applied = new();

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAppearanceComponent, AfterAutoHandleStateEvent>(OnSuitState);
        SubscribeLocalEvent<NinjaAppearanceItemComponent, ComponentInit>(OnItemInit);
        SubscribeLocalEvent<NinjaAppearanceItemComponent, EntParentChangedMessage>(OnItemReparented);
        SubscribeLocalEvent<NinjaAppearanceItemComponent, AfterAutoHandleStateEvent>(OnItemState);
        SubscribeLocalEvent<NinjaAppearanceItemComponent, ComponentShutdown>(OnItemShutdown);
        SubscribeLocalEvent<NinjaAppearanceItemComponent, AppearanceChangeEvent>(OnItemAppearanceChanged,
            after: [typeof(GenericVisualizerSystem), typeof(PdaVisualizerSystem)]);
        SubscribeLocalEvent<NinjaAppearanceItemComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after:
            [
                typeof(ClientClothingSystem),
                typeof(ToggleableVisualsSystem),
                typeof(FlippableClothingVisualizerSystem),
            ]);
        SubscribeLocalEvent<NinjaAppearanceItemComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem), typeof(ToggleableVisualsSystem)]);
        SubscribeLocalEvent<NinjaAppearanceComponent, GotEquippedEvent>(OnSuitEquipped,
            after: [typeof(ClientClothingSystem)]);
    }

    private void OnItemShutdown(Entity<NinjaAppearanceItemComponent> ent, ref ComponentShutdown args)
    {
        _savedIconStates.Remove(ent.Owner);
        _applied.Remove(ent.Owner);
    }

    private void OnSuitState(Entity<NinjaAppearanceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid())
            return;

        RefreshWearerGear(wearer);
    }

    private void OnSuitEquipped(Entity<NinjaAppearanceComponent> ent, ref GotEquippedEvent args)
    {
        RefreshWearerGear(args.Equipee);
    }

    private void RefreshWearerGear(EntityUid wearer)
    {
        foreach (var item in EnumerateWearerItems(wearer))
        {
            if (TryComp<NinjaAppearanceItemComponent>(item, out var itemComp))
                ResolveAndApply(item, itemComp);
        }
    }

    private void OnItemInit(Entity<NinjaAppearanceItemComponent> ent, ref ComponentInit args)
    {
        ResolveAndApply(ent.Owner, ent.Comp);
    }

    private void OnItemReparented(Entity<NinjaAppearanceItemComponent> ent, ref EntParentChangedMessage args)
    {
        ResolveAndApply(ent.Owner, ent.Comp);
    }

    private void OnItemState(Entity<NinjaAppearanceItemComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ResolveAndApply(ent.Owner, ent.Comp);
    }

    private void OnItemAppearanceChanged(Entity<NinjaAppearanceItemComponent> ent, ref AppearanceChangeEvent args)
    {
        ResolveAndApply(ent.Owner, ent.Comp);
    }

    private void OnGetEquipmentVisuals(EntityUid item, NinjaAppearanceItemComponent component,
        GetEquipmentVisualsEvent args)
    {
        var (prefix, style, visible) = ResolveAppearance(item, component);

        if (!visible)
        {
            args.Layers.Clear();
            return;
        }

        for (var i = 0; i < args.Layers.Count; i++)
        {
            var (key, layer) = args.Layers[i];
            ApplyColorwayToLayerData(item, layer, prefix, style);
            args.Layers[i] = (key, layer);
        }
    }

    private void OnGetInhandVisuals(EntityUid item, NinjaAppearanceItemComponent component,
        GetInhandVisualsEvent args)
    {
        var (prefix, style, _) = ResolveAppearance(item, component);

        for (var i = 0; i < args.Layers.Count; i++)
        {
            var (key, layer) = args.Layers[i];
            ApplyColorwayToLayerData(item, layer, prefix, style);
            args.Layers[i] = (key, layer);
        }
    }

    private void ApplyColorwayToLayerData(EntityUid item, PrototypeLayerData layer, string prefix, NinjaStyle style)
    {
        var state = layer.State;
        if (string.IsNullOrEmpty(state))
            return;

        RSI? rsi = null;
        if (layer.RsiPath != null)
            rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / layer.RsiPath).RSI;
        else if (TryComp(item, out SpriteComponent? sprite))
            rsi = sprite.BaseRSI;

        if (TryGetFirstState(rsi, GetColorCandidates(StripPrefixes(state), prefix, style), out var candidate))
            layer.State = candidate;
    }

    private void ResolveAndApply(EntityUid item, NinjaAppearanceItemComponent itemComp)
    {
        var (prefix, style, visible) = ResolveAppearance(item, itemComp);

        if (visible)
        {
            RestoreSpriteStates(item);
        }

        ApplySpriteLayers(item, prefix, style, visible);

        if (!_applied.TryGetValue(item, out var applied) || applied != (prefix, style, visible))
        {
            _applied[item] = (prefix, style, visible);
            _item.VisualsChanged(item);
        }
    }

    private (string Prefix, NinjaStyle Style, bool Visible) ResolveAppearance(EntityUid item, NinjaAppearanceItemComponent itemComp)
    {
        if (TryComp<NinjaAppearanceComponent>(item, out var suit))
            return (PrefixFor(suit.Colorway), suit.Style, ResolveVisible(suit, itemComp));

        if (FindSuit(Transform(item)) is { } claim)
            return (PrefixFor(claim.Comp.Colorway), claim.Comp.Style, ResolveVisible(claim.Comp, itemComp));

        return (PrefixFor(itemComp.FrozenColor), itemComp.FrozenStyle ?? NinjaStyle.Old, true);
    }

    private static string PrefixFor(NinjaColorway? colorway) => colorway switch
    {
        NinjaColorway.Blue => "blue-",
        NinjaColorway.Red => "red-",
        _ => "green-",
    };

    private static bool ResolveVisible(NinjaAppearanceComponent suit, NinjaAppearanceItemComponent itemComp) => itemComp.ItemType switch
    {
        NinjaAppearanceItemType.Scarf => suit.ScarfShown,
        NinjaAppearanceItemType.Helmet => suit.HelmetShown,
        _ => true,
    };

    private void ApplySpriteLayers(EntityUid item, string prefix, NinjaStyle style, bool visible)
    {
        if (!TryComp<SpriteComponent>(item, out var sprite))
            return;

        if (!visible)
        {
            // First hide: remember exactly what the sprite looked like, so showing it again can
            // restore it verbatim.
            if (!_savedIconStates.ContainsKey(item))
            {
                var states = new List<string?>();
                foreach (var layer in sprite.AllLayers)
                    states.Add(layer.RsiState.Name);
                _savedIconStates[item] = states.ToArray();
            }

            var index = 0;
            foreach (var layer in sprite.AllLayers)
            {
                var rsi = layer.ActualRsi ?? sprite.BaseRSI;
                if (rsi != null && rsi.TryGetState(DeactivatedIconState, out _))
                    _sprite.LayerSetRsiState((item, sprite), index, DeactivatedIconState);

                index++;
            }

            return;
        }

        var layerIndex = 0;
        foreach (var layer in sprite.AllLayers)
        {
            var state = layer.RsiState.Name;
            if (!string.IsNullOrEmpty(state))
            {
                var rsi = layer.ActualRsi ?? sprite.BaseRSI;
                if (rsi != null && TryGetFirstState(rsi, GetColorCandidates(StripPrefixes(state), prefix, style), out var candidate) &&
                    candidate != state)
                {
                    _sprite.LayerSetRsiState((item, sprite), layerIndex, candidate);
                }
            }

            layerIndex++;
        }
    }

    private void RestoreSpriteStates(EntityUid item)
    {
        if (!_savedIconStates.Remove(item, out var states) || !TryComp<SpriteComponent>(item, out var sprite))
            return;

        var count = 0;
        foreach (var _ in sprite.AllLayers)
            count++;
        count = Math.Min(states.Length, count);

        for (var i = 0; i < count; i++)
        {
            if (states[i] is { } state && !string.IsNullOrEmpty(state))
                _sprite.LayerSetRsiState((item, sprite), i, state);
        }
    }

    private static List<string> GetColorCandidates(string baseState, string prefix, NinjaStyle style)
    {
        var candidates = new List<string>(4);
        if (string.IsNullOrEmpty(baseState))
            return candidates;

        if (style == NinjaStyle.New)
        {
            candidates.Add($"{prefix}new-{baseState}");
            candidates.Add($"new-{baseState}");
        }

        candidates.Add(prefix + baseState);
        candidates.Add(baseState);
        return candidates;
    }

    private static bool TryGetFirstState(RSI? rsi, List<string> candidates, out string state)
    {
        foreach (var candidate in candidates)
        {
            if (rsi != null && rsi.TryGetState(candidate, out _))
            {
                state = candidate;
                return true;
            }
        }

        state = string.Empty;
        return false;
    }

    private static string StripColorPrefix(string state)
    {
        foreach (var prefix in ColorPrefixes)
        {
            if (state.StartsWith(prefix, StringComparison.Ordinal))
                return state.Substring(prefix.Length);
        }

        return state;
    }

    private static string StripStylePrefix(string state)
    {
        if (state.StartsWith(StylePrefix, StringComparison.Ordinal))
            return state.Substring(StylePrefix.Length);

        return state;
    }

    private static string StripPrefixes(string state) => StripStylePrefix(StripColorPrefix(state));

    private (EntityUid Uid, NinjaAppearanceComponent Comp)? FindSuit(TransformComponent xform)
    {
        var parent = xform.ParentUid;
        while (parent.IsValid())
        {
            if (!Exists(parent))
                return null;

            if (TryComp<NinjaAppearanceComponent>(parent, out var comp))
                return (parent, comp);

            foreach (var item in EnumerateWearerItems(parent))
            {
                if (TryComp<NinjaAppearanceComponent>(item, out var itemComp))
                    return (item, itemComp);
            }

            parent = Transform(parent).ParentUid;
        }

        return null;
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

        if (TryComp<HandsComponent>(wearer, out var hands)
            && TryComp<ContainerManagerComponent>(wearer, out var handContainers))
        {
            foreach (var handId in hands.Hands.Keys)
            {
                if (!handContainers.Containers.TryGetValue(handId, out var handContainer))
                    continue;

                foreach (var held in handContainer.ContainedEntities)
                {
                    yield return held;
                    foreach (var contained in EnumerateContainedItems(held))
                        yield return contained;
                }
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
}