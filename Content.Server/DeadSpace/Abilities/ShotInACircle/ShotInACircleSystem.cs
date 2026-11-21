// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Numerics;
using Content.Shared.DeadSpace.Abilities;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.DeadSpace.Abilities.Systems;

public sealed partial class ShotInACircleSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShotInACircleActionEvent>(OnAction);
    }

    private void OnAction(ShotInACircleActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        var coords = _transform.GetMapCoordinates(performer);

        for (var i = 0; i < args.Count; i++)
        {
            var angle = Angle.FromDegrees((360f / args.Count) * i);
            var direction = angle.ToWorldVec();
            var spawnCoords = coords.Offset(direction * args.Offset);
            var spawned = Spawn(args.Entity, spawnCoords);

            if (TryComp<PhysicsComponent>(spawned, out var physics))
            {
                var impulse = direction * args.ProjectileSpeed * physics.Mass;
                _physics.ApplyLinearImpulse(spawned, impulse, body: physics);
            }
        }

        args.Handled = true;
    }
}