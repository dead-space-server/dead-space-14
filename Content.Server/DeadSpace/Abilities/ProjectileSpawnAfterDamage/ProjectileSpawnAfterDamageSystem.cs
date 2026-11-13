// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Numerics;
using Content.Shared.DeadSpace.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Damage.Systems;

public sealed partial class ProjectileSpawnAfterDamageSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileSpawnAfterDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<ProjectileSpawnAfterDamageComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (args.DamageDelta == null)
            return;

        if (_mobState.IsDead(ent))
            return;

        var totalDamage = (float) args.DamageDelta.GetTotal();

        ent.Comp.AccumulatedDamage += totalDamage;

        if (ent.Comp.AccumulatedDamage < ent.Comp.Threshold)
            return;

        ent.Comp.AccumulatedDamage = 0f;

        if (!ent.Comp.Entity.HasValue)
            return;

        var proto = ent.Comp.Entity.Value;
        var count = ent.Comp.Count;
        var coords = _transform.GetMapCoordinates(ent);

        var baseAngle = _random.NextFloat(0f, 360f);

        for (var i = 0; i < count; i++)
        {
            var angle = Angle.FromDegrees(baseAngle + (360f / count) * i);
            var direction = angle.ToWorldVec();
            var spawnCoords = coords.Offset(direction * ent.Comp.SpawnOffset);

            var spawned = Spawn(proto, spawnCoords);

            if (!TryComp<PhysicsComponent>(spawned, out var physics) || physics.Mass <= 0f)
                continue;

            var impulse = direction * ent.Comp.ProjectileSpeed * physics.Mass;
            _physics.ApplyLinearImpulse(spawned, impulse, body: physics);
        }
    }
}