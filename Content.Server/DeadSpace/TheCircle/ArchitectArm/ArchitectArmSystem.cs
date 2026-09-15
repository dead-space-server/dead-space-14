// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Destructible;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.DeadSpace.TheCircle.ArchitectArm;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.TheCircle.ArchitectArm;

public sealed class ArchitectArmSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier ImpactSound = new SoundCollectionSpecifier("MetalSlam");
    private static readonly ProtoId<NpcFactionPrototype> NecromorfsFaction = "Necromorfs";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ArchitectArmDashRequestEvent>(OnDashRequest);
        SubscribeLocalEvent<ArchitectArmComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<ArchitectArmComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<ActiveArchitectDashComponent, StartCollideEvent>(OnCollide);
    }

    private void OnEquippedHand(Entity<ArchitectArmComponent> ent, ref GotEquippedHandEvent args)
    {
        _alerts.ShowAlert(args.User, ent.Comp.CooldownAlert, autoRemove: false);
    }

    private void OnUnequippedHand(Entity<ArchitectArmComponent> ent, ref GotUnequippedHandEvent args)
    {
        _alerts.ClearAlert(args.User, ent.Comp.CooldownAlert);
    }

    private void OnDashRequest(ArchitectArmDashRequestEvent msg, EntitySessionEventArgs args)
    {
        var item = GetEntity(msg.Item);
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp<ArchitectArmComponent>(item, out var arm) ||
            !_hands.IsHolding(user, item) ||
            _hands.GetActiveItem(user) != item)
            return;

        if (TryComp<ActiveArchitectDashComponent>(user, out var running))
        {
            if (TryComp<PhysicsComponent>(user, out var runningPhysics))
                EndDash(user, running, runningPhysics);
            else
                RemCompDeferred<ActiveArchitectDashComponent>(user);

            return;
        }

        if (!CanDash(user) ||
            _timing.CurTime < arm.NextDash ||
            !TryComp<PhysicsComponent>(user, out _))
            return;

        var origin = _transform.GetMapCoordinates(user);
        var target = _transform.ToMapCoordinates(GetCoordinates(msg.Target));
        var offset = target.Position - origin.Position;
        if (origin.MapId != target.MapId || offset.LengthSquared() < 0.01f)
            return;

        var active = EnsureComp<ActiveArchitectDashComponent>(user);
        active.Dashing = true;
        active.Direction = offset.Normalized();
        active.Origin = origin;
        active.StartTime = _timing.CurTime + arm.DashWindup;
        active.EndTime = active.StartTime + arm.DashDuration;
        active.Speed = arm.DashSpeed;
        active.InitialSpeed = arm.DashSpeed;
        active.Range = arm.DashRange;
        active.Duration = arm.DashDuration;
        active.ObjectsPerSlowdown = arm.DashObjectsPerSlowdown;
        active.SpeedReduction = arm.DashSpeedReduction;
        active.MinimumSpeed = arm.DashMinimumSpeed;
        active.ObstacleRestartDelay = arm.DashObstacleRestartDelay;
        active.UnbreakablePrototypes = arm.DashUnbreakablePrototypes;

        arm.NextDash = _timing.CurTime + arm.DashCooldown;
        Dirty(item, arm);

        _alerts.ShowAlert(
            user,
            arm.CooldownAlert,
            cooldown: (_timing.CurTime, arm.NextDash),
            autoRemove: false);

        RaiseNetworkEvent(new ArchitectArmWindupEvent(msg.Target, arm.DashWindup), args.SenderSession);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveArchitectDashComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var active, out var physics))
        {
            var now = _timing.CurTime;

            if (active.Dashing && !CanDash(uid))
            {
                EndDash(uid, active, physics);
                continue;
            }

            if (!active.Dashing)
            {
                if (active.RestartPending)
                {
                    if (now < active.RestartTime)
                        continue;

                    if (!CanDash(uid))
                    {
                        EndDash(uid, active, physics);
                        continue;
                    }

                    var direction = Transform(uid).WorldRotation.ToWorldVec();
                    if (direction.LengthSquared() > 0.01f)
                        active.Direction = direction.Normalized();

                    active.RestartPending = false;
                    active.Dashing = true;
                    active.Origin = _transform.GetMapCoordinates(uid);
                    active.StartTime = now;
                    active.EndTime = now + active.Duration;
                    active.Speed = active.InitialSpeed;
                    active.DestroyedObjectCount = 0;
                    active.DestroyedObjects.Clear();
                }

                if (active.Captured is { } carried && Exists(carried))
                {
                    var carriedXform = Transform(carried);
                    _transform.SetLocalPosition(carried, -Vector2.UnitY * 0.45f, carriedXform);
                    _transform.SetLocalRotation(carried, Angle.Zero, carriedXform);
                }

                if (active.Captured is { } held && now >= active.ReleaseTime)
                {
                    ReleaseCaptured(uid, active, held);
                    RemCompDeferred<ActiveArchitectDashComponent>(uid);
                }

                continue;
            }

            if (now < active.StartTime)
                continue;

            SetMovementLocked(uid, active, true);

            var position = _transform.GetMapCoordinates(uid);
            if (now >= active.EndTime ||
                position.MapId != active.Origin.MapId ||
                Vector2.DistanceSquared(position.Position, active.Origin.Position) >= active.Range * active.Range)
            {
                EndDash(uid, active, physics);
                continue;
            }

            var wanted = Transform(uid).WorldRotation.ToWorldVec();
            if (wanted.LengthSquared() > 0.01f)
            {
                wanted = wanted.Normalized();
                var dot = Vector2.Dot(active.Direction, wanted);
                active.Direction = Vector2.Normalize(
                    Vector2.Lerp(active.Direction, wanted, MathF.Min(1f, frameTime * 6.8f)));

                var speed = dot < -0.5f ? active.Speed * 0.2f : active.Speed;
                _physics.SetLinearVelocity(uid, active.Direction * speed, body: physics);
            }
            else
            {
                _physics.SetLinearVelocity(uid, active.Direction * active.Speed, body: physics);
            }
        }
    }

    private bool CanDash(EntityUid user)
    {
        return TryComp<MobStateComponent>(user, out var mobState) &&
               !_mobState.IsDead(user, mobState) &&
               !_mobState.IsCritical(user, mobState) &&
               !_mobState.IsPreCritical(user, mobState) &&
               TryComp<StandingStateComponent>(user, out var standing) &&
               standing.Standing;
    }

    private void OnCollide(Entity<ActiveArchitectDashComponent> ent, ref StartCollideEvent args)
    {
        if (!ent.Comp.Dashing || _timing.CurTime < ent.Comp.StartTime)
            return;

        var other = args.OtherEntity;

        if (!HasComp<BodyComponent>(other))
        {
            _audio.PlayPvs(ImpactSound, ent.Owner);

            var otherPrototype = MetaData(other).EntityPrototype?.ID;
            if (otherPrototype != null && ent.Comp.UnbreakablePrototypes.Contains(otherPrototype))
            {
                if (TryComp<PhysicsComponent>(ent.Owner, out var obstaclePhysics))
                    EndDash(ent.Owner, ent.Comp, obstaclePhysics);
                else
                {
                    SetMovementLocked(ent.Owner, ent.Comp, false);
                    RemCompDeferred<ActiveArchitectDashComponent>(ent.Owner);
                }

                return;
            }

            _destructible.DestroyEntity(other);
            if (TerminatingOrDeleted(other) && ent.Comp.DestroyedObjects.Add(other))
            {
                ent.Comp.DestroyedObjectCount++;

                if (ent.Comp.ObjectsPerSlowdown > 0 &&
                    ent.Comp.DestroyedObjectCount % ent.Comp.ObjectsPerSlowdown == 0)
                {
                    ent.Comp.Speed = MathF.Max(
                        ent.Comp.MinimumSpeed,
                        ent.Comp.Speed - ent.Comp.SpeedReduction);
                }
            }

            return;
        }

        if (ent.Comp.Captured != null ||
            other == ent.Owner ||
            TryComp<NpcFactionMemberComponent>(other, out var faction) &&
            _factions.IsMember((other, faction), NecromorfsFaction))
            return;

        ent.Comp.Captured = other;
        ent.Comp.ReleaseTime = _timing.CurTime + TimeSpan.FromSeconds(5);
        ent.Comp.Dashing = false;

        if (TryComp<PhysicsComponent>(ent.Owner, out var physics))
            _physics.SetLinearVelocity(ent.Owner, Vector2.Zero, body: physics);

        SetMovementLocked(ent.Owner, ent.Comp, false);

        _stun.TryKnockdown(other, TimeSpan.FromSeconds(0.5), drop: false, force: true);
        _transform.SetCoordinates(other, Transform(other), new EntityCoordinates(ent.Owner, -Vector2.UnitY * 0.45f), rotation: Angle.Zero);
    }

    private void RestartDashAfterObstacle(Entity<ActiveArchitectDashComponent> ent)
    {
        if (TryComp<PhysicsComponent>(ent.Owner, out var physics))
            _physics.SetLinearVelocity(ent.Owner, Vector2.Zero, body: physics);

        SetMovementLocked(ent.Owner, ent.Comp, false);

        ent.Comp.Dashing = false;
        ent.Comp.RestartPending = true;
        ent.Comp.RestartTime = _timing.CurTime + ent.Comp.ObstacleRestartDelay;

        if (_players.TryGetSessionByEntity(ent.Owner, out var session))
        {
            RaiseNetworkEvent(
                new ArchitectArmWindupEvent(
                    GetNetCoordinates(Transform(ent.Owner).Coordinates),
                    ent.Comp.ObstacleRestartDelay),
                session);
        }
    }

    private void ReleaseCaptured(EntityUid user, ActiveArchitectDashComponent active, EntityUid captured)
    {
        if (!Exists(captured))
        {
            active.Captured = null;
            return;
        }

        var userMapCoordinates = _transform.GetMapCoordinates(user);
        var capturedMapCoordinates = _transform.GetMapCoordinates(captured);
        var direction = capturedMapCoordinates.MapId == userMapCoordinates.MapId
            ? capturedMapCoordinates.Position - userMapCoordinates.Position
            : -Transform(user).WorldRotation.ToWorldVec();

        if (direction.LengthSquared() <= 0.01f)
            direction = -Transform(user).WorldRotation.ToWorldVec();
        else
            direction = direction.Normalized();

        var userCoordinates = Transform(user).Coordinates;
        _transform.SetCoordinates(captured, Transform(captured), userCoordinates);

        _throwing.TryThrow(
            captured,
            direction * 5f,
            10f,
            animated: true,
            user: user);

        _stun.TryKnockdown(captured, TimeSpan.FromSeconds(0.5), drop: false, force: true);
        active.Captured = null;
    }

    private void SetMovementLocked(EntityUid uid, ActiveArchitectDashComponent active, bool locked)
    {
        if (active.MovementLocked == locked)
            return;

        if (!TryComp<InputMoverComponent>(uid, out var mover))
        {
            active.MovementLocked = locked;
            return;
        }

        if (locked)
        {
            active.OldCanMove = mover.CanMove;
            mover.CanMove = false;
        }
        else
        {
            mover.CanMove = active.OldCanMove;
        }

        active.MovementLocked = locked;
        Dirty(uid, mover);
    }

    private void EndDash(EntityUid uid, ActiveArchitectDashComponent active, PhysicsComponent physics)
    {
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

        if (active.Captured is { } captured)
            ReleaseCaptured(uid, active, captured);

        SetMovementLocked(uid, active, false);
        RemCompDeferred<ActiveArchitectDashComponent>(uid);
    }
}

[RegisterComponent]
public sealed partial class ActiveArchitectDashComponent : Component
{
    public EntityUid? Captured;
    public bool Dashing;
    public bool RestartPending;
    public MapCoordinates Origin;
    public Vector2 Direction;
    public float Speed;
    public float InitialSpeed;
    public float Range;
    public TimeSpan Duration;
    public int ObjectsPerSlowdown;
    public float SpeedReduction;
    public float MinimumSpeed;
    public TimeSpan ObstacleRestartDelay;
    public HashSet<EntProtoId> UnbreakablePrototypes = new();
    public int DestroyedObjectCount;
    public readonly HashSet<EntityUid> DestroyedObjects = new();
    public bool OldCanMove = true;
    public bool MovementLocked;
    public TimeSpan StartTime;
    public TimeSpan EndTime;
    public TimeSpan RestartTime;
    public TimeSpan ReleaseTime;
}
