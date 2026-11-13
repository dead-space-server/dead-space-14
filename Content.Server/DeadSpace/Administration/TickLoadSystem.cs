// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Reflection;
using System.Text;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Administration;

public sealed partial class TickLoadSystem : EntitySystem
{
    private const double MinAvgMs = 0.05;
    private const double MinSpikeMs = 1d;

    private static readonly string[] PhysicsParts =
    {
        "_beforeSolveMonitor",
        "_broadphaseMonitor",
        "_collideMonitor",
        "_stepMonitor",
        "_afterSolveMonitor",
        "_finalStepMonitor",
    };

    private static readonly string[] PhysicsNames =
    {
        "BeforeSolve",
        "Broadphase",
        "CollideContacts",
        "Step",
        "AfterSolve",
        "FinalStep",
    };

    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IGameTiming _timing = default!;

    private ISawmill _log = default!;
    private PropertyInfo? _sum;
    private IConsoleShell? _shell;
    private bool _running;
    private bool _metricsWereOn;
    private bool _physicsWereOn;
    private bool _physicsBound;
    private int _ticks;
    private int _need;
    private int _seconds;
    private List<Row> _rows = new();
    private List<Row> _physics = new();
    private double[] _before = Array.Empty<double>();
    private double[] _last = Array.Empty<double>();
    private double[] _max = Array.Empty<double>();
    private double[] _tickMs = Array.Empty<double>();
    private double[] _worstParts = Array.Empty<double>();
    private double[] _physicsBefore = Array.Empty<double>();
    private double _worstSum;
    private long _awakeSum;
    private int _awakeMax;
    private TimeSpan _startPause;
    private TimeSpan _lastPause;
    private readonly int[] _startGcCounts = new int[3];
    private readonly int[] _lastGcCounts = new int[3];
    private int _gcTicks;
    private double _maxPause;
    private int _worstGen;
    private double _worstPause;
    private PhysicsSystem? _physicsSystem;

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("tickload");
    }

    public bool Start(IConsoleShell shell, int seconds, out string error)
    {
        error = "";
        if (_running)
        {
            error = "A sample is already running.";
            return false;
        }

        if (!Bind(out error))
            return false;

        var rate = _timing.TickRate;
        if (rate == 0)
            rate = 30;

        _shell = shell;
        _seconds = seconds;
        _metricsWereOn = _systems.MetricsEnabled;
        _systems.MetricsEnabled = true;
        BindPhysics();
        var count = _rows.Count;
        _before = Snapshot(_rows);
        _last = _before;
        _max = new double[count];
        _tickMs = new double[count];
        _worstParts = new double[count];
        _worstSum = 0d;
        _physicsBefore = Snapshot(_physics);
        _awakeSum = 0;
        _awakeMax = 0;
        _startPause = _lastPause = GC.GetTotalPauseDuration();
        for (var g = 0; g < 3; g++)
            _startGcCounts[g] = _lastGcCounts[g] = GC.CollectionCount(g);
        _gcTicks = 0;
        _maxPause = 0;
        _worstGen = -1;
        _worstPause = 0;
        _ticks = 0;
        _need = seconds * rate;
        _running = true;
        return true;
    }

    public override void Update(float frameTime)
    {
        if (!_running)
            return;

        var now = Snapshot(_rows);
        var tickSum = 0d;
        for (var i = 0; i < _rows.Count; i++)
        {
            var ms = (now[i] - _last[i]) * 1000d;
            if (ms < 0d)
                ms = 0d;
            _tickMs[i] = ms;
            if (ms > _max[i])
                _max[i] = ms;
            tickSum += ms;
        }

        _last = now;
        var pause = GC.GetTotalPauseDuration();
        var tickPause = (pause - _lastPause).TotalMilliseconds;
        _lastPause = pause;
        var gen = -1;
        for (var g = 2; g >= 0; g--)
        {
            var count = GC.CollectionCount(g);
            if (count != _lastGcCounts[g] && gen < 0)
                gen = g;
            _lastGcCounts[g] = count;
        }

        if (gen >= 0)
            _gcTicks++;
        if (tickPause > _maxPause)
            _maxPause = tickPause;

        if (_physicsSystem != null)
        {
            var awake = _physicsSystem.AwakeBodies.Count;
            _awakeSum += awake;
            if (awake > _awakeMax)
                _awakeMax = awake;
        }

        if (tickSum > _worstSum)
        {
            _worstSum = tickSum;
            _worstGen = gen;
            _worstPause = tickPause;
            Array.Copy(_tickMs, _worstParts, _rows.Count);
        }

        _ticks++;
        if (_ticks < _need)
            return;

        Finish();
    }

    private void Finish()
    {
        _running = false;
        var physicsAfter = Snapshot(_physics);
        _systems.MetricsEnabled = _metricsWereOn;
        if (_physicsBound && _physicsSystem != null)
            SetPhysicsMetrics(_physicsSystem, _physicsWereOn);

        var ticks = _ticks;
        var total = 0d;
        var lines = new List<Line>(_rows.Count);
        for (var i = 0; i < _rows.Count; i++)
        {
            var spent = (_last[i] - _before[i]) * 1000d;
            if (spent < 0d)
                spent = 0d;
            var avg = ticks > 0 ? spent / ticks : 0d;
            total += spent;
            lines.Add(new Line(_rows[i].Name, avg, _max[i]));
        }

        lines.Sort(static (a, b) => b.Avg.CompareTo(a.Avg));
        var perTick = ticks > 0 ? total / ticks : 0d;
        var rate = _seconds > 0 ? ticks / _seconds : _timing.TickRate;
        var budget = rate > 0 ? 1000d / rate : 0d;
        var text = new StringBuilder();
        text.AppendLine($"tickload: {ticks} ticks, {_seconds} s, rate {rate}, budget {budget:F2} ms");
        text.AppendLine($"systems sum avg {perTick:F2} ms/tick, worst tick {_worstSum:F2} ms");
        var gcPause = (GC.GetTotalPauseDuration() - _startPause).TotalMilliseconds;
        text.AppendLine($"gc: gen0 {GC.CollectionCount(0) - _startGcCounts[0]}, gen1 {GC.CollectionCount(1) - _startGcCounts[1]}, gen2 {GC.CollectionCount(2) - _startGcCounts[2]}, " +
                        $"paused {gcPause:F1} ms total, max {_maxPause:F1} ms in one tick, {_gcTicks} ticks with a gc; " +
                        $"worst tick: {(_worstGen >= 0 ? $"gen{_worstGen} gc, paused {_worstPause:F1} ms" : "no gc")}");
        text.AppendLine("      avg ms    max ms   share  system");

        var shown = 0;
        var tailMs = 0d;
        var tailCount = 0;
        foreach (var line in lines)
        {
            var keep = line.Avg >= MinAvgMs || line.Max >= MinSpikeMs || shown < 15;
            if (!keep)
            {
                tailMs += line.Avg;
                tailCount++;
                continue;
            }

            var share = perTick > 0d ? line.Avg / perTick * 100d : 0d;
            text.AppendLine($"   {line.Avg,8:F3}  {line.Max,8:F3}  {share,5:F1}%  {line.Name}");
            shown++;
        }

        if (tailCount > 0)
            text.AppendLine($"   ... {tailCount} systems under {MinAvgMs:F3} ms, together {tailMs:F2} ms/tick");

        text.AppendLine("worst tick:");
        var worst = new List<Line>(_rows.Count);
        for (var i = 0; i < _rows.Count; i++)
            worst.Add(new Line(_rows[i].Name, _worstParts[i], 0d));
        worst.Sort(static (a, b) => b.Avg.CompareTo(a.Avg));
        var worstShown = 0;
        foreach (var line in worst)
        {
            if (worstShown >= 8 || line.Avg < MinAvgMs)
                break;
            var share = _worstSum > 0d ? line.Avg / _worstSum * 100d : 0d;
            text.AppendLine($"   {line.Avg,8:F3} ms  {share,5:F1}%  {line.Name}");
            worstShown++;
        }

        if (_physics.Count > 0)
        {
            text.AppendLine("physics parts:");
            for (var i = 0; i < _physics.Count; i++)
            {
                var ms = (physicsAfter[i] - _physicsBefore[i]) * 1000d;
                if (ms < 0d)
                    ms = 0d;
                var avg = ticks > 0 ? ms / ticks : 0d;
                text.AppendLine($"   {avg,8:F3} ms/tick  {_physics[i].Name}");
            }
        }

        AppendAwakeBodies(text, ticks);

        var report = text.ToString().TrimEnd();
        _log.Info(report);
        try
        {
            _shell?.WriteLine(report);
        }
        catch (Exception e)
        {
            _log.Warning($"Sample console is gone: {e.Message}");
        }

        _shell = null;
    }

    private void AppendAwakeBodies(StringBuilder text, int ticks)
    {
        if (_physicsSystem == null)
            return;

        var groups = new Dictionary<string, AwakeGroup>();
        var contacts = 0;
        var places = new Dictionary<string, int>();
        foreach (var (uid, body, xform) in _physicsSystem.AwakeBodies)
        {
            var id = MetaData(uid).EntityPrototype?.ID ?? "(no prototype)";
            var place = PlaceName(xform);
            groups.TryGetValue(id, out var group);
            group.Bodies++;
            group.Contacts += body.ContactCount;
            if (body.LinearVelocity.LengthSquared() > 0.0001f || MathF.Abs(body.AngularVelocity) > 0.01f)
                group.Moving++;
            group.Place ??= place;
            groups[id] = group;
            contacts += body.ContactCount;
            places[place] = places.GetValueOrDefault(place) + 1;
        }

        var avgAwake = ticks > 0 ? (double) _awakeSum / ticks : 0d;
        text.AppendLine($"awake bodies: avg {avgAwake:F0}, max {_awakeMax}, now {_physicsSystem.AwakeBodies.Count}, their contacts now {contacts}");

        var top = new List<(string Id, AwakeGroup Group)>(groups.Count);
        foreach (var (id, group) in groups)
            top.Add((id, group));

        top.Sort(static (a, b) => b.Group.Bodies.CompareTo(a.Group.Bodies));
        for (var i = 0; i < top.Count && i < 10; i++)
        {
            var group = top[i].Group;
            text.AppendLine($"   {group.Bodies,6}  moving {group.Moving,4}  contacts {group.Contacts,5}  {top[i].Id}  on {group.Place}");
        }

        var placeList = new List<(string Place, int Count)>(places.Count);
        foreach (var (place, count) in places)
            placeList.Add((place, count));

        placeList.Sort(static (a, b) => b.Count.CompareTo(a.Count));
        text.Append("awake by place:");
        for (var i = 0; i < placeList.Count && i < 6; i++)
            text.Append($" {placeList[i].Place} {placeList[i].Count};");
        text.AppendLine();
    }

    private string PlaceName(TransformComponent xform)
    {
        if (xform.GridUid is { } grid)
            return $"{MetaData(grid).EntityName} ({grid})";

        if (xform.MapUid is { } map)
            return $"{MetaData(map).EntityName} ({map}, no grid)";

        return "nullspace";
    }

    private struct AwakeGroup
    {
        public int Bodies;
        public int Moving;
        public int Contacts;
        public string? Place;
    }

    private bool Bind(out string error)
    {
        error = "";
        _rows = new List<Row>();
        var type = _systems.GetType();
        FieldInfo? field = null;
        while (type != null && field == null)
        {
            field = type.GetField("_updateOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }

        if (field?.GetValue(_systems) is not Array regs)
        {
            error = "System list is missing.";
            return false;
        }

        foreach (var reg in regs)
        {
            if (reg == null)
                continue;
            var regType = reg.GetType();
            var system = regType.GetField("System")?.GetValue(reg);
            var monitor = regType.GetField("Monitor")?.GetValue(reg);
            if (system == null || monitor == null)
            {
                error = "System counters are missing.";
                return false;
            }

            _sum ??= monitor.GetType().GetProperty("Sum");
            if (_sum == null)
            {
                error = "System counters are missing.";
                return false;
            }

            _rows.Add(new Row(system.GetType().Name, monitor));
        }

        return _rows.Count > 0;
    }

    private void BindPhysics()
    {
        _physics = new List<Row>();
        _physicsBound = false;
        _physicsSystem = EntityManager.System<PhysicsSystem>();
        _physicsWereOn = _physicsSystem.MetricsEnabled;
        if (!SetPhysicsMetrics(_physicsSystem, true))
            return;

        for (var i = 0; i < PhysicsParts.Length; i++)
        {
            var field = FindField(_physicsSystem.GetType(), PhysicsParts[i]);
            var monitor = field?.GetValue(_physicsSystem);
            if (monitor == null || monitor.GetType().GetProperty("Sum") == null)
            {
                _physics.Clear();
                SetPhysicsMetrics(_physicsSystem, _physicsWereOn);
                return;
            }

            _physics.Add(new Row(PhysicsNames[i], monitor));
        }

        _physicsBound = true;
    }

    private static bool SetPhysicsMetrics(PhysicsSystem physics, bool enabled)
    {
        var property = physics.GetType().GetProperty("MetricsEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var setter = property?.GetSetMethod(true);
        if (setter == null)
            return false;

        setter.Invoke(physics, new object[] { enabled });
        return true;
    }

    private static FieldInfo? FindField(Type? type, string name)
    {
        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field;
            type = type.BaseType;
        }

        return null;
    }

    private double[] Snapshot(List<Row> rows)
    {
        var values = new double[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            values[i] = (double)_sum!.GetValue(rows[i].Monitor)!;
        return values;
    }

    private readonly record struct Row(string Name, object Monitor);

    private readonly record struct Line(string Name, double Avg, double Max);
}
