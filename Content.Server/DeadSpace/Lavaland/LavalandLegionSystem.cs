using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandLegionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandLegionComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<LavalandLegionComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<LavalandLegionComponent, ComponentShutdown>(OnLegionShutdown);
        SubscribeLocalEvent<LavalandLegionHeadComponent, MeleeHitEvent>(OnHeadMeleeHit);
        SubscribeLocalEvent<LavalandLegionHeadComponent, ComponentShutdown>(OnHeadShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandLegionHeadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var head, out var xform))
        {
            if (head.NextInfestCheck > curTime)
                continue;

            head.NextInfestCheck = curTime + head.InfestCheckInterval;

            if (IsDead(uid))
                continue;

            TryInfestNearby((uid, head), xform);
        }
    }

    private void OnAttemptShoot(Entity<LavalandLegionComponent> ent, ref AttemptShootEvent args)
    {
        PruneHeads(ent);

        if (ent.Comp.ActiveHeads.Count < ent.Comp.MaxActiveHeads)
            return;

        args.Cancelled = true;
    }

    private void OnGunShot(Entity<LavalandLegionComponent> ent, ref GunShotEvent args)
    {
        PruneHeads(ent);

        foreach (var (ammo, _) in args.Ammo)
        {
            if (ammo is not { } head ||
                !TryComp<LavalandLegionHeadComponent>(head, out var headComp))
            {
                continue;
            }

            headComp.Source = ent.Owner;
            ent.Comp.ActiveHeads.Add(head);
        }
    }

    private void OnLegionShutdown(Entity<LavalandLegionComponent> ent, ref ComponentShutdown args)
    {
        foreach (var head in ent.Comp.ActiveHeads)
        {
            if (TryComp<LavalandLegionHeadComponent>(head, out var headComp) &&
                headComp.Source == ent.Owner)
            {
                headComp.Source = null;
            }
        }

        ent.Comp.ActiveHeads.Clear();
    }

    private void OnHeadMeleeHit(Entity<LavalandLegionHeadComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || IsDead(ent.Owner))
            return;

        foreach (var target in args.HitEntities)
        {
            if (TryInfestTarget(ent, target))
                return;
        }
    }

    private void OnHeadShutdown(Entity<LavalandLegionHeadComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Source is not { } source ||
            !TryComp<LavalandLegionComponent>(source, out var legion))
        {
            return;
        }

        legion.ActiveHeads.Remove(ent.Owner);
    }

    private void TryInfestNearby(Entity<LavalandLegionHeadComponent> head, TransformComponent xform)
    {
        var coordinates = _transform.GetMapCoordinates(head.Owner, xform);
        foreach (var (target, _) in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(coordinates, head.Comp.InfestRange))
        {
            if (TryInfestTarget(head, target))
                return;
        }
    }

    private bool TryInfestTarget(Entity<LavalandLegionHeadComponent> head, EntityUid target)
    {
        if (target == head.Owner ||
            HasComp<LavalandLegionInfestedComponent>(target) ||
            !HasComp<HumanoidAppearanceComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var mobState) ||
            !_mobState.IsDead(target, mobState))
        {
            return false;
        }

        var targetXform = Transform(target);
        var targetCoordinates = _transform.GetMapCoordinates(target, targetXform);
        if (targetCoordinates.MapId == MapId.Nullspace)
            return false;

        EnsureComp<LavalandLegionInfestedComponent>(target);
        var legion = Spawn(head.Comp.InfestPrototype, targetCoordinates, rotation: _transform.GetWorldRotation(targetXform));
        EnsureComp<LavalandLegionInfestedComponent>(legion);
        QueueDel(target);
        QueueDel(head.Owner);

        return true;
    }

    private void PruneHeads(Entity<LavalandLegionComponent> ent)
    {
        ent.Comp.ActiveHeads.RemoveWhere(head => !Exists(head) || IsDead(head));
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var mobState) && _mobState.IsDead(uid, mobState);
    }
}
