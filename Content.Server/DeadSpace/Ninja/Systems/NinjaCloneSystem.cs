// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Humanoid;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaCloneSystem : EntitySystem
{
    private const int MaxSpawnAttempts = 20;

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NinjaCloneComponent, MeleeAttackEvent>(OnMeleeAttack);
    }

    private void OnMeleeAttack(Entity<NinjaCloneComponent> ent, ref MeleeAttackEvent args)
    {

        if (!_random.Prob(ent.Comp.CloningChance))
            return;

        var origin = Transform(ent).Coordinates;
        if (GetFreeSpawnCoords(origin, ent.Comp.MinSpawnRadius, ent.Comp.MaxSpawnRadius) is not { } coords)
            return;

        SpawnVisualClone(ent, coords, ent.Comp.CloneProto);
    }

    public EntityUid SpawnVisualClone(EntityUid source, EntityCoordinates coords, EntProtoId cloneProto)
    {
        var clone = Spawn(cloneProto, coords);

        if (TryComp<HumanoidAppearanceComponent>(source, out var sourceAppearance))
        {
            var cloneAppearance = EnsureComp<HumanoidAppearanceComponent>(clone);
            _humanoidAppearance.SetSpecies(clone, sourceAppearance.Species, sync: false);
            _humanoidAppearance.SetSex(clone, sourceAppearance.Sex, sync: false);
            _humanoidAppearance.CloneAppearance(source, clone, sourceAppearance, cloneAppearance);
        }

        _metaData.SetEntityName(clone, MetaData(source).EntityName);

        EnsureComp<NinjaCloneVisualComponent>(clone).Source = source;

        if (TryComp<TimedDespawnComponent>(source, out var timedDespawn))
        {
            EnsureComp<TimedDespawnComponent>(clone).Lifetime = timedDespawn.Lifetime;
        }

        return clone;
    }

    private EntityCoordinates? GetFreeSpawnCoords(EntityCoordinates origin, float minRadius, float maxRadius)
    {
        for (var attempt = 0; attempt < MaxSpawnAttempts; attempt++)
        {
            var offset = _random.NextVector2(minRadius, maxRadius);
            var coords = origin.Offset(offset);
            if (!_lookup.AnyEntitiesIntersecting(coords, LookupFlags.Static))
                return coords;
        }

        return null;
    }
}