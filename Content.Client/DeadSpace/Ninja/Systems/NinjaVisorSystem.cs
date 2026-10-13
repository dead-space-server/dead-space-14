// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class NinjaVisorSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaVisorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NinjaVisorComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<NinjaVisorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NinjaVisorComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<NinjaVisorComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem)]);
    }

    private void OnInit(Entity<NinjaVisorComponent> ent, ref ComponentInit args)
        => ApplySpriteStates(ent.Owner, ent.Comp.Mode);

    private void OnState(Entity<NinjaVisorComponent> ent, ref AfterAutoHandleStateEvent args)
        => ApplySpriteStates(ent.Owner, ent.Comp.Mode);

    private void OnShutdown(Entity<NinjaVisorComponent> ent, ref ComponentShutdown args)
        => ApplySpriteStates(ent.Owner, NinjaVisorMode.Protective);

    private void OnGetEquipmentVisuals(Entity<NinjaVisorComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        var suffix = SuffixFor(ent.Comp.Mode);
        for (var i = 0; i < args.Layers.Count; i++)
        {
            var (key, layer) = args.Layers[i];
            SwapState(ent.Owner, layer, suffix);
            args.Layers[i] = (key, layer);
        }
    }

    private void OnGetInhandVisuals(Entity<NinjaVisorComponent> ent, ref GetInhandVisualsEvent args)
    {
        var suffix = SuffixFor(ent.Comp.Mode);
        for (var i = 0; i < args.Layers.Count; i++)
        {
            var (key, layer) = args.Layers[i];
            SwapState(ent.Owner, layer, suffix);
            args.Layers[i] = (key, layer);
        }
    }

    private void SwapState(EntityUid item, PrototypeLayerData layer, string suffix)
    {
        var state = StripModeSuffix(layer.State);
        if (string.IsNullOrEmpty(state))
            return;

        RSI? rsi = null;
        if (layer.RsiPath != null)
            rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / layer.RsiPath).RSI;
        else if (TryComp<SpriteComponent>(item, out var sprite))
            rsi = sprite.BaseRSI;

        layer.State = GetCandidateState(rsi, state, suffix);
    }

    private void ApplySpriteStates(EntityUid item, NinjaVisorMode mode)
    {
        if (!TryComp<SpriteComponent>(item, out var sprite))
            return;

        var suffix = SuffixFor(mode);
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            var state = StripModeSuffix(layer.RsiState.Name);
            if (!string.IsNullOrEmpty(state))
            {
                var rsi = layer.ActualRsi ?? sprite.BaseRSI;
                var candidate = GetCandidateState(rsi, state, suffix);
                if (candidate != layer.RsiState.Name)
                    _sprite.LayerSetRsiState((item, sprite), index, candidate);
            }

            index++;
        }

        _item.VisualsChanged(item);
    }

    private static readonly string[] ModeSuffixes = { "-thermal", "-night" };

    private static string StripModeSuffix(string? state)
    {
        if (string.IsNullOrEmpty(state))
            return state!;

        foreach (var suffix in ModeSuffixes)
        {
            if (state.EndsWith(suffix, StringComparison.Ordinal))
                return state.Substring(0, state.Length - suffix.Length);
        }

        return state!;
    }

    private static string GetCandidateState(RSI? rsi, string state, string suffix)
    {
        if (!string.IsNullOrEmpty(suffix) && rsi != null && rsi.TryGetState(state + suffix, out _))
            return state + suffix;

        return state;
    }

    private static string SuffixFor(NinjaVisorMode mode) => mode switch
    {
        NinjaVisorMode.Thermal => "-thermal",
        NinjaVisorMode.NightVision => "-night",
        _ => "",
    };
}