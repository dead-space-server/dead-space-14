using Content.Shared.DeadSpace.PdaPainter;
using Robust.Client.GameObjects;
using Robust.Client.Utility;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace Content.Client.DeadSpace.PdaPainter;

/// <summary>
/// Renders the pixels painted onto a PDA by the PDA painter machine as an
/// overlay sprite layer above the PDA's base layer.
/// </summary>
public sealed class PdaPaintedVisualizerSystem : EntitySystem
{
    private const string OverlayLayerKey = "PdaPainterOverlay";

    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaPaintedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PdaPaintedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PdaPaintedComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<PdaPaintedComponent> ent, ref ComponentStartup args)
    {
        UpdateOverlay(ent);
    }

    private void OnShutdown(Entity<PdaPaintedComponent> ent, ref ComponentShutdown args)
    {
        RemoveOverlayLayer(ent);
    }

    private void OnState(Entity<PdaPaintedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateOverlay(ent);
    }

    private void RemoveOverlayLayer(Entity<PdaPaintedComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), OverlayLayerKey, out var index, false))
        {
            // Remove the layer itself, not just the map entry — otherwise the
            // painted texture would stay visible on the sprite.
            _sprite.RemoveLayer((ent, sprite), index, false);
            _sprite.LayerMapRemove((ent, sprite), OverlayLayerKey);
        }
    }

    private void UpdateOverlay(Entity<PdaPaintedComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var pixels = ent.Comp.Pixels;

        if (pixels.Count == 0)
        {
            RemoveOverlayLayer(ent);
            return;
        }

        using var image = new Image<Rgba32>(32, 32);
        var span = image.GetPixelSpan();

        foreach (var (index, packed) in pixels)
        {
            var x = index % 32;
            var y = index / 32;
            if (x < 0 || y < 0 || x >= 32 || y >= 32)
                continue;

            var r = (byte)((packed >> 16) & 0xFF);
            var g = (byte)((packed >> 8) & 0xFF);
            var b = (byte)(packed & 0xFF);
            var a = (byte)((packed >> 24) & 0xFF);
            span[y * 32 + x] = new Rgba32(r, g, b, a);
        }

        var texture = Texture.LoadFromImage(image);

        if (_sprite.LayerMapTryGet((ent, sprite), OverlayLayerKey, out var layerIndex, false))
        {
            _sprite.LayerSetTexture((ent, sprite), layerIndex, texture);
        }
        else
        {
            var index = System.Linq.Enumerable.Count(sprite.AllLayers);
            _sprite.AddBlankLayer((ent, sprite));
            _sprite.LayerMapSet((ent, sprite), OverlayLayerKey, index);
            _sprite.LayerSetTexture((ent, sprite), index, texture);
        }
    }
}
