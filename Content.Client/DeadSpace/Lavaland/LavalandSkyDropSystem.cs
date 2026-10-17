// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>Moves the sprite of <see cref="LavalandSkyDropComponent"/> down from the sky, or up into it.</summary>
public sealed class LavalandSkyDropSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandSkyDropComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var drop, out var sprite))
        {
            var progress = drop.Duration <= 0f
                ? 1f
                : Math.Clamp((float) (now - drop.Start).TotalSeconds / drop.Duration, 0f, 1f);

            // Falls faster and faster; rises slowly at first.
            var height = drop.Rising
                ? drop.Height * progress * progress
                : drop.Height * (1f - progress) * (1f - progress);

            _sprite.SetOffset((uid, sprite), new Vector2(0f, height));

            // Keep the place of the sprite in the render tree with it, or it is culled high up in the air.
            _spriteTree.QueueTreeUpdate((uid, sprite));
        }
    }
}
