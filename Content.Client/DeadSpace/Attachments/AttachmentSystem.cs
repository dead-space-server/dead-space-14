// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Attachments;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Attachments;

/// <summary>Draws <see cref="AttachmentComponent.Layers"/> over the weapon holding them.</summary>
public sealed class AttachmentSystem : SharedAttachmentSystem
{
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string LayerPrefix = "attachment-layer-";

    /// <summary>How many overlay layers each holder currently has.</summary>
    private readonly Dictionary<EntityUid, int> _layerCounts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AttachmentHolderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AttachmentHolderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AttachmentHolderComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(Entity<AttachmentHolderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLayers(ent);
    }

    private void OnStartup(Entity<AttachmentHolderComponent> ent, ref ComponentStartup args)
    {
        UpdateLayers(ent);
    }

    private void OnShutdown(Entity<AttachmentHolderComponent> ent, ref ComponentShutdown args)
    {
        _layerCounts.Remove(ent);
    }

    protected override void AttachmentsChanged(Entity<AttachmentHolderComponent> holder)
    {
        base.AttachmentsChanged(holder);
        UpdateLayers(holder);
    }

    private void UpdateLayers(Entity<AttachmentHolderComponent> holder)
    {
        if (!TryComp<SpriteComponent>(holder, out var sprite))
            return;

        var spriteEnt = (holder.Owner, sprite);
        if (_layerCounts.TryGetValue(holder, out var old))
        {
            for (var i = 0; i < old; i++)
            {
                _sprite.RemoveLayer(spriteEnt, LayerPrefix + i, false);
            }
        }

        var count = 0;

        // Knife and flashlight drawn over the weapon while they are fixed on it.
        foreach (var (slot, overlay) in holder.Comp.SlotOverlays)
        {
            if (GetSlotItem(holder, slot) == null)
                continue;

            var lit = slot == holder.Comp.LightSlot && holder.Comp.LightOn && overlay.StateOn != null;
            var state = lit ? overlay.StateOn! : overlay.State;
            if (!HasState(sprite, overlay.Sprite, state))
                continue;

            var key = LayerPrefix + count;
            _sprite.AddLayer(spriteEnt, new PrototypeLayerData
            {
                RsiPath = overlay.Sprite.ToString(),
                State = state,
                Offset = overlay.Offset,
                Shader = lit ? "unshaded" : null,
                MapKeys = new HashSet<string> { key },
            }, null);
            count++;
        }

        foreach (var attachment in GetAttachments(holder))
        {
            foreach (var layer in attachment.Comp.Layers)
            {
                var path = layer.Sprite ?? holder.Comp.OverlaySprite ?? CompOrNull<AttachmentOverlaySpriteComponent>(holder)?.Sprite;
                if (!HasState(sprite, path, layer.State))
                    continue;

                var key = LayerPrefix + count;
                var data = new PrototypeLayerData
                {
                    RsiPath = path?.ToString(),
                    State = layer.State,
                    Color = layer.Color,
                    Shader = layer.Unshaded ? "unshaded" : null,
                    MapKeys = new HashSet<string> { key },
                };
                _sprite.AddLayer(spriteEnt, data, null);
                count++;
            }
        }

        _layerCounts[holder] = count;
    }

    /// <summary>Skips layers whose state is missing instead of drawing the error sprite.</summary>
    private bool HasState(SpriteComponent sprite, ResPath? path, string state)
    {
        RSI? rsi;
        if (path == null)
            rsi = sprite.BaseRSI;
        else if (_resources.TryGetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / path.Value, out var resource))
            rsi = resource.RSI;
        else
            rsi = null;

        return rsi != null && rsi.TryGetState(state, out _);
    }
}
