// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.DeadSpace.Necromorphs.Necroobelisk.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Content.Shared.DeadSpace.Necromorphs.Sanity;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Zombies;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Necromorphs.Necroobelisk;

/// <inheritdoc cref="NecroobeliskShardComponent"/>
public sealed class NecroobeliskShardSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedSanitySystem _sanity = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _inRange = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NecroobeliskShardComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<NecroobeliskShardComponent> ent, ref ComponentShutdown args)
    {
        ClearOverlays(ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NecroobeliskShardComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shard, out var xform))
        {
            if (now < shard.NextCheck)
                continue;

            shard.NextCheck = now + shard.CheckInterval;

            // Carried or in a bag it sleeps: only loose on the floor or hidden under a tile does it work.
            if (_container.IsEntityInContainer(uid) || xform.MapUid == null)
            {
                ClearOverlays(shard);
                continue;
            }

            Pulse(shard, _transform.GetMapCoordinates(uid, xform));
        }
    }

    /// <summary>As the obelisk does: everyone in range loses sanity, and the mad are taken by the infection.</summary>
    private void Pulse(NecroobeliskShardComponent shard, MapCoordinates at)
    {
        _inRange.Clear();
        _lookup.GetEntitiesInRange(at, shard.Range, _inRange);

        foreach (var mob in shard.MobsInRange.ToArray())
        {
            if (_inRange.Contains(mob))
                continue;

            RemComp<SanityOverlayComponent>(mob);
            shard.MobsInRange.Remove(mob);
        }

        foreach (var mob in _inRange)
        {
            if (HasComp<ImmunNecroobeliskComponent>(mob) || !TryComp<SanityComponent>(mob, out var sanity))
                continue;

            _sanity.TryAddSanityLvl(mob, -shard.SanityDamage, sanity);
            if (sanity.SanityLevel > 0)
            {
                EnsureComp<SanityOverlayComponent>(mob);
                shard.MobsInRange.Add(mob);
                continue;
            }

            RemComp<SanityOverlayComponent>(mob);
            shard.MobsInRange.Remove(mob);
            GoMad(shard, mob);
        }
    }

    /// <summary>The obelisk's own: enslaved to the Circle and infected, unless already a necromorph or a zombie.</summary>
    private void GoMad(NecroobeliskShardComponent shard, EntityUid victim)
    {
        if (HasComp<NecromorfComponent>(victim) || HasComp<ZombieComponent>(victim))
            return;

        if (!HasComp<UnitologyComponent>(victim) &&
            !HasComp<UnitologyHeadComponent>(victim) &&
            !HasComp<UnitologyEnslavedComponent>(victim))
        {
            AddComp<UnitologyEnslavedComponent>(victim);
        }

        EnsureComp<InfectionDeadComponent>(victim);
        _damage.TryChangeDamage(victim, shard.MadDamage, true, false);
    }

    private void ClearOverlays(NecroobeliskShardComponent shard)
    {
        foreach (var mob in shard.MobsInRange)
        {
            if (!TerminatingOrDeleted(mob))
                RemComp<SanityOverlayComponent>(mob);
        }

        shard.MobsInRange.Clear();
    }
}
