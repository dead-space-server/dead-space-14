using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbWoundVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private sealed class WoundLayer
    {
        public readonly string Key;
        public readonly string Group;
        public readonly string[] States;
        public readonly HumanoidVisualLayers? Base;
        public int Offset;
        public int Stage;

        public WoundLayer(string key, string group, string[] states, HumanoidVisualLayers? baseLayer)
        {
            Key = key;
            Group = group;
            States = states;
            Base = baseLayer;
        }
    }

    private sealed class WoundLayers
    {
        public readonly Dictionary<LimbZone, List<WoundLayer>> ByZone = new();
        public readonly List<WoundLayer> All = new();
    }

    private readonly Dictionary<EntityUid, WoundLayers> _state = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimbWoundVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LimbWoundVisualsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LimbWoundVisualsComponent, AfterAutoHandleStateEvent>(OnVisState);
        SubscribeLocalEvent<LimbBodyComponent, AfterAutoHandleStateEvent>(OnBodyState);
        SubscribeLocalEvent<LimbPartWoundComponent, AfterAutoHandleStateEvent>(OnPartState);
    }

    private void OnStartup(Entity<LimbWoundVisualsComponent> ent, ref ComponentStartup args) => Refresh(ent);

    private void OnVisState(Entity<LimbWoundVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            RemoveLayers((ent, sprite));

        Refresh(ent);
    }

    private void OnBodyState(Entity<LimbBodyComponent> ent, ref AfterAutoHandleStateEvent args) => Refresh(ent);

    private void OnPartState(Entity<LimbPartWoundComponent> ent, ref AfterAutoHandleStateEvent args) => Refresh(ent);

    private void OnShutdown(Entity<LimbWoundVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            RemoveLayers((ent, sprite));

        _state.Remove(ent);
    }

    private void Refresh(EntityUid uid)
    {
        if (!TryComp<LimbWoundVisualsComponent>(uid, out var vis)
            || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var ent = new Entity<SpriteComponent>(uid, sprite);

        if (TryComp<LimbBodyComponent>(uid, out var body))
            RefreshBody(ent, vis, body);
        else if (TryComp<LimbPartWoundComponent>(uid, out var part))
            RefreshPart(ent, vis, part);
    }

    private void RefreshBody(Entity<SpriteComponent> ent, LimbWoundVisualsComponent vis, LimbBodyComponent body)
    {
        if (_state.TryGetValue(ent, out var existing) && Misplaced(ent, existing))
            RemoveLayers(ent);

        var layers = GetOrBuild(ent, vis, null);
        if (layers == null)
            return;

        var zones = body.Zones;
        foreach (var zone in LimbWoundVisualRules.Zones)
        {
            var i = LimbWoundRules.Index(zone);
            if ((uint) i >= (uint) zones.Count)
                continue;

            var state = zones[i];
            Apply(ent, vis, layers, zone, in state, LimbWoundVisualRules.BodyVisible(in state));
        }
    }

    private void RefreshPart(Entity<SpriteComponent> ent, LimbWoundVisualsComponent vis, LimbPartWoundComponent part)
    {
        var layers = GetOrBuild(ent, vis, part.Zone);
        if (layers == null)
            return;

        Apply(ent, vis, layers, part.Zone, in part.State, true);
    }

    private WoundLayers? GetOrBuild(Entity<SpriteComponent> ent, LimbWoundVisualsComponent vis, LimbZone? onlyZone)
    {
        if (_state.TryGetValue(ent, out var layers))
            return layers;

        if (vis.Groups.Count == 0)
            return null;

        layers = BuildLayers(ent, vis, onlyZone);
        _state[ent] = layers;
        return layers;
    }

    private bool Valid(EntityUid uid, LimbWoundVisualsComponent vis)
    {
        if (vis.Groups.Count > 0 && vis.States.Count > 0 && vis.States.Count == vis.Thresholds.Count)
            return true;

        Log.Error($"LimbWoundVisuals on {ToPrettyString(uid)} is invalid: need at least one group, "
                  + $"and thresholds ({vis.Thresholds.Count}) must match states ({vis.States.Count}).");
        return false;
    }

    private WoundLayers BuildLayers(Entity<SpriteComponent> ent, LimbWoundVisualsComponent vis, LimbZone? onlyZone)
    {
        var layers = new WoundLayers();
        if (!Valid(ent, vis))
            return layers;

        foreach (var zone in LimbWoundVisualRules.Zones)
        {
            if (onlyZone != null && zone != onlyZone)
                continue;

            int index;
            HumanoidVisualLayers? anchor = null;
            if (onlyZone != null)
            {
                index = 1;
            }
            else
            {
                if (LimbWoundVisualRules.Layer(zone) is not { } baseKey
                    || !_sprite.LayerMapTryGet(ent.AsNullable(), baseKey, out var baseIndex, false))
                    continue;

                index = baseIndex + 1;
                anchor = baseKey;
            }

            var list = new List<WoundLayer>();
            foreach (var (group, data) in vis.Groups)
            {
                var prefix = ResolvePrefix(zone, group, data, vis);
                var states = new string[vis.States.Count];
                for (var i = 0; i < states.Length; i++)
                {
                    states[i] = $"{prefix}_{group}_{vis.States[i]}";
                }

                var key = $"limb_{zone}_{group}";
                AddSpriteLayer(ent, key, data, states[0], index++);

                var wound = new WoundLayer(key, group, states, anchor);
                list.Add(wound);
                layers.All.Add(wound);
            }

            if (vis.Bandage is { } bandage && ResolveBandage(zone, bandage) is { } bandagePrefix)
            {
                var key = $"limb_{zone}_{LimbWoundVisualRules.Bandage}";
                var state = $"{bandagePrefix}_{LimbWoundVisualRules.Bandage}";
                AddSpriteLayer(ent, key, bandage, state, index++);

                var wound = new WoundLayer(key, LimbWoundVisualRules.Bandage, new[] { state }, anchor);
                list.Add(wound);
                layers.All.Add(wound);
            }

            if (list.Count > 0)
                layers.ByZone[zone] = list;
        }

        foreach (var wound in layers.All)
        {
            if (wound.Base is { } baseLayer
                && _sprite.LayerMapTryGet(ent.AsNullable(), wound.Key, out var i, false)
                && _sprite.LayerMapTryGet(ent.AsNullable(), baseLayer, out var bi, false))
                wound.Offset = i - bi;
        }

        return layers;
    }

    private void AddSpriteLayer(Entity<SpriteComponent> ent, string key, LimbWoundSprite data, string state, int index)
    {
        var layer = _sprite.AddLayer(ent.AsNullable(),
            new SpriteSpecifier.Rsi(new ResPath(data.Sprite), state), index);
        _sprite.LayerMapSet(ent.AsNullable(), key, layer);
        if (data.Color != null)
            _sprite.LayerSetColor(ent.AsNullable(), layer, Color.FromHex(data.Color));
        _sprite.LayerSetVisible(ent.AsNullable(), layer, false);
    }

    private bool Misplaced(Entity<SpriteComponent> ent, WoundLayers layers)
    {
        foreach (var wound in layers.All)
        {
            if (wound.Base is not { } baseLayer)
                continue;

            if (!_sprite.LayerMapTryGet(ent.AsNullable(), wound.Key, out var i, false)
                || !_sprite.LayerMapTryGet(ent.AsNullable(), baseLayer, out var bi, false))
                return true;

            if (i - bi != wound.Offset)
                return true;
        }

        return false;
    }

    private string ResolvePrefix(LimbZone zone, string group, LimbWoundSprite data, LimbWoundVisualsComponent vis)
    {
        var primary = LimbWoundVisualRules.Prefix(zone);
        var fallback = LimbWoundVisualRules.Prefix(LimbWoundVisualRules.Fallback(zone));
        if (primary == fallback)
            return primary;

        return HasState(data.Sprite, $"{primary}_{group}_{vis.States[0]}") ? primary : fallback;
    }

    private string? ResolveBandage(LimbZone zone, LimbWoundSprite data)
    {
        var primary = LimbWoundVisualRules.Prefix(zone);
        if (HasState(data.Sprite, $"{primary}_{LimbWoundVisualRules.Bandage}"))
            return primary;

        var fallback = LimbWoundVisualRules.Prefix(LimbWoundVisualRules.Fallback(zone));
        return HasState(data.Sprite, $"{fallback}_{LimbWoundVisualRules.Bandage}") ? fallback : null;
    }

    private bool HasState(string sprite, string state)
    {
        return _cache.TryGetResource<RSIResource>(new ResPath("/Textures") / new ResPath(sprite), out var res)
               && res.RSI.TryGetState(state, out _);
    }

    private void Apply(Entity<SpriteComponent> ent, LimbWoundVisualsComponent vis, WoundLayers layers,
        LimbZone zone, in LimbZoneState state, bool visible)
    {
        if (!layers.ByZone.TryGetValue(zone, out var list))
            return;

        foreach (var wound in list)
        {
            var stage = 0;
            if (visible)
            {
                stage = wound.Group == LimbWoundVisualRules.Bandage
                    ? LimbWoundVisualRules.BandageStage(in state)
                    : LimbWoundVisualRules.Stage(LimbWoundVisualRules.Severity(wound.Group, zone, in state), vis.Thresholds);
            }

            if (stage == wound.Stage)
                continue;

            wound.Stage = stage;
            if (!_sprite.LayerMapTryGet(ent.AsNullable(), wound.Key, out var layer, false))
                continue;

            if (stage <= 0)
            {
                _sprite.LayerSetVisible(ent.AsNullable(), layer, false);
                continue;
            }

            var si = Math.Min(stage - 1, wound.States.Length - 1);
            _sprite.LayerSetRsiState(ent.AsNullable(), layer, wound.States[si]);
            _sprite.LayerSetVisible(ent.AsNullable(), layer, true);
        }
    }

    private void RemoveLayers(Entity<SpriteComponent> ent)
    {
        if (!_state.TryGetValue(ent, out var layers))
            return;

        foreach (var wound in layers.All)
        {
            if (!_sprite.LayerMapTryGet(ent.AsNullable(), wound.Key, out var layer, false))
                continue;

            _sprite.LayerMapRemove(ent.AsNullable(), wound.Key);
            _sprite.RemoveLayer(ent.AsNullable(), layer);
        }

        _state.Remove(ent);
    }
}
