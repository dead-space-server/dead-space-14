// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland.Bosses;

/// <summary>
/// Draws <see cref="LavalandSpriteMotionComponent"/>: the sprite flies along its way, spinning and fading.
/// Also spawns the things of the boss intros on this client only.
/// </summary>
public sealed class LavalandSpriteMotionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;

    /// <summary>The sprite as the prototype made it, before any motion.</summary>
    private readonly Dictionary<EntityUid, (Vector2 Offset, Color Color, Angle Rotation)> _base = new();

    /// <summary>Client-side intro things and when they go away.</summary>
    private readonly List<(EntityUid Uid, TimeSpan At)> _expiring = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandSpriteMotionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<LavalandSpriteMotionComponent> ent, ref ComponentShutdown args)
    {
        if (_base.Remove(ent.Owner, out var original) && TryComp<SpriteComponent>(ent, out var sprite))
        {
            _sprite.SetOffset((ent.Owner, sprite), original.Offset);
            _sprite.SetColor((ent.Owner, sprite), original.Color);
            _sprite.SetRotation((ent.Owner, sprite), EndRotation(ent.Comp, original.Rotation));
            _spriteTree.QueueTreeUpdate((ent.Owner, sprite));
        }
    }

    private static Angle EndRotation(LavalandSpriteMotionComponent motion, Angle original)
    {
        return motion.EndRotation is { } degrees ? original + Angle.FromDegrees(degrees) : original;
    }

    /// <summary>Spawns a thing of an intro here, on this client only.</summary>
    public void SpawnFx(LavalandIntroFx fx, MapId mapId)
    {
        if (!_prototype.HasIndex<EntityPrototype>(fx.Prototype))
            return;

        var uid = Spawn(fx.Prototype, new MapCoordinates(fx.Position, mapId));
        var now = _timing.CurTime;

        if (fx.State is { } state && TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.LayerSetRsiState((uid, sprite), 0, state);

        if (fx.Lifetime > 0f)
            _expiring.Add((uid, now + TimeSpan.FromSeconds(fx.Lifetime)));

        if (fx.Duration <= 0f)
            return;

        var motion = EnsureComp<LavalandSpriteMotionComponent>(uid);
        motion.Start = now;
        motion.Duration = fx.Duration;
        motion.From = fx.From;
        motion.To = fx.To;
        motion.Arc = fx.Arc;
        motion.Spin = fx.Spin;
        motion.FadeOut = fx.FadeOut;
        motion.FadeIn = fx.FadeIn;
        motion.Accelerate = fx.Accelerate;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        for (var i = _expiring.Count - 1; i >= 0; i--)
        {
            var (uid, at) = _expiring[i];
            if (now < at)
                continue;

            _expiring.RemoveAt(i);
            if (!TerminatingOrDeleted(uid))
                QueueDel(uid);
        }

        var query = EntityQueryEnumerator<LavalandSpriteMotionComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var motion, out var sprite))
        {
            if (!_base.TryGetValue(uid, out var original))
                _base[uid] = original = (sprite.Offset, sprite.Color, sprite.Rotation);

            var progress = motion.Duration <= 0f
                ? 1f
                : Math.Clamp((float) (now - motion.Start).TotalSeconds / motion.Duration, 0f, 1f);
            var along = motion.Accelerate ? progress * progress : progress;

            var offset = Vector2.Lerp(motion.From, motion.To, along) +
                         new Vector2(0f, motion.Arc * 4f * progress * (1f - progress));
            _sprite.SetOffset((uid, sprite), original.Offset + offset);

            // The offset moves the drawn sprite: its place in the render tree must follow, or it is culled.
            _spriteTree.QueueTreeUpdate((uid, sprite));

            if (motion.Spin != 0f && progress < 1f)
                _sprite.SetRotation((uid, sprite), sprite.Rotation + new Angle(motion.Spin * frameTime));
            else if (motion.Spin != 0f)
                _sprite.SetRotation((uid, sprite), EndRotation(motion, original.Rotation));

            var alpha = (motion.FadeIn ? progress : 1f) * (motion.FadeOut ? 1f - progress : 1f);
            if (motion.FadeIn || motion.FadeOut)
                _sprite.SetColor((uid, sprite), original.Color.WithAlpha(original.Color.A * alpha));
        }
    }
}
