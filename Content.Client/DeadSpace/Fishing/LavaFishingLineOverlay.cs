using System.Numerics;
using Content.Shared.DeadSpace.Fishing;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Fishing;

public sealed class LavaFishingLineOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly IEntityManager _entities;
    private readonly SharedTransformSystem _transform;

    public LavaFishingLineOverlay(IEntityManager entities)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = _entities.EntityQueryEnumerator<LavaFishingRodComponent>();
        while (query.MoveNext(out var rod))
        {
            if (rod.Fisher is not { } fisher || rod.Float is not { } floatUid ||
                !_entities.TryGetComponent<TransformComponent>(fisher, out var fisherTransform) ||
                !_entities.TryGetComponent<TransformComponent>(floatUid, out var floatTransform) ||
                fisherTransform.MapID != args.MapId || floatTransform.MapID != args.MapId)
                continue;

            var from = _transform.GetWorldPosition(fisherTransform) + new Vector2(0.15f, 0.25f);
            var to = _transform.GetWorldPosition(floatTransform);
            args.WorldHandle.DrawLine(from, to, Color.FromHex("#A86C51"));
        }
    }
}
