// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Physics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Alt + left click on a pipe on lavaland rides the pipe network: straight through junctions when possible,
/// stopping at walls or the end of the pipe, with sparks flying from under the rider.
/// Settings: <see cref="LavalandPipeSlideConfig"/> in the lavaland planet prototype.
/// </summary>
public sealed class LavalandPipeSlideSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly List<EntityUid> _stopping = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LavalandPipeSlideRequestEvent>(OnRequest);
    }

    private bool TryGetConfig(EntityUid uid, out LavalandPipeSlideConfig config)
    {
        config = default!;
        if (Transform(uid).MapUid is not { } map ||
            !TryComp<LavalandMapComponent>(map, out var lavaland) ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            !planet.PipeSlide.Enabled)
        {
            return false;
        }

        config = planet.PipeSlide;
        return true;
    }

    private void OnRequest(LavalandPipeSlideRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (TryComp<LavalandPipeSliderComponent>(user, out var slider))
        {
            Dismount((user, slider));
            return;
        }

        if (msg.Pipe is not { } netPipe || GetEntity(netPipe) is not { Valid: true } pipe ||
            !_mobState.IsAlive(user) || !TryGetConfig(user, out var config) ||
            GetPipeNode(pipe) == null || !Transform(pipe).Anchored)
        {
            return;
        }

        var userPos = _transform.GetWorldPosition(user);
        var pipePos = _transform.GetWorldPosition(pipe);
        if (Transform(pipe).MapID != Transform(user).MapID || Vector2.Distance(userPos, pipePos) > config.MountRange)
            return;

        // Head the way the rider is facing.
        var facing = _transform.GetWorldRotation(user).ToWorldVec();
        var comp = EnsureComp<LavalandPipeSliderComponent>(user);
        comp.Pipe = pipe;
        comp.PreviousPipe = null;
        comp.Direction = facing;
        comp.Speed = config.Speed;
        comp.NextPipe = PickNext(pipe, null, facing);

        // Lava and chasms under the pipe should not catch the rider.
        if (!HasComp<CanMoveInAirComponent>(user))
        {
            AddComp<CanMoveInAirComponent>(user);
            comp.AddedAirMovement = true;
        }
        if (!HasComp<LavalandChasmImmuneComponent>(user))
        {
            AddComp<LavalandChasmImmuneComponent>(user);
            comp.AddedChasmImmunity = true;
        }

        _transform.SetWorldPosition(user, pipePos);
        if (TryComp<PhysicsComponent>(user, out var physics))
            _physics.SetLinearVelocity(user, Vector2.Zero, body: physics);
        if (config.MountSound != null)
            _audio.PlayPvs(config.MountSound, user);
        Dirty(user, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _stopping.Clear();
        var query = EntityQueryEnumerator<LavalandPipeSliderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var slider, out var xform))
        {
            if (!TryGetConfig(uid, out var config) || !_mobState.IsAlive(uid) ||
                slider.NextPipe is not { } next || TerminatingOrDeleted(next))
            {
                _stopping.Add(uid);
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            var target = _transform.GetWorldPosition(next);
            var offset = target - position;
            var step = slider.Speed * frameTime;

            if (offset.Length() <= step)
            {
                _transform.SetWorldPosition(uid, target);
                if (offset.LengthSquared() > 0.0001f)
                    slider.Direction = Vector2.Normalize(offset);
                slider.PreviousPipe = slider.Pipe;
                slider.Pipe = next;
                slider.NextPipe = PickNext(next, slider.PreviousPipe, slider.Direction);
            }
            else
            {
                slider.Direction = Vector2.Normalize(offset);
                _transform.SetWorldPosition(uid, position + slider.Direction * step);
            }

            if (now >= slider.NextSpark)
            {
                slider.NextSpark = now + config.SparkInterval;
                Sparks(uid, config);
            }

            if (config.GrindSound != null && now >= slider.NextSound)
            {
                slider.NextSound = now + config.GrindSoundInterval;
                _audio.PlayPvs(config.GrindSound, uid);
            }
        }

        foreach (var uid in _stopping)
        {
            if (TryComp<LavalandPipeSliderComponent>(uid, out var slider))
                Dismount((uid, slider));
        }
    }

    private void Dismount(Entity<LavalandPipeSliderComponent> ent)
    {
        var direction = ent.Comp.Direction;
        var speed = TryGetConfig(ent, out var config) ? config.DismountSpeed : 0f;
        RemComp<LavalandPipeSliderComponent>(ent);
        if (speed > 0f && direction.LengthSquared() > 0.0001f && TryComp<PhysicsComponent>(ent, out var physics))
            _physics.SetLinearVelocity(ent, direction * speed, body: physics);
    }

    private PipeNode? GetPipeNode(EntityUid pipe)
    {
        if (!TryComp<NodeContainerComponent>(pipe, out var container))
            return null;

        foreach (var node in container.Nodes.Values)
        {
            if (node is PipeNode pipeNode)
                return pipeNode;
        }
        return null;
    }

    /// <summary>The connected pipe that keeps the current direction best, skipping where we came from and blocked tiles.</summary>
    private EntityUid? PickNext(EntityUid current, EntityUid? previous, Vector2 direction)
    {
        if (!TryComp<NodeContainerComponent>(current, out var container))
            return null;

        var origin = _transform.GetWorldPosition(current);
        EntityUid? best = null;
        var bestScore = float.MinValue;
        foreach (var node in container.Nodes.Values)
        {
            if (node is not PipeNode)
                continue;

            foreach (var reachable in node.ReachableNodes)
            {
                var owner = reachable.Owner;
                if (owner == current || owner == previous || reachable is not PipeNode || IsBlocked(owner))
                    continue;

                var offset = _transform.GetWorldPosition(owner) - origin;
                if (offset.LengthSquared() < 0.01f)
                    continue;

                var score = Vector2.Dot(Vector2.Normalize(offset), direction) + _random.NextFloat(0f, 0.01f);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = owner;
            }
        }

        return best;
    }

    private bool IsBlocked(EntityUid pipe)
    {
        var xform = Transform(pipe);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return true;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        return _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid);
    }

    private void Sparks(EntityUid uid, LavalandPipeSlideConfig config)
    {
        var coordinates = _transform.GetMapCoordinates(uid);
        var count = _random.Next(config.SparksMin, Math.Max(config.SparksMin, config.SparksMax) + 1);
        for (var i = 0; i < count; i++)
        {
            var spark = Spawn(config.SparkPrototype, coordinates);
            if (TryComp<PhysicsComponent>(spark, out var physics))
            {
                var speed = _random.NextFloat(config.SparkSpeedMin, Math.Max(config.SparkSpeedMin, config.SparkSpeedMax));
                _physics.SetLinearVelocity(spark, _random.NextAngle().ToVec() * speed, body: physics);
            }
        }
    }
}
