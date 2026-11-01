using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Conveyor;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Prometheus;
using Robust.Shared.Player;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;
using DroneConsoleComponent = Content.Server.Shuttles.DroneConsoleComponent;

namespace Content.Server.Physics.Controllers;

public sealed class MoverController : SharedMoverController
{
    private static readonly Gauge ActiveMoverGauge = Metrics.CreateGauge(
        "physics_active_mover_count",
        "Amount of ActiveInputMovers being processed by MoverController");

    [Dependency] private readonly ThrusterSystem _thruster = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!; //DS-14

    private Dictionary<EntityUid, (ShuttleComponent, List<(EntityUid, PilotComponent, InputMoverComponent, TransformComponent)>)> _shuttlePilots = new();

    private EntityQuery<ActiveInputMoverComponent> _activeQuery;
    private EntityQuery<ActiveNPCComponent> _activeNpcQuery;
    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<ConveyedComponent> _conveyedQuery;
    private EntityQuery<DroneConsoleComponent> _droneQuery;
    private EntityQuery<HTNComponent> _htnQuery;
    private EntityQuery<ShuttleComponent> _shuttleQuery;
    private EntityQuery<ZombieBlobComponent> _zombieBlobQuery;

    // Not needed for persistence; just used to save an alloc
    private readonly HashSet<EntityUid> _seenMovers = [];
    private readonly HashSet<EntityUid> _seenRelayMovers = [];
    private readonly HashSet<Entity<DockingComponent>> _shuttleDockSet = [];
    private readonly List<Entity<InputMoverComponent>> _moversToUpdate = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveInputMoverComponent, EntityPausedEvent>(OnEntityPaused);
        SubscribeLocalEvent<InputMoverComponent, EntityUnpausedEvent>(OnEntityUnpaused);
        SubscribeLocalEvent<ActiveNPCComponent, ComponentStartup>(OnActiveNpcStartup);
        SubscribeLocalEvent<ActiveNPCComponent, ComponentShutdown>(OnActiveNpcShutdown);

        SubscribeLocalEvent<RelayInputMoverComponent, PlayerAttachedEvent>(OnRelayPlayerAttached);
        SubscribeLocalEvent<RelayInputMoverComponent, PlayerDetachedEvent>(OnRelayPlayerDetached);
        SubscribeLocalEvent<InputMoverComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<InputMoverComponent, PlayerDetachedEvent>(OnPlayerDetached);

