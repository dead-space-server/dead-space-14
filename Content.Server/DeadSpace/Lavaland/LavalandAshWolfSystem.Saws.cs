// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Gatherable.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>The saws of the Ash Wolf: spiralling out of a landing, hunting from the sky, flying straight.</summary>
public sealed partial class LavalandAshWolfSystem
{
    private const float SpiralTurnSpeed = 3.2f;
    private const float SpiralGrowSpeed = 2.6f;
    private const float SpiralLifetime = 6.5f;
    private const float ReturnSpeed = 14f;

    private EntityUid SpawnSaw(Entity<LavalandAshWolfComponent> ent, Vector2 at, LavalandAshWolfSawKind kind, TimeSpan start, float lifetime)
    {
        var saw = Spawn(ent.Comp.Saw, new MapCoordinates(at, Transform(ent).MapID));
        var data = EnsureComp<LavalandAshWolfSawComponent>(saw);
        data.Wolf = ent;
        data.Kind = kind;
        data.Start = start;
        data.End = start + TimeSpan.FromSeconds(lifetime);
        data.Origin = at;
        return saw;
    }

    private void SpawnSpiralSaw(Entity<LavalandAshWolfComponent> ent, Vector2 at, float angle, TimeSpan now)
    {
        var saw = SpawnSaw(ent, at, LavalandAshWolfSawKind.Spiral, now, SpiralLifetime);
        var data = Comp<LavalandAshWolfSawComponent>(saw);
        data.Angle = angle;
        // Radius: starts just outside the wolf.
        data.Velocity = new Vector2(0.8f, 0f);
    }

    private EntityUid SpawnHomingSaw(Entity<LavalandAshWolfComponent> ent, Vector2 at, EntityUid player, TimeSpan now)
    {
        // It falls for half a second first.
        var saw = SpawnSaw(ent, at, LavalandAshWolfSawKind.Homing, now + TimeSpan.FromSeconds(0.5), ent.Comp.SkyDuration);
        var data = Comp<LavalandAshWolfSawComponent>(saw);
        data.Chasing = player;
        SkyMove(saw, now, 0.5f, rising: false);
        return saw;
    }

    private void SpawnStraightSaw(Entity<LavalandAshWolfComponent> ent, Vector2 at, Vector2 velocity, TimeSpan now)
    {
        var saw = SpawnSaw(ent, at, LavalandAshWolfSawKind.Straight, now, 2.8f);
        Comp<LavalandAshWolfSawComponent>(saw).Velocity = velocity;
    }

    private void DeleteSaws(EntityUid wolf)
    {
        var query = EntityQueryEnumerator<LavalandAshWolfSawComponent>();
        while (query.MoveNext(out var uid, out var saw))
        {
            if (saw.Wolf == wolf)
                QueueDel(uid);
        }
    }

