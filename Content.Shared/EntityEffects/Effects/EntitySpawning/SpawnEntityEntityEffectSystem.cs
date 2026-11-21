using Robust.Shared.Network;

namespace Content.Shared.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a number of entities of a given prototype at the coordinates of this entity.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpawnEntityEntityEffectSystem : EntityEffectSystem<TransformComponent, SpawnEntity>
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!; // DS14

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnEntity> args)
    {
        var quantity = args.Effect.Number * (int)Math.Floor(args.Scale);
        var proto = args.Effect.Entity;

        // DS14-start
        if (args.Effect.UseMapCoords)
        {
            var mapCoords = _transform.GetMapCoordinates(entity, entity.Comp);

            if (args.Effect.Predicted)
            {
                for (var i = 0; i < quantity; i++)
                {
                    PredictedSpawn(proto, mapCoords);
                }
            }
            else if (_net.IsServer)
            {
                for (var i = 0; i < quantity; i++)
                {
                    Spawn(proto, mapCoords);
                }
            }

            return;
        }
        // DS14-end

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                PredictedSpawnNextToOrDrop(proto, entity, entity.Comp);
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                SpawnNextToOrDrop(proto, entity, entity.Comp);
            }
        }
    }
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnEntity : BaseSpawnEntityEntityEffect<SpawnEntity>
{
    // DS14-start
    /// <summary>
    /// Spawn directly at map coordinates instead of spawning in nullspace before dropping near the target.
    /// This keeps MapInit spawners, such as EntityTableSpawner prototypes, at the intended position.
    /// </summary>
    [DataField]
    public bool UseMapCoords;
    // DS14-end
}
