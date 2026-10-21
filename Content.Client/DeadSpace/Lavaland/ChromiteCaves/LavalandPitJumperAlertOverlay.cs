// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>The exclamation mark popping up over a pit jumper that has spotted somebody.</summary>
public sealed class LavalandPitJumperAlertOverlay : Overlay
{
    private static readonly SpriteSpecifier.Rsi Exclamation = new(new ResPath("Effects/text.rsi"), "exclamation");

    private readonly IEntityManager _entities;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public LavalandPitJumperAlertOverlay(IEntityManager entities, SpriteSystem sprite)
    {
        _entities = entities;
        _sprite = sprite;
        _transform = entities.System<SharedTransformSystem>();
        ZIndex = 7;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        Texture? texture = null;
        var query = _entities.EntityQueryEnumerator<LavalandPitJumperAlertComponent, TransformComponent>();
        while (query.MoveNext(out _, out var alert, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            texture ??= _sprite.Frame0(Exclamation);
            var t = alert.Elapsed;

            // Pops up with an overshoot, then bobs.
            var pop = t < 0.15f ? t / 0.15f * 1.3f : t < 0.25f ? 1.3f - (t - 0.15f) / 0.1f * 0.3f : 1f;
            var bob = MathF.Sin(t * 12f) * 0.05f;
            var center = _transform.GetWorldPosition(xform) + new Vector2(0f, 1f + bob);
            var size = new Vector2(0.75f) * pop;
            handle.DrawTextureRect(texture, Box2.CenteredAround(center, size), Color.FromHex("#ffdd44"));
        }
    }
}