        _activeQuery = GetEntityQuery<ActiveInputMoverComponent>();
        _activeNpcQuery = GetEntityQuery<ActiveNPCComponent>();
        _actorQuery = GetEntityQuery<ActorComponent>();
        _conveyedQuery = GetEntityQuery<ConveyedComponent>();
        _droneQuery = GetEntityQuery<DroneConsoleComponent>();
        _htnQuery = GetEntityQuery<HTNComponent>();
        _shuttleQuery = GetEntityQuery<ShuttleComponent>();
        _zombieBlobQuery = GetEntityQuery<ZombieBlobComponent>();
    }

    private void OnEntityPaused(Entity<ActiveInputMoverComponent> ent, ref EntityPausedEvent args)
    {
        // Become unactive [sic] if we don't have PhysicsComp.IgnorePaused
        if (PhysicsQuery.TryComp(ent, out var phys) && phys.IgnorePaused)
            return;
        RemCompDeferred<ActiveInputMoverComponent>(ent);
    }

    private void OnEntityUnpaused(Entity<InputMoverComponent> ent, ref EntityUnpausedEvent args)
    {
        UpdateMoverStatus((ent, ent.Comp));
    }

    private void OnActiveNpcStartup(Entity<ActiveNPCComponent> ent, ref ComponentStartup args)
    {
        if (MoverQuery.TryComp(ent, out var mover))
            UpdateMoverStatus((ent.Owner, mover, null));
    }

    private void OnActiveNpcShutdown(Entity<ActiveNPCComponent> ent, ref ComponentShutdown args)
    {
        if (MoverQuery.TryComp(ent, out var mover))
            UpdateMoverStatus((ent.Owner, mover, null));
    }

    protected override void OnMoverStartup(Entity<InputMoverComponent> ent, ref ComponentStartup args)
    {
        base.OnMoverStartup(ent, ref args);
        UpdateMoverStatus((ent, ent.Comp));
    }

    protected override void OnTargetRelayShutdown(Entity<MovementRelayTargetComponent> ent, ref ComponentShutdown args)
    {
        base.OnTargetRelayShutdown(ent, ref args);
        UpdateMoverStatus((ent, null, ent.Comp));
    }

    protected override void UpdateMoverStatus(Entity<InputMoverComponent?, MovementRelayTargetComponent?> ent)
    {
        // Track that we aren't in a loop of movement relayers
        _seenMovers.Clear();
        while (true)
        {
            if (!MoverQuery.Resolve(ent, ref ent.Comp1, logMissing: false))
            {
                RemCompDeferred<ActiveInputMoverComponent>(ent);
                break;
            }

            var meta = MetaData(ent);
            if (Terminating(ent, meta))
                break;

            if (CanDeactivateDormantNpcMover((ent.Owner, ent.Comp1), ent.Comp2))
            {
                SetWishDir((ent.Owner, ent.Comp1), Vector2.Zero);
                RemCompDeferred<ActiveInputMoverComponent>(ent);
                break;
            }

            ActiveInputMoverComponent? activeMover = null;
            if (!meta.EntityPaused
                || PhysicsQuery.TryComp(ent, out var phys) && phys.IgnorePaused)
                activeMover = EnsureComp<ActiveInputMoverComponent>(ent);

            // If we're a relay target, make sure our drivers are InputMovers
            if (RelayTargetQuery.Resolve(ent, ref ent.Comp2, logMissing: false)
                // In case we're called from ComponentShutdown:
                && ent.Comp2.LifeStage <= ComponentLifeStage.Running
                && Exists(ent.Comp2.Source)
                && !_seenMovers.Contains(ent.Comp2.Source))
            {
                if (ent.Comp2.Source == ent.Owner)
                {
                    Log.Error($"Entity {ToPrettyString(ent)} is attempting to relay movement to itself!");
                    break;
                }

                if (activeMover is not null)
                    activeMover.RelayedFrom = ent.Comp2.Source;

                ent = ent.Comp2.Source;
                _seenMovers.Add(ent);
                continue;
            }

            // No longer a well-defined relay target
            if (activeMover is not null)
                activeMover.RelayedFrom = null;

            break;
        }
    }

    private void OnRelayPlayerAttached(Entity<RelayInputMoverComponent> entity, ref PlayerAttachedEvent args)
    {
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnRelayPlayerDetached(Entity<RelayInputMoverComponent> entity, ref PlayerDetachedEvent args)
    {
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnPlayerAttached(Entity<InputMoverComponent> entity, ref PlayerAttachedEvent args)
    {
        SetMoveInput(entity, MoveButtons.None);
        UpdateMoverStatus((entity, entity.Comp));
    }

    private void OnPlayerDetached(Entity<InputMoverComponent> entity, ref PlayerDetachedEvent args)
    {
        SetMoveInput(entity, MoveButtons.None);
        UpdateMoverStatus((entity, entity.Comp));
    }

    protected override bool CanSound()
    {
        return true;
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        // We use _seenMovers here as well as in UpdateMoverStatus—this means we
        // cannot have any events get fired while we use it in this while loop.
        _seenMovers.Clear();
        _moversToUpdate.Clear();

        // Don't use EntityQueryEnumerator because admin ghosts have to move on
        // paused maps. Pausing movers is handled via ActiveInputMoverComponent.
        var inputQueryEnumerator = AllEntityQuery<ActiveInputMoverComponent, InputMoverComponent>();
        while (inputQueryEnumerator.MoveNext(out var uid, out var activeComp, out var moverComp))
        {
            if (CanDeactivateDormantNpcMover((uid, moverComp), null, activeComp))
            {
                SetWishDir((uid, moverComp), Vector2.Zero);
                RemCompDeferred<ActiveInputMoverComponent>(uid);
                continue;
            }

            if (CanSkipIdleMover((uid, moverComp), activeComp))
                continue;

            if (activeComp.RelayedFrom != null)
            {
                _seenRelayMovers.Clear(); // O(1) if already empty
                QueueRelaySources(activeComp.RelayedFrom);
            }

            // If it's already inserted, that's fine—that means it'll still be
            // handled before its child movers
            AddMover((uid, moverComp));
        }

        ActiveMoverGauge.Set(_moversToUpdate.Count);

        foreach (var ent in _moversToUpdate)
        {
            HandleMobMovement(ent, frameTime);
        }

        HandleShuttleMovement(frameTime);
        return;

        // When we insert a chain of relay sources we have to flip its ordering
        // It's going to be extremely uncommon for a relay chain to be more than
        // one entity so we just recurse as needed.
        void QueueRelaySources(EntityUid? next)
        {
            if (next == null)
                return;

            var nextUid = next.Value;

            // We only care if it's still a mover
            if (!_activeQuery.TryComp(nextUid, out var nextActive)
                || !MoverQuery.TryComp(nextUid, out var nextMover)
                || !_seenRelayMovers.Add(nextUid))
                return;

            Debug.Assert(nextUid != nextActive.RelayedFrom);

            // While it is (as of writing) currently true that this recursion
            // should always terminate due to RelayedFrom always being written
            // in a way that tracks if it's made a loop, we still take the extra
            // memory (and small time cost) of making sure via _seenRelayMovers.
            QueueRelaySources(nextActive.RelayedFrom);
            AddMover((nextUid, nextMover));
        }

        // Track inserts so we have ~ O(1) inserts without duplicates. Hopefully
        // it doesn't matter that both _seenMovers and _moversToUpdate are never
        // trimmed? They should be pretty memory light anyway, and in general
        // it'll be rare for there to be a decrease in movers.
        void AddMover(Entity<InputMoverComponent> entity)
        {
            if (!_seenMovers.Add(entity))
                return;

            _moversToUpdate.Add(entity);
        }
    }

    // DS14-Start: keep dormant NPCs out of the mover hot path once they have fully stopped.
    private bool CanDeactivateDormantNpcMover(
        Entity<InputMoverComponent> entity,
        MovementRelayTargetComponent? relayTarget,
        ActiveInputMoverComponent? activeMover = null)
    {
        return IsDormantHtnNpcMover(entity.Owner, relayTarget, activeMover) &&
               IsFullyIdleMover(entity, activeMover);
    }

    private bool CanSkipIdleMover(Entity<InputMoverComponent> entity, ActiveInputMoverComponent activeMover)
    {
        if (!IsFullyIdleMover(entity, activeMover))
            return false;

        // Players and relay chains need to stay in the full path for prediction and relay state propagation.
        if (_actorQuery.HasComp(entity) ||
            RelayQuery.HasComp(entity) ||
            HasRunningRelayTarget(entity.Owner, null) ||
            activeMover.RelayedFrom != null)
        {
            return false;
        }

        SetWishDir(entity, Vector2.Zero);
        return true;
    }

    private bool IsDormantHtnNpcMover(
        EntityUid uid,
        MovementRelayTargetComponent? relayTarget,
        ActiveInputMoverComponent? activeMover)
    {
        if (!_htnQuery.HasComp(uid) ||
            HasRunningActiveNpc(uid) ||
            _actorQuery.HasComp(uid) ||
            _conveyedQuery.HasComp(uid) ||
            RelayQuery.HasComp(uid) ||
            HasRunningRelayTarget(uid, relayTarget) ||
            _zombieBlobQuery.HasComp(uid) ||
            activeMover?.RelayedFrom != null)
        {
            return false;
        }

        return true;
    }

    private bool IsFullyIdleMover(Entity<InputMoverComponent> entity, ActiveInputMoverComponent? activeMover)
    {
        var mover = entity.Comp;

        if (mover.HasDirectionalMovement ||
            Timing.CurTick <= mover.LastInputTick ||
            _conveyedQuery.HasComp(entity) ||
            activeMover?.RelayedFrom != null ||
            HasPendingRelativeMovement(entity.Owner, mover) ||
            HasPendingRotation(mover))
        {
            return false;
        }

        if (!PhysicsQuery.TryComp(entity, out var physics))
            return true;

        return physics.LinearVelocity.Equals(Vector2.Zero) &&
               physics.AngularVelocity.Equals(0f);
    }

    private bool HasRunningActiveNpc(EntityUid uid)
    {
        return _activeNpcQuery.TryComp(uid, out var activeNpc) &&
               activeNpc.LifeStage <= ComponentLifeStage.Running;
    }

    private bool HasRunningRelayTarget(EntityUid uid, MovementRelayTargetComponent? relayTarget)
    {
        return RelayTargetQuery.Resolve(uid, ref relayTarget, logMissing: false) &&
               relayTarget.LifeStage <= ComponentLifeStage.Running;
    }

    private bool HasPendingRelativeMovement(EntityUid uid, InputMoverComponent mover)
    {
        if (mover.LerpTarget == TimeSpan.Zero)
            return false;

        if (mover.LerpTarget >= Timing.CurTime)
            return true;

        if (!XformQuery.TryComp(uid, out var xform))
            return false;

        var relative = xform.GridUid ?? xform.MapUid;
        return mover.RelativeEntity != relative;
    }

    private static bool HasPendingRotation(InputMoverComponent mover)
    {
        return !Angle.ShortestDistance(mover.RelativeRotation, mover.TargetRelativeRotation)
            .EqualsApprox(Angle.Zero, 0.001);
    }
    // DS14-End

    public (Vector2 Strafe, float Rotation, float Brakes) GetPilotVelocityInput(PilotComponent component)
    {
        if (!Timing.InSimulation)
        {
            // Outside of simulation we'll be running client predicted movement per-frame.
            // So return a full-length vector as if it's a full tick.
            // Physics system will have the correct time step anyways.
            ResetSubtick(component);
            ApplyTick(component, 1f);
            return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
        }

        float remainingFraction;

        if (Timing.CurTick > component.LastInputTick)
        {
            component.CurTickStrafeMovement = Vector2.Zero;
            component.CurTickRotationMovement = 0f;
            component.CurTickBraking = 0f;
            remainingFraction = 1;
        }
        else
        {
            remainingFraction = (ushort.MaxValue - component.LastInputSubTick) / (float) ushort.MaxValue;
        }

        ApplyTick(component, remainingFraction);

        // Logger.Info($"{curDir}{walk}{sprint}");
        return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
    }

    private void ResetSubtick(PilotComponent component)
    {
        if (Timing.CurTick <= component.LastInputTick) return;

        component.CurTickStrafeMovement = Vector2.Zero;
        component.CurTickRotationMovement = 0f;
        component.CurTickBraking = 0f;
        component.LastInputTick = Timing.CurTick;
        component.LastInputSubTick = 0;
    }

    protected override void HandleShuttleInput(EntityUid uid, ShuttleButtons button, ushort subTick, bool state)
    {
        if (!PilotQuery.TryComp(uid, out var pilot) || pilot.Console == null)
            return;

        ResetSubtick(pilot);

        if (subTick >= pilot.LastInputSubTick)
        {
            var fraction = (subTick - pilot.LastInputSubTick) / (float) ushort.MaxValue;

            ApplyTick(pilot, fraction);
            pilot.LastInputSubTick = subTick;
        }

        var buttons = pilot.HeldButtons;

        if (state)
        {
            buttons |= button;
        }
        else
        {
            buttons &= ~button;
        }

        pilot.HeldButtons = buttons;
        if (state) //DS-14
            _shuttleConsole.NoteShuttleControl(uid, pilot.Console.Value); //DS-14
    }

    private static void ApplyTick(PilotComponent component, float fraction)
    {
        var x = 0;
        var y = 0;
        var rot = 0;
        int brake;

        if ((component.HeldButtons & ShuttleButtons.StrafeLeft) != 0x0)
        {
            x -= 1;
        }

        if ((component.HeldButtons & ShuttleButtons.StrafeRight) != 0x0)
        {
            x += 1;
        }

        component.CurTickStrafeMovement.X += x * fraction;

        if ((component.HeldButtons & ShuttleButtons.StrafeUp) != 0x0)
        {
            y += 1;
        }

        if ((component.HeldButtons & ShuttleButtons.StrafeDown) != 0x0)
        {
            y -= 1;
        }

        component.CurTickStrafeMovement.Y += y * fraction;

        if ((component.HeldButtons & ShuttleButtons.RotateLeft) != 0x0)
        {
            rot -= 1;
        }

        if ((component.HeldButtons & ShuttleButtons.RotateRight) != 0x0)
        {
            rot += 1;
        }

        component.CurTickRotationMovement += rot * fraction;

        if ((component.HeldButtons & ShuttleButtons.Brake) != 0x0)
        {
            brake = 1;
        }
        else
        {
            brake = 0;
        }

        component.CurTickBraking += brake * fraction;
    }

    /// <summary>
    /// Helper function to extrapolate max velocity for a given Vector2 (really, its angle) and shuttle.
    /// </summary>
    private Vector2 ObtainMaxVel(Vector2 vel, ShuttleComponent shuttle)
    {
        if (vel.Length() == 0f)
            return Vector2.Zero;

        // this math could PROBABLY be simplified for performance
        // probably
        //             __________________________________
        //            / /    __   __ \2   /    __   __ \2
        // O = I : _ /  |I * | 1/H | |  + |I * |  0  | |
        //          V   \    |_ 0 _| /    \    |_1/V_| /

        var horizIndex = vel.X > 0 ? 1 : 3; // east else west
        var vertIndex = vel.Y > 0 ? 2 : 0; // north else south
        var horizComp = vel.X != 0 ? MathF.Pow(Vector2.Dot(vel, new (shuttle.LinearThrust[horizIndex] / shuttle.LinearThrust[horizIndex], 0f)), 2) : 0;
        var vertComp = vel.Y != 0 ? MathF.Pow(Vector2.Dot(vel, new (0f, shuttle.LinearThrust[vertIndex] / shuttle.LinearThrust[vertIndex])), 2) : 0;

        return shuttle.BaseMaxLinearVelocity * vel * MathF.ReciprocalSqrtEstimate(horizComp + vertComp);
    }

    private void HandleShuttleMovement(float frameTime)
    {
        var newPilots = new Dictionary<EntityUid, (ShuttleComponent Shuttle, List<(EntityUid PilotUid, PilotComponent Pilot, InputMoverComponent Mover, TransformComponent ConsoleXform)>)>();

        // We just mark off their movement and the shuttle itself does its own movement
        var activePilotQuery = EntityQueryEnumerator<PilotComponent, InputMoverComponent>();
        while (activePilotQuery.MoveNext(out var uid, out var pilot, out var mover))
        {
            var consoleEnt = pilot.Console;

            // TODO: This is terrible. Just make a new mover and also make it remote piloting + device networks
            if (_droneQuery.TryComp(consoleEnt, out var cargoConsole))
                consoleEnt = cargoConsole.Entity;

            if (!XformQuery.TryComp(consoleEnt, out var xform))
                continue;

            var gridId = xform.GridUid;
            // This tries to see if the grid is a shuttle and if the console should work.
            if (!MapGridQuery.HasComp(gridId) ||
                !_shuttleQuery.TryGetComponent(gridId, out var shuttleComponent) ||
                !shuttleComponent.Enabled)
                continue;

            if (!newPilots.TryGetValue(gridId.Value, out var pilots))
            {
                pilots = (shuttleComponent, new List<(EntityUid, PilotComponent, InputMoverComponent, TransformComponent)>());
                newPilots[gridId.Value] = pilots;
            }

            pilots.Item2.Add((uid, pilot, mover, xform));
        }

        // Reset inputs for non-piloted shuttles.
        foreach (var (shuttleUid, (shuttle, _)) in _shuttlePilots)
        {
            if (newPilots.ContainsKey(shuttleUid) || CanPilot(shuttleUid))
                continue;

            _thruster.DisableLinearThrusters(shuttle);
        }

        _shuttlePilots = newPilots;

        // Collate all of the linear / angular velocites for a shuttle
        // then do the movement input once for it.
        foreach (var (shuttleUid, (shuttle, pilots)) in _shuttlePilots)
        {
            if (Paused(shuttleUid) || CanPilot(shuttleUid) || !PhysicsQuery.TryComp(shuttleUid, out var body))
                continue;

            var shuttleNorthAngle = TransformSystem.GetWorldRotation(shuttleUid, XformQuery);

            // Collate movement linear and angular inputs together
            var linearInput = Vector2.Zero;
            var brakeInput = 0f;
            var angularInput = 0f;
            var linearCount = 0;
            var brakeCount = 0;
            var angularCount = 0;

            foreach (var (_, pilot, _, consoleXform) in pilots)
            {
                var (strafe, rotation, brakes) = GetPilotVelocityInput(pilot);

                if (brakes > 0f)
                {
                    brakeInput += brakes;
                    brakeCount++;
                }

                if (strafe.Length() > 0f)
                {
                    var offsetRotation = consoleXform.LocalRotation;
                    linearInput += offsetRotation.RotateVec(strafe);
                    linearCount++;
                }

                if (rotation != 0f)
                {
                    angularInput += rotation;
                    angularCount++;
                }
            }

            // Don't slow down the shuttle if there's someone just looking at the console
            linearInput /= Math.Max(1, linearCount);
            angularInput /= Math.Max(1, angularCount);
            brakeInput /= Math.Max(1, brakeCount);

            // Handle shuttle movement
            if (brakeInput > 0f)
            {
                if (body.LinearVelocity.Length() > 0f)
                {
                    // Minimum brake velocity for a direction to show its thrust appearance.
                    const float appearanceThreshold = 0.1f;

                    // Get velocity relative to the shuttle so we know which thrusters to fire
                    var shuttleVelocity = (-shuttleNorthAngle).RotateVec(body.LinearVelocity);
                    var force = Vector2.Zero;

                    if (shuttleVelocity.X < 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.West);

                        if (shuttleVelocity.X < -appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.East);

                        var index = (int) Math.Log2((int) DirectionFlag.East);
                        force.X += shuttle.LinearThrust[index];
                    }
                    else if (shuttleVelocity.X > 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.East);

                        if (shuttleVelocity.X > appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.West);

                        var index = (int) Math.Log2((int) DirectionFlag.West);
                        force.X -= shuttle.LinearThrust[index];
                    }

                    if (shuttleVelocity.Y < 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.South);

                        if (shuttleVelocity.Y < -appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.North);

                        var index = (int) Math.Log2((int) DirectionFlag.North);
                        force.Y += shuttle.LinearThrust[index];
                    }
                    else if (shuttleVelocity.Y > 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.North);

                        if (shuttleVelocity.Y > appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.South);

                        var index = (int) Math.Log2((int) DirectionFlag.South);
                        force.Y -= shuttle.LinearThrust[index];
                    }

                    var impulse = force * brakeInput * ShuttleComponent.BrakeCoefficient;
                    impulse = shuttleNorthAngle.RotateVec(impulse);
                    var forceMul = frameTime * body.InvMass;
                    var maxVelocity = (-body.LinearVelocity).Length() / forceMul;

                    // Don't overshoot
                    if (impulse.Length() > maxVelocity)
                        impulse = impulse.Normalized() * maxVelocity;

                    PhysicsSystem.ApplyForce(shuttleUid, impulse, body: body);
                }
                else
                {
                    _thruster.DisableLinearThrusters(shuttle);
                }

                if (body.AngularVelocity != 0f)
                {
                    var torque = shuttle.AngularThrust * brakeInput * (body.AngularVelocity > 0f ? -1f : 1f) * ShuttleComponent.BrakeCoefficient;
                    var torqueMul = body.InvI * frameTime;

                    if (body.AngularVelocity > 0f)
                    {
                        torque = MathF.Max(-body.AngularVelocity / torqueMul, torque);
                    }
                    else
                    {
                        torque = MathF.Min(-body.AngularVelocity / torqueMul, torque);
                    }

                    if (!torque.Equals(0f))
                    {
                        PhysicsSystem.ApplyTorque(shuttleUid, torque, body: body);
                        _thruster.SetAngularThrust(shuttle, true);
                    }
                }
                else
                {
                    _thruster.SetAngularThrust(shuttle, false);
                }
            }

            if (linearInput.Length().Equals(0f))
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, true);

                if (brakeInput.Equals(0f))
                    _thruster.DisableLinearThrusters(shuttle);
            }
            else
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, false);
                var angle = linearInput.ToWorldAngle();
                var linearDir = angle.GetDir();
                var dockFlag = linearDir.AsFlag();
                var totalForce = Vector2.Zero;

                // Won't just do cardinal directions.
                foreach (DirectionFlag dir in Enum.GetValues(typeof(DirectionFlag)))
                {
                    // Brain no worky but I just want cardinals
                    switch (dir)
                    {
                        case DirectionFlag.South:
                        case DirectionFlag.East:
                        case DirectionFlag.North:
                        case DirectionFlag.West:
                            break;
                        default:
                            continue;
                    }

                    if ((dir & dockFlag) == 0x0)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, dir);
                        continue;
                    }

                    var force = Vector2.Zero;
                    var index = (int) Math.Log2((int) dir);
                    var thrust = shuttle.LinearThrust[index];

                    switch (dir)
                    {
                        case DirectionFlag.North:
                            force.Y += thrust;
                            break;
                        case DirectionFlag.South:
                            force.Y -= thrust;
                            break;
                        case DirectionFlag.East:
                            force.X += thrust;
                            break;
                        case DirectionFlag.West:
                            force.X -= thrust;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Attempted to apply thrust to shuttle {shuttleUid} along invalid dir {dir}.");
                    }

                    _thruster.EnableLinearThrustDirection(shuttle, dir);
                    var impulse = force * linearInput.Length();
                    totalForce += impulse;
                }

                var forceMul = frameTime * body.InvMass;

                var localVel = (-shuttleNorthAngle).RotateVec(body.LinearVelocity);
                var maxVelocity = ObtainMaxVel(localVel, shuttle); // max for current travel dir
                var maxWishVelocity = ObtainMaxVel(totalForce, shuttle);
                var properAccel = (maxWishVelocity - localVel) / forceMul;

                var finalForce = Vector2Dot(totalForce, properAccel.Normalized()) * properAccel.Normalized();

                if (localVel.Length() >= maxVelocity.Length() && Vector2.Dot(totalForce, localVel) > 0f)
                    finalForce = Vector2.Zero; // burn would be faster if used as such

                if (finalForce.Length() > properAccel.Length())
                    finalForce = properAccel; // don't overshoot

                //Log.Info($"shuttle: maxVelocity {maxVelocity} totalForce {totalForce} finalForce {finalForce} forceMul {forceMul} properAccel {properAccel}");

                finalForce = shuttleNorthAngle.RotateVec(finalForce);

                if (finalForce.Length() > 0f)
                    PhysicsSystem.ApplyForce(shuttleUid, finalForce, body: body);
            }

            if (MathHelper.CloseTo(angularInput, 0f))
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, true);

                if (brakeInput <= 0f)
                    _thruster.SetAngularThrust(shuttle, false);
            }
            else
            {
                PhysicsSystem.SetSleepingAllowed(shuttleUid, body, false);
                var torque = shuttle.AngularThrust * -angularInput;

                // Need to cap the velocity if 1 tick of input brings us over cap so we don't continuously
                // edge onto the cap over and over.
                var torqueMul = body.InvI * frameTime;

                torque = Math.Clamp(torque,
                    (-ShuttleComponent.MaxAngularVelocity - body.AngularVelocity) / torqueMul,
                    (ShuttleComponent.MaxAngularVelocity - body.AngularVelocity) / torqueMul);

                if (!torque.Equals(0f))
                {
                    PhysicsSystem.ApplyTorque(shuttleUid, torque, body: body);
                    _thruster.SetAngularThrust(shuttle, true);
                }
            }
        }
    }

    // .NET 8 seem to miscompile usage of Vector2.Dot above. This manual outline fixes it pending an upstream fix.
    // See PR #24008
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float Vector2Dot(Vector2 value1, Vector2 value2)
    {
        return Vector2.Dot(value1, value2);
    }

    private bool CanPilot(EntityUid shuttleUid)
    {
        return FTLQuery.TryComp(shuttleUid, out var ftl)
        && (ftl.State & (FTLState.Starting | FTLState.Travelling | FTLState.Arriving)) != 0x0
            || PreventPilotQuery.HasComp(shuttleUid)
            || HasDockedPort(shuttleUid);
    }

    private bool HasDockedPort(EntityUid shuttleUid)
    {
        _shuttleDockSet.Clear();
        _lookup.GetChildEntities(shuttleUid, _shuttleDockSet);

        foreach (var dock in _shuttleDockSet)
        {
            if (dock.Comp.DockedWith != null)
                return true;
        }

        return false;
    }

}
