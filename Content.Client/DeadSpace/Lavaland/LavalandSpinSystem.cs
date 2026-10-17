// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland.Components;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>Turns the sprites of spinning entities; puts them back upright when they stop.</summary>
public sealed class LavalandSpinSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<LavalandSpinComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var spin, out var sprite))
        {
            if (spin.Spinning)
                _sprite.SetRotation((uid, sprite), sprite.Rotation + new Angle(spin.Speed * frameTime));
            else if (sprite.Rotation != Angle.Zero)
                _sprite.SetRotation((uid, sprite), Angle.Zero);
        }
    }
}
