// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Blizzard on lavaland: snow cover creeps in from the map borders, whitening floor, rocks and flora under it.
/// </summary>
public sealed class LavalandSnowCoverOverlay : Overlay
{
    private const int Strips = 12;

    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public LavalandSnowCoverOverlay(IEntityManager entities, IGameTiming timing)
    {
        _entities = entities;
        _timing = timing;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.MapUid.IsValid() && _entities.HasComponent<LavalandSnowCoverComponent>(args.MapUid);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entities.TryGetComponent<LavalandSnowCoverComponent>(args.MapUid, out var cover))
            return;

        var progress = Math.Clamp((float) (_timing.CurTime - cover.Start).TotalSeconds / MathF.Max(1f, cover.GrowSeconds), 0f, 1f);
        var depth = cover.Depth * progress;
        if (depth <= 0.5f)
            return;

        var handle = args.WorldHandle;
        var half = cover.HalfSize;
        var strip = depth / Strips;

        // Thick at the border, fading inward.
        for (var i = 0; i < Strips; i++)
        {
            var outer = half - i * strip;
            var inner = outer - strip;
            var alpha = 0.55f * (1f - i / (float) Strips) + 0.05f;
            var color = cover.Color.WithAlpha(alpha * progress);

            handle.DrawRect(new Box2(-half, inner, half, outer), color);      // north
            handle.DrawRect(new Box2(-half, -outer, half, -inner), color);    // south
            handle.DrawRect(new Box2(inner, -inner, outer, inner), color);    // east
            handle.DrawRect(new Box2(-outer, -inner, -inner, inner), color);  // west
        }

        // Everything beyond the map border is fully snowed in.
        var far = half + 200f;
        var full = cover.Color.WithAlpha(0.6f * progress);
        handle.DrawRect(new Box2(-far, half, far, far), full);
        handle.DrawRect(new Box2(-far, -far, far, -half), full);
        handle.DrawRect(new Box2(half, -half, far, half), full);
        handle.DrawRect(new Box2(-far, -half, -half, half), full);
    }
}

public sealed class LavalandSnowCoverSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private LavalandSnowCoverOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new LavalandSnowCoverOverlay(EntityManager, _timing);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}
