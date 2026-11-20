// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using Content.Server.Physics.Controllers;
using Content.Server.Power.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Physics;

public sealed partial class StallCauseLogSystem : EntitySystem
{
    private const double HotMs = 5d;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MoverController _mover = default!;
    [Dependency] private PhysicsSystem _physics = default!;
    [Dependency] private PowerNetSystem _power = default!;

    private readonly Dictionary<string, double> _phaseSum = new();
    private bool _ready;
    private TimeSpan _nextLog;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(PhysicsSystem));
        UpdatesAfter.Add(typeof(PowerNetSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var before = PhaseMs("BeforeSolve");
        var broad = PhaseMs("Broadphase");
        var contacts = PhaseMs("CollideContacts");
        var step = PhaseMs("Step");
        if (!_ready)
        {
            _ready = true;
            return;
        }

        var mover = _mover.ReadStall();
        var reconnect = _power.StallReconnectMs;
        var sync = _power.StallSyncMs;
        var solver = _power.StallSolverMs;
        var receivers = _power.StallReceiverMs;
        var hot = before >= HotMs || step >= HotMs || broad >= HotMs || contacts >= HotMs
                  || reconnect >= HotMs || solver >= HotMs || sync >= HotMs || receivers >= HotMs
                  || mover.MobsMs >= HotMs || mover.ShuttleMs >= HotMs;
        if (!hot || _timing.CurTime < _nextLog)
            return;

        _nextLog = _timing.CurTime + TimeSpan.FromSeconds(15);
        var who = Exists(mover.Slowest) ? ToPrettyString(mover.Slowest) : "none";
        Log.Warning(
            $"Stall: BeforeSolve {before:0.00} ms, Step {step:0.00} ms, Broadphase {broad:0.00} ms, Contacts {contacts:0.00} ms, awake {_physics.AwakeBodies.Count} | " +
            $"mover mobs {mover.MobsMs:0.00} ms ({mover.Count}) shuttle {mover.ShuttleMs:0.00} ms slowest {who} {mover.SlowMs:0.00} ms | " +
            $"idle {mover.Idle}/{mover.IdleMs:0.00} tile {mover.Tile}/{mover.TileMs:0.00} space {mover.Space}/{mover.SpaceMs:0.00} ground {mover.Ground}/{mover.GroundMs:0.00} | " +
            $"tileStep {mover.TileStepMs:0.00} collider {mover.ColliderMs:0.00} tileLookup {mover.TileLookupMs:0.00} velocity {mover.VelocityMs:0.00} turn {mover.TurnMs:0.00} foot {mover.FootMs:0.00} | " +
            $"power reconnect {reconnect:0.00} ms (apc {_power.StallApcReconnects} net {_power.StallNetReconnects}) sync {sync:0.00} ms solver {solver:0.00} ms receivers {receivers:0.00} ms | " +
            $"tick {_timing.CurTick}");
    }

    private double PhaseMs(string phase)
    {
        var sum = SharedPhysicsSystem.TickUsagePhaseHistogram.WithLabels(phase).Sum;
        var previous = _phaseSum.GetValueOrDefault(phase);
        _phaseSum[phase] = sum;
        return (sum - previous) * 1000d;
    }
}