    private void UpdateSaws(TimeSpan now, float frameTime)
    {
        var query = EntityQueryEnumerator<LavalandAshWolfSawComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var saw, out var xform))
        {
            if (TerminatingOrDeleted(saw.Wolf) || !TryComp<LavalandAshWolfComponent>(saw.Wolf, out var wolf))
            {
                QueueDel(uid);
                continue;
            }

            if (now < saw.Start)
                continue;

            // A sky saw has landed.
            if (HasComp<LavalandSkyDropComponent>(uid))
                RemComp<LavalandSkyDropComponent>(uid);

            if (now >= saw.End && saw.Kind != LavalandAshWolfSawKind.Returning)
            {
                QueueDel(uid);
                continue;
            }

            var wolfEnt = (saw.Wolf, wolf);
            switch (saw.Kind)
            {
                case LavalandAshWolfSawKind.Spiral:
                    UpdateSpiralSaw(uid, saw, wolfEnt, now, frameTime);
                    break;
                case LavalandAshWolfSawKind.Straight:
                    UpdateStraightSaw(uid, saw, wolfEnt, xform, frameTime);
                    break;
                case LavalandAshWolfSawKind.Homing:
                    UpdateHomingSaw(uid, saw, wolfEnt, now, frameTime);
                    break;
                case LavalandAshWolfSawKind.Returning:
                    UpdateReturningSaw(uid, saw, wolfEnt, frameTime);
                    continue;
            }

            if (!TerminatingOrDeleted(uid))
                CutRock(uid, saw.Wolf);
        }
    }

    /// <summary>Round and out; meeting someone, it sticks in them for a few cuts, then goes on.</summary>
    private void UpdateSpiralSaw(EntityUid uid, LavalandAshWolfSawComponent saw, Entity<LavalandAshWolfComponent> wolf,
        TimeSpan now, float frameTime)
    {
        if (saw.StuckIn is { } stuck)
        {
            // Stuck: its time does not run out meanwhile.
            saw.End += TimeSpan.FromSeconds(frameTime);
            if (!IsAliveTarget(stuck))
            {
                saw.StuckIn = null;
                return;
            }

            _transform.SetWorldPosition(uid, Position(stuck));
            if (now < saw.NextTick)
                return;

            _damage.TryChangeDamage(stuck, wolf.Comp.GrindDamage, origin: wolf);
            PlaySound(wolf.Comp.SawSound, stuck, -6f);
            saw.NextTick = now + TimeSpan.FromSeconds(wolf.Comp.GrindInterval);
            if (--saw.StuckTicks <= 0)
            {
                saw.Hit.Add(stuck);
                saw.StuckIn = null;
            }

            return;
        }

        saw.Angle += SpiralTurnSpeed * frameTime;
        saw.Velocity.X += SpiralGrowSpeed * frameTime;
        var position = saw.Origin + new Vector2(MathF.Cos(saw.Angle), MathF.Sin(saw.Angle)) * saw.Velocity.X;
        if (BreakOnWall(uid, position))
            return;

        _transform.SetWorldPosition(uid, position);

        foreach (var victim in GetVictims(wolf, wolf.Comp, new MapCoordinates(position, Transform(uid).MapID), SawHitRange))
        {
            if (saw.Hit.Contains(victim))
                continue;

            saw.StuckIn = victim;
            saw.StuckTicks = wolf.Comp.GrindTicks;
            saw.NextTick = now;
            break;
        }
    }

    private void UpdateStraightSaw(EntityUid uid, LavalandAshWolfSawComponent saw, Entity<LavalandAshWolfComponent> wolf,
        TransformComponent xform, float frameTime)
    {
        var position = _transform.GetWorldPosition(xform) + saw.Velocity * frameTime;
        if (BreakOnWall(uid, position))
            return;

        _transform.SetWorldPosition(uid, position);
        foreach (var victim in GetVictims(wolf, wolf.Comp, new MapCoordinates(position, xform.MapID), SawHitRange))
        {
            if (!saw.Hit.Add(victim))
                continue;

            _damage.TryChangeDamage(victim, wolf.Comp.RetreatSawDamage, origin: wolf);
            PlaySound(wolf.Comp.SawSound, victim, -4f);
        }
    }

    /// <summary>Hunts its player; on reaching them, a parry within the grace sends it back into the wolf.</summary>
    private void UpdateHomingSaw(EntityUid uid, LavalandAshWolfSawComponent saw, Entity<LavalandAshWolfComponent> wolf,
        TimeSpan now, float frameTime)
    {
        if (!IsAliveTarget(saw.Chasing))
        {
            QueueDel(uid);
            return;
        }

        var target = saw.Chasing!.Value;
        if (saw.ContactResolve is { } resolve)
        {
            _transform.SetWorldPosition(uid, Position(target));
            if (_parry.TryConsumeParry(target, wolf))
            {
                saw.ContactResolve = null;
                saw.Kind = LavalandAshWolfSawKind.Returning;
                return;
            }

            if (now < resolve)
                return;

            saw.ContactResolve = null;
            saw.NextHit = now + TimeSpan.FromSeconds(0.8);
            _damage.TryChangeDamage(target, wolf.Comp.SkySawDamage, origin: wolf);
            PlaySound(wolf.Comp.SawSound, target, -4f);
            return;
        }

        var position = Position(uid);
        var delta = Position(target) - position;
        var distance = delta.Length();
        if (distance <= SawHitRange && now >= saw.NextHit)
        {
            saw.ContactResolve = now + TimeSpan.FromSeconds(0.2f);
            return;
        }

        if (distance <= 0.01f)
            return;

        var next = position + delta / distance * MathF.Min(distance, wolf.Comp.SkySawSpeed * Haste(wolf.Comp) * frameTime);
        if (!BreakOnWall(uid, next))
            _transform.SetWorldPosition(uid, next);
    }

    /// <summary>
    /// A saw does not just go through walls. Rock and ore it cuts (see <see cref="CutRock"/>); any other wall it strikes
    /// for <see cref="LavalandAshWolfComponent.SawWallDamage"/>: broken, the saw flies on through the gap; standing,
    /// the saw shatters there and is gone, hurting no one behind it.
    /// </summary>
    private bool BreakOnWall(EntityUid saw, Vector2 position)
    {
        if (!TryComp<LavalandAshWolfSawComponent>(saw, out var sawComp) ||
            !TryComp<LavalandAshWolfComponent>(sawComp.Wolf, out var wolf))
        {
            return false;
        }

        var xform = Transform(saw);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var local = Vector2.Transform(position, _transform.GetInvWorldMatrix(gridUid));
        var tile = _map.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid, local));
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var blocker))
        {
            if (blocker is not { } blockerUid ||
                HasComp<GatherableComponent>(blockerUid) ||
                !TryComp<PhysicsComponent>(blockerUid, out var body) ||
                !body.Hard || !body.CanCollide ||
                (body.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
            {
                continue;
            }

            _damage.TryChangeDamage(blockerUid, wolf.SawWallDamage, origin: sawComp.Wolf);
            PlaySound(wolf.SawSound, blockerUid, -4f);
            if (TerminatingOrDeleted(blockerUid) || EntityManager.IsQueuedForDeletion(blockerUid))
                continue;

            QueueDel(saw);
            return true;
        }

        return false;
    }

    private void UpdateReturningSaw(EntityUid uid, LavalandAshWolfSawComponent saw, Entity<LavalandAshWolfComponent> wolf, float frameTime)
    {
        var position = Position(uid);
        var delta = Position(wolf) - position;
        var distance = delta.Length();
        if (distance <= 0.7f)
        {
            _damage.TryChangeDamage(wolf.Owner, wolf.Comp.ReflectedSawDamage, ignoreResistances: true, origin: uid);
            _popup.PopupEntity(Loc.GetString("lavaland-ash-wolf-saw-back-popup"), wolf, PopupType.Medium);
            PlaySound(wolf.Comp.SawSound, wolf);
            QueueDel(uid);
            return;
        }

        _transform.SetWorldPosition(uid, position + delta / distance * MathF.Min(distance, ReturnSpeed * frameTime));
    }

    /// <summary>Rock and ore in the way of a saw are cut as by a pickaxe.</summary>
    private void CutRock(EntityUid saw, EntityUid wolf)
    {
        var xform = Transform(saw);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var rock))
        {
            if (rock is { } rockUid && TryComp<GatherableComponent>(rockUid, out var gatherable))
                _gatherable.Gather(rockUid, wolf, gatherable);
        }
    }
}
