// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Animations;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>Draws the chain of a <see cref="LavalandChainArcComponent"/> swing from the wielder to the flung blade.</summary>
public sealed class LavalandChainArcSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new LavalandChainArcOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<LavalandChainArcOverlay>();
    }
}

public sealed class LavalandChainArcOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly IEntityManager _entManager;

    public LavalandChainArcOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var spriteSystem = _entManager.System<SpriteSystem>();
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var query = _entManager.EntityQueryEnumerator<LavalandChainArcComponent, TrackUserComponent, SpriteComponent, TransformComponent>();

        handle.SetTransform(Matrix3x2.Identity);
        while (query.MoveNext(out var chain, out var track, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId ||
                track.User is not { } user ||
                !_entManager.TryGetComponent(user, out TransformComponent? userXform) ||
                userXform.MapID != args.MapId)
            {
                continue;
            }

            // The blade is where the swing's sprite is drawn: its position, moved by the sprite's offset.
            var (position, rotation) = xformSystem.GetWorldPositionRotation(xform);
            var from = xformSystem.GetWorldPosition(userXform);
            var to = position + rotation.RotateVec(sprite.Offset);
            var diff = to - from;
            var length = diff.Length();
            if (length < 0.2f)
                continue;

            var texture = spriteSystem.Frame0(chain.Sprite);
            var width = texture.Width / (float) EyeManager.PixelsPerMeter;
            var angle = diff.ToWorldAngle();
            var color = chain.Color.WithAlpha(chain.Color.A * sprite.Color.A);

            // Link after link, a tile each, so the chain is not one stretched link.
            var links = Math.Max(1, (int) MathF.Round(length));
            var step = diff / links;
            var linkLength = length / links;
            for (var i = 0; i < links; i++)
            {
                var mid = from + step * (i + 0.5f);
                var box = new Box2(-width / 2f, -linkLength / 2f, width / 2f, linkLength / 2f).Translated(mid);
                handle.DrawTextureRect(texture, new Box2Rotated(box, angle, mid), color);
            }
        }
    }
}
