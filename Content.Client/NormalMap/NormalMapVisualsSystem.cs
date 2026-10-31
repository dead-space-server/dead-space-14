using System.Linq;
using System.Numerics;
using System.Text;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.NormalMap;

// THE CLOSEST THING TO THE OPPOSITE OF Him A HIVEMIND THAT USES DISGUISE TO ENSLAVE THE WEAK INJECTS ITSELF INTO YOUR SOUL GRACE LEFT ROTTED A STAB WHOSE PAIN YOU DON'T FEEL BUT He DOES THE ONLY CURE? RETALLIATION.
public sealed class NormalMapVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IResourceCache _res = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private const string ShaderId = "NormalMapped";
    private const string ParameterTexture = "normalMap";
    private const string ParameterUV = "normalUV";
    private const string NormalSuffix = "_normal";
    private const float ScanInterval = 0.5f;

    private readonly Dictionary<(string Rsi, string State), bool> _probeCache = new();
    private readonly Dictionary<string, Texture> _textureCache = new();
    private readonly HashSet<EntityUid> _queued = new();

    private float _scanTimer;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpriteComponent, AppearanceChangeEvent>(OnAppearance);
        SubscribeLocalEvent<NormalMapVisualsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnAppearance(Entity<SpriteComponent> ent, ref AppearanceChangeEvent args)
    {
        _queued.Add(ent);
    }

    private void OnShutdown(Entity<NormalMapVisualsComponent> ent, ref ComponentShutdown args)
    {
        TryComp<SpriteComponent>(ent.Owner, out var sprite);
        Cleanup(new Entity<SpriteComponent?>(ent.Owner, sprite), ent.Comp);
    }

    public override void Update(float frameTime)
    {
        _scanTimer += frameTime;
        if (_scanTimer >= ScanInterval)
        {
            _scanTimer = 0;

            // Re-check everything that already has normal maps (clothing may have been re-rendered).
            var appliedQuery = AllEntityQuery<SpriteComponent, NormalMapVisualsComponent>();
            while (appliedQuery.MoveNext(out var uid, out _, out _))
            {
                _queued.Add(uid);
            }

            // Look for new sprites that have normal maps. Only these get the component, so the
            // per-frame pass below stays cheap.
            var spriteQuery = AllEntityQuery<SpriteComponent>();
            while (spriteQuery.MoveNext(out var uid, out var sprite))
            {
                if (HasComp<NormalMapVisualsComponent>(uid))
                    continue;

                if (HasAnyNormal((uid, sprite)))
                    _queued.Add(uid);
            }
        }

        // Validate tracked sprites every frame: a clothing layer can be removed and re-created at
        // any moment, in which case its layer object is no longer part of the sprite and the normal
        // map has to be rebuilt for the new one.
        var query = AllEntityQuery<SpriteComponent, NormalMapVisualsComponent>();
        while (query.MoveNext(out var uid, out var sprite, out var comp))
        {
            Entity<SpriteComponent?> ent = new(uid, sprite);
            if (!IsIntact(ent, comp))
            {
                _queued.Add(uid);
                continue;
            }

            UpdateCells(ent, comp);
        }

        foreach (var uid in _queued)
        {
            if (!Deleted(uid) && TryComp<SpriteComponent>(uid, out var sprite))
                Apply((uid, sprite));
        }

        _queued.Clear();
    }

    private void Apply(Entity<SpriteComponent> entity)
    {
        Entity<SpriteComponent?> ent = new(entity.Owner, entity.Comp);
        var uid = entity.Owner;
        var signature = BuildSignature(ent);

        NormalMapVisualsComponent? comp = null;
        if (TryComp<NormalMapVisualsComponent>(uid, out var existing))
            comp = existing;

        if (!HasAnyNormal(ent))
        {
            if (comp != null)
            {
                Cleanup(ent, comp);
                RemComp<NormalMapVisualsComponent>(uid);
            }

            return;
        }

        if (comp != null && comp.Signature == signature && IsIntact(ent, comp))
            return;

        comp ??= EnsureComp<NormalMapVisualsComponent>(uid);
        Cleanup(ent, comp);
        comp.Signature = signature;

        var count = entity.Comp.AllLayers.Count();
        for (var index = 0; index < count; index++)
        {
            if (!_sprite.TryGetLayer(ent, index, out var layer, false))
                continue;

            // Never touch shader-parameter helper layers.
            if (layer.CopyToShaderParameters != null || !layer.Visible)
                continue;

            var rsi = _sprite.LayerGetEffectiveRsi(ent, index);
            if (rsi == null)
                continue;

            var stateId = _sprite.LayerGetRsiState(ent, index);
            if (!stateId.IsValid || string.IsNullOrEmpty(stateId.Name) || !rsi.TryGetState(stateId, out var state))
                continue;

            var stateName = stateId.Name!;
            if (!HasNormalFile(rsi.Path, stateName))
                continue;

            if (!TryGetNormalTexture(rsi.Path, stateName, out var normalSheet))
                continue;

            // The normal image may be a flat 1-frame image while the state has directions. In that
            // case just use the whole image for every direction.
            var isSheet = state.Size.X > 0 &&
                          state.Size.Y > 0 &&
                          normalSheet.Width % state.Size.X == 0 &&
                          normalSheet.Height % state.Size.Y == 0 &&
                          normalSheet.Width / state.Size.X * (normalSheet.Height / state.Size.Y) >=
                          LayerGetDirectionCount(state) * state.DelayCount;

            var shader = _prototype.Index<ShaderPrototype>(ShaderId).InstanceUnique();
            shader.SetParameter(ParameterTexture, normalSheet);

            comp.Applied.Add(new NormalMapVisualsComponent.AppliedEntry
            {
                TargetLayer = layer,
                Shader = shader,
                OriginalShader = layer.Shader,
                OriginalShaderPrototype = layer.ShaderPrototype,
                State = state,
                NormalSheet = normalSheet,
                FrameSize = state.Size,
                IsSheet = isSheet,
                TargetRsi = rsi.Path.ToString(),
                TargetState = stateName,
            });

            entity.Comp.LayerSetShader(index, shader, ShaderId);
        }

        // Bind the correct normal-sheet cell right away, otherwise the whole sheet would be used
        // for a frame.
        UpdateCells(ent, comp);
    }

    private static void Cleanup(Entity<SpriteComponent?> ent, NormalMapVisualsComponent comp)
    {
        foreach (var applied in comp.Applied)
        {
            var layer = applied.TargetLayer;
            if (layer != null &&
                ent.Comp != null &&
                ReferenceEquals(layer.Shader, applied.Shader) &&
                TryGetLayerIndex(ent, layer, out var index))
            {
                if (applied.OriginalShaderPrototype == SpriteSystem.UnshadedId)
                    ent.Comp.LayerSetShader(index, SpriteSystem.UnshadedId.Id);
                else
                    ent.Comp.LayerSetShader(index, applied.OriginalShader, applied.OriginalShaderPrototype);
            }

            // The instance is exclusively ours, so it is safe to dispose after restoring the layer.
            applied.Shader?.Dispose();
        }

        comp.Applied.Clear();
    }

    private static bool IsIntact(Entity<SpriteComponent?> ent, NormalMapVisualsComponent comp)
    {
        if (comp.Applied.Count == 0)
            return false;

        foreach (var applied in comp.Applied)
        {
            if (applied.TargetLayer == null || applied.Shader == null)
                return false;

            if (!ReferenceEquals(applied.TargetLayer.Shader, applied.Shader))
                return false;

            if (!TryGetLayerIndex(ent, applied.TargetLayer, out _))
                return false;
        }

        return true;
    }

    private static bool TryGetLayerIndex(Entity<SpriteComponent?> ent, Layer layer, out int index)
    {
        index = -1;
        if (ent.Comp == null)
            return false;

        var current = 0;
        foreach (var candidate in ent.Comp.AllLayers)
        {
            if (ReferenceEquals(candidate, layer))
            {
                index = current;
                return true;
            }

            current++;
        }

        return false;
    }

    private void UpdateCells(Entity<SpriteComponent?> ent, NormalMapVisualsComponent comp)
    {
        if (comp.Applied.Count == 0)
            return;

        var angle = _transform.GetWorldRotation(ent.Owner) + _eye.CurrentEye.Rotation;
        angle = angle.Reduced().FlipPositive();

        foreach (var applied in comp.Applied)
        {
            var state = applied.State;
            var sheet = applied.NormalSheet;
            var shader = applied.Shader;
            if (state == null || sheet == null || shader == null)
                continue;

            var target = applied.TargetLayer;
            var dir = ent.Comp is { EnableDirectionOverride: true, DirectionOverride: { } overrideDirection }
                ? overrideDirection.Convert(state.RsiDirections)
                : Layer.GetDirection(state.RsiDirections, angle);

            dir = dir.OffsetRsiDir(target.DirOffset);

            // Flat source-sheet index: directions are rows, animation frames are columns.
            var flat = applied.IsSheet
                ? (int) dir * state.DelayCount + target.AnimationFrame
                : -1;

            if (flat == applied.LastCell)
                continue;

            var uv = flat < 0
                ? new Vector4(0, 0, 1, 1)
                : GetCellUV(sheet, applied.FrameSize, flat);

            applied.LastCell = flat;
            shader.SetParameter(ParameterUV, uv);
        }
    }

    /// <summary>
    ///     Computes the normalized UV rect of a cell inside the normal sheet, matching the UVs the
    ///     sprite renderer produces (origin in the top-left corner).
    /// </summary>
    private static Vector4 GetCellUV(Texture sheet, Vector2i frameSize, int flat)
    {
        if (frameSize.X <= 0 || frameSize.Y <= 0)
            return new Vector4(0, 0, 1, 1);

        var columns = sheet.Width / frameSize.X;
        if (columns <= 0)
            return new Vector4(0, 0, 1, 1);

        var x = flat % columns * frameSize.X;
        var y = flat / columns * frameSize.Y;

        if (x + frameSize.X > sheet.Width || y + frameSize.Y > sheet.Height)
            return new Vector4(0, 0, 1, 1);

        var width = sheet.Width;
        var height = sheet.Height;

        return new Vector4(
            x / (float) width,
            1f - (y + frameSize.Y) / (float) height,
            (x + frameSize.X) / (float) width,
            1f - y / (float) height);
    }

    private static int LayerGetDirectionCount(RSI.State state)
    {
        return state.RsiDirections switch
        {
            RsiDirectionType.Dir1 => 1,
            RsiDirectionType.Dir4 => 4,
            RsiDirectionType.Dir8 => 8,
            _ => 1,
        };
    }

    private bool HasAnyNormal(Entity<SpriteComponent?> ent)
    {
        var count = ent.Comp?.AllLayers.Count() ?? 0;
        for (var index = 0; index < count; index++)
        {
            if (!_sprite.TryGetLayer(ent, index, out var layer, false))
                continue;

            if (layer.CopyToShaderParameters != null || !layer.Visible)
                continue;

            var rsi = _sprite.LayerGetEffectiveRsi(ent, index);
            if (rsi == null)
                continue;

            var state = _sprite.LayerGetRsiState(ent, index);
            if (!state.IsValid || string.IsNullOrEmpty(state.Name))
                continue;

            if (HasNormalFile(rsi.Path, state.Name!))
                return true;
        }

        return false;
    }

    private string BuildSignature(Entity<SpriteComponent?> ent)
    {
        var sb = new StringBuilder();
        var count = ent.Comp?.AllLayers.Count() ?? 0;
        for (var index = 0; index < count; index++)
        {
            if (!_sprite.TryGetLayer(ent, index, out var layer, false))
                continue;

            if (layer.CopyToShaderParameters != null)
                continue;

            var rsi = _sprite.LayerGetEffectiveRsi(ent, index);
            var state = _sprite.LayerGetRsiState(ent, index);
            sb.Append(rsi?.Path.ToString() ?? "-")
                .Append(':')
                .Append(state.Name ?? "-")
                .Append(':')
                .Append(layer.Visible ? '1' : '0')
                .Append(';');
        }

        return sb.ToString();
    }

    private bool HasNormalFile(ResPath rsiPath, string state)
    {
        var key = (rsiPath.ToString(), state);
        if (_probeCache.TryGetValue(key, out var cached))
            return cached;

        var has = _res.ContentFileExists(rsiPath / (state + NormalSuffix + ".png"));
        _probeCache[key] = has;
        return has;
    }

    private bool TryGetNormalTexture(ResPath rsiPath, string state, out Texture texture)
    {
        var path = rsiPath / (state + NormalSuffix + ".png");
        var pathString = path.ToString();
        if (_textureCache.TryGetValue(pathString, out var cached))
        {
            texture = cached;
            return true;
        }

        if (_res.TryGetResource<TextureResource>(path, out var resource) && resource.Texture != null)
        {
            _textureCache[pathString] = resource.Texture;
            texture = resource.Texture;
            return true;
        }

        texture = default!;
        return false;
    }
}
