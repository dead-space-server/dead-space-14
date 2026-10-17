using System.Numerics;
using Content.Server.DeadSpace.Destructible;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.DeadSpace.Destructible;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Destructible.Thresholds.Behaviors;

[DataDefinition]
public sealed partial class AirlockKnockoutBehavior : IThresholdBehavior
{
    [DataField]
    public EntProtoId LeftPieceProto { get; set; } = "AirlockPieceLeft";

    [DataField]
    public EntProtoId RightPieceProto { get; set; } = "AirlockPieceRight";

    [DataField]
    public float ImpulseForce { get; set; } = 10f;

    [DataField]
    public float SpreadDegrees { get; set; } = 20f;

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var xform = entMan.GetComponent<TransformComponent>(owner);

        if (!entMan.TryGetComponent<ExplosionKnockoutComponent>(owner, out var knockout))
            return;

        var pos = xform.WorldPosition;
        var mapId = xform.MapID;

        // Направление от эпицентра к шлюзу.
        Vector2 direction;
        if (knockout.RecentEpicenter is { } epicenter &&
            epicenter.MapId == mapId)
        {
            direction = pos - epicenter.Position;
            if (direction.LengthSquared() < 0.001f)
                direction = random.NextVector2().Normalized();
            else
                direction = direction.Normalized();
        }
        else
        {
            direction = random.NextVector2().Normalized();
        }

        var leftDir = Rotate(direction, random.NextFloat(-SpreadDegrees, SpreadDegrees));
        var rightDir = Rotate(direction, random.NextFloat(-SpreadDegrees, SpreadDegrees));

        var physics = entMan.System<SharedPhysicsSystem>();

        SpawnPiece(entMan, physics, random, LeftPieceProto, new MapCoordinates(pos - new Vector2(0.05f, 0f), mapId), leftDir, ImpulseForce);
        SpawnPiece(entMan, physics, random, RightPieceProto, new MapCoordinates(pos + new Vector2(0.05f, 0f), mapId), rightDir, ImpulseForce);

        entMan.QueueDeleteEntity(owner);
    }

    private static void SpawnPiece(
        IEntityManager entMan,
        SharedPhysicsSystem physics,
        IRobustRandom random,
        EntProtoId proto,
        MapCoordinates coords,
        Vector2 dir,
        float force)
    {
        var piece = entMan.SpawnEntity(proto, coords);
        if (!entMan.TryGetComponent<PhysicsComponent>(piece, out var body))
            return;

        physics.ApplyLinearImpulse(piece, dir * force, body: body);
        physics.ApplyAngularImpulse(piece, random.NextFloat(-1f, 1f) * force * 0.5f, body: body);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        var rad = MathHelper.DegreesToRadians(degrees);
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
