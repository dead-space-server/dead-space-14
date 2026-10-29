// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.AshWolf;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland.AshWolf;

/// <summary>
/// The eyes of the Ash Wolf drawn again above the weather (and below the field of view), so they glow through the ash
/// of the strong storm while its body is lost in it.
/// </summary>
public sealed class LavalandAshWolfEyesOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public LavalandAshWolfEyesOverlay(IEntityManager entities)
    {
        _entities = entities;
        _sprite = entities.System<SpriteSystem>();
        _transform = entities.System<SharedTransformSystem>();
        // Above the weather's stencil overlay.
        ZIndex = 50;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var bounds = args.WorldAABB.Enlarged(2f);
        var query = _entities.EntityQueryEnumerator<LavalandAshWolfEyesComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId || !sprite.Visible)
                continue;

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            if (!bounds.Contains(position) ||
                !_sprite.LayerMapTryGet((uid, sprite), LavalandAshWolfLayers.Eyes, out var index, false) ||
                sprite[index] is not SpriteComponent.Layer { Visible: true, ActualState: { } state } layer)
            {
                continue;
            }

            var direction = layer.EffectiveDirection(state, rotation + eyeRotation,
                sprite.EnableDirectionOverride ? sprite.DirectionOverride : null);
            var texture = state.GetFrame(direction, Math.Clamp(layer.AnimationFrame, 0, Math.Max(0, state.DelayCount - 1)));
            var offset = sprite.Offset + layer.Offset;

            // Upright on the screen, whatever the eye's rotation.
            handle.SetTransform(Matrix3x2.CreateRotation((float) -eyeRotation.Theta) * Matrix3x2.CreateTranslation(position));
            var size = new Vector2(texture.Width, texture.Height) / EyeManager.PixelsPerMeter;
            handle.DrawTextureRect(texture, Box2.CenteredAround(offset, size), layer.Color);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}

public sealed class LavalandAshWolfEyesSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private LavalandAshWolfEyesOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LavalandAshWolfEyesOverlay(EntityManager);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}
