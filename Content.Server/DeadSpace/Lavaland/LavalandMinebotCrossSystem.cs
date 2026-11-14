// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandMinebotCrossSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(NPCSteeringSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var bots = EntityQueryEnumerator<LavalandMinebotComponent, InputMoverComponent, TransformComponent>();
        while (bots.MoveNext(out var uid, out var minebot, out var mover, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            StepTowardOwner((uid, minebot), mover, xform);
        }
    }

    private void StepTowardOwner(Entity<LavalandMinebotComponent> bot, InputMoverComponent mover, TransformComponent xform)
    {
        if (bot.Comp.BoundUser is not { } user || TerminatingOrDeleted(user))
            return;

        var userXform = Transform(user);
        if (xform.MapID != userXform.MapID)
            return;

        if (xform.GridUid == userXform.GridUid)
        {
            if (!bot.Comp.FollowPulledToGrid)
                return;

            bot.Comp.FollowPulledToGrid = false;
            _npc.SetBlackboard(bot, NPCBlackboard.FollowTarget, new EntityCoordinates(user, Vector2.Zero));
            return;
        }

        if (xform.GridUid == null || userXform.GridUid == null)
            return;

        var botMap = _transform.GetMapCoordinates(xform);
        var userMap = _transform.GetMapCoordinates(userXform);
        var delta = userMap.Position - botMap.Position;
        var distance = delta.Length();
        if (distance < 0.2f)
            return;

        var direction = delta / distance;
        var ahead = new MapCoordinates(botMap.Position + direction * 1.1f, botMap.MapId);
        if (_mapManager.TryFindGridAt(ahead, out var aheadGrid, out _) && aheadGrid == userXform.GridUid)
        {
            var local = (-_mover.GetParentGridAngle(mover)).RotateVec(direction);
            mover.CurTickSprintMovement = local;
            mover.LastInputTick = _timing.CurTick;
            mover.LastInputSubTick = ushort.MaxValue;
            return;
        }

        if (!_mapManager.TryFindGridAt(botMap, out var ourGrid, out _) || ourGrid != xform.GridUid)
            return;

        var aimDistance = MathF.Min(distance, 6f);
        var aimWorld = new MapCoordinates(botMap.Position + direction * aimDistance, botMap.MapId);
        if (!_mapManager.TryFindGridAt(aimWorld, out var aimGrid, out _) || aimGrid != ourGrid)
            aimWorld = new MapCoordinates(botMap.Position + direction * 2f, botMap.MapId);

        var localAim = Vector2.Transform(aimWorld.Position, _transform.GetInvWorldMatrix(ourGrid));
        _npc.SetBlackboard(bot, NPCBlackboard.FollowTarget, new EntityCoordinates(ourGrid, localAim));
        bot.Comp.FollowPulledToGrid = true;
    }
}
