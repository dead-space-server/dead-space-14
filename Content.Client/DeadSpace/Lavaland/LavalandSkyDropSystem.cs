// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Moves the sprite of <see cref="LavalandSkyDropComponent"/> down from the sky, or up into it - always straight up
/// the screen, however the thing is turned - or shrinks it away into a pit.
/// </summary>
public sealed class LavalandSkyDropSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;

    /// <summary>
    /// When this client first saw each drop start. The state comes in late, and a short rise would be mostly over by
    /// then - the thing would just vanish; played from the moment it is seen, it is seen whole.
    /// </summary>
    private readonly Dictionary<EntityUid, (TimeSpan ServerStart, TimeSpan LocalStart)> _seen = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandSkyDropComponent, ComponentShutdown>(OnShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<LavalandSkyDropComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var drop, out var sprite))
        {
            if (!_seen.TryGetValue(uid, out var seen) || seen.ServerStart != drop.Start)
            {
                seen = (drop.Start, drop.Start > now ? drop.Start : now);
                _seen[uid] = seen;
            }

            var progress = drop.Duration <= 0f
                ? 1f
                : Math.Clamp((float) (now - seen.LocalStart).TotalSeconds / drop.Duration, 0f, 1f);

            if (drop.Sinking)
            {
                // Gone down the pit: smaller and smaller where it lies.
                _sprite.SetScale((uid, sprite), Vector2.One * Math.Max(0.01f, 1f - progress));
                _spriteTree.QueueTreeUpdate((uid, sprite));
                continue;
            }

            // Shoots up sharply and slows high up; falls faster and faster.
            var height = drop.Rising
                ? drop.Height * (1f - (1f - progress) * (1f - progress))
                : drop.Height * (1f - progress) * (1f - progress);

            // The offset is in the sprite's own frame: a thrown item lies turned any way, and "up" for it would be
            // to the side. Turned back by the sprite's rotation, it is straight up the screen.
            var spriteRotation = sprite.NoRotation ? -eyeRotation : _transform.GetWorldRotation(uid);
            var screenUp = (-eyeRotation).RotateVec(new Vector2(drop.Slant * height, height));
            _sprite.SetOffset((uid, sprite), (-spriteRotation).RotateVec(screenUp));

            // Keep the place of the sprite in the render tree with it, or it is culled high up in the air.
            _spriteTree.QueueTreeUpdate((uid, sprite));
        }
    }

    private void OnShutdown(Entity<LavalandSkyDropComponent> ent, ref ComponentShutdown args)
    {
        _seen.Remove(ent.Owner);
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetOffset((ent.Owner, sprite), Vector2.Zero);
        _sprite.SetScale((ent.Owner, sprite), Vector2.One);
    }
}
