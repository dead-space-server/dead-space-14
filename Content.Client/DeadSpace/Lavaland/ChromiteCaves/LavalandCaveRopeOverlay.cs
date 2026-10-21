// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>
/// The hook flying straight up out of the caves with its rope, then the rope taut above the doll while it is
/// pulled up. Rope and tip are those of the thing he climbs with (a grappling gun, a sticky hand...);
/// the grappling gun's are only the fallback.
/// </summary>
public sealed class LavalandCaveRopeOverlay : Overlay
{
    private const float RopeHeight = 13f;

    private static readonly SpriteSpecifier.Rsi RopeSprite =
        new(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");

    private static readonly SpriteSpecifier.Rsi HookSprite =
        new(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "hook");

    private readonly IEntityManager _entities;
    private readonly IPrototypeManager _prototype;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public LavalandCaveRopeOverlay(IEntityManager entities, SpriteSystem sprite)
    {
        _entities = entities;
        _prototype = IoCManager.Resolve<IPrototypeManager>();
        _sprite = sprite;
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var query = _entities.EntityQueryEnumerator<LavalandCaveClimbComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var climb, out var sprite, out var xform))
        {
            if (climb.Stage == LavalandCaveClimbStage.Hanging || xform.MapID != args.MapId)
                continue;

            var rope = _sprite.Frame0(climb.RopeSprite ?? RopeSprite);
            var hook = climb.TipPrototype is { } tip && _prototype.HasIndex<EntityPrototype>(tip)
                ? _sprite.GetPrototypeIcon(tip).Default
                : _sprite.Frame0(HookSprite);

            var position = _transform.GetWorldPosition(xform);
            var t = Math.Clamp(climb.Elapsed / MathF.Max(0.1f, climb.Duration), 0f, 1f);
            float bottom;
            float top;
            if (climb.Stage == LavalandCaveClimbStage.Rope)
            {
                // The hook shoots up, slowing down at the top.
                bottom = position.Y + 0.3f;
                top = bottom + RopeHeight * (1f - (1f - t) * (1f - t));
            }
            else
            {
                // Taut above the doll going up.
                bottom = position.Y + sprite.Offset.Y + 0.4f;
                top = position.Y + RopeHeight + 0.3f;
            }

            if (top <= bottom)
                continue;

            for (var y = bottom; y < top; y += 1f)
            {
                var segmentTop = MathF.Min(y + 1f, top);
                handle.DrawTextureRect(rope, new Box2(position.X - 0.5f, y, position.X + 0.5f, segmentTop));
            }

            if (climb.Stage == LavalandCaveClimbStage.Rope)
            {
                // Projectile sprites point north: up, the way it flies.
                var tipPosition = new Vector2(position.X, top);
                handle.DrawTextureRect(hook, Box2.CenteredAround(tipPosition, Vector2.One));
            }
        }
    }
}
