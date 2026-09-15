using System.Numerics;
using Content.Shared.Random.Helpers;
using Robust.Shared.Timing;

namespace Content.Shared.Random;

/// <summary>
///     System containing various content-related random helpers.
/// </summary>
public sealed class RandomHelperSystem : EntitySystem
{
    // DS14-start: current engine uses readonly IoC fields.
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    // DS14-end

    // DS14-start - predicted random uses System.Random on the current engine.
    public void RandomOffset(EntityUid entity, float minX, float maxX, float minY, float maxY, System.Random? random = null)
    {
        random ??= SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(entity));

        var randomX = random.NextSingle() * (maxX - minX) + minX;
        var randomY = random.NextSingle() * (maxY - minY) + minY;
        var offset = new Vector2(randomX, randomY);

        var xform = Transform(entity);
        _transform.SetLocalPosition(entity, xform.LocalPosition + offset, xform);
    }

    public void RandomOffset(EntityUid entity, float min, float max, System.Random? random = null)
    {
        RandomOffset(entity, min, max, min, max, random);
    }

    public void RandomOffset(EntityUid entity, float value, System.Random? random = null)
    {
        RandomOffset(entity, -value, value, random);
    }
    // DS14-end
}
