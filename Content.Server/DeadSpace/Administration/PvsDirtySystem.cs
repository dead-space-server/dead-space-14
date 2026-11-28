// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Text;
using Content.Shared.Chemistry.Components;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Administration;

public sealed partial class PvsDirtySystem : EntitySystem
{
    private const int TopRows = 20;
    private const int ViewRows = 15;
    private const float ChunkSize = 8f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _inView = new();
    private readonly Dictionary<string, long> _viewPrototypes = new();
    private readonly Dictionary<string, (int Count, float Scale)> _viewPlayers = new();
    private int _viewIndex;
    private int _viewSamples;
    private long _viewTotal;
    private long _viewContained;
    private long _viewSolutions;
    private readonly long[] _viewDepth = new long[3];
    private int _viewMax;
    private float _viewSide;

    private readonly object _lock = new();
    private readonly List<(EntityUid Uid, GameTick Tick)> _pending = new();
    private readonly List<(EntityUid Uid, GameTick Tick)> _ready = new();
    private readonly List<(MapId Map, Vector2 Pos)> _eyes = new();
    private readonly Dictionary<string, Tally> _components = new();
    private readonly Dictionary<string, Tally> _prototypes = new();

    private ISawmill _log = default!;
    private IConsoleShell? _shell;
    private bool _running;
    private int _ticks;
    private int _need;
    private int _seconds;
    private long _entities;
    private long _viewed;
    private long _unseen;
    private long _deleted;
    private float _rangeSquared;

    public bool Running => _running;
    public string? LastReport { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        _log = Logger.GetSawmill("pvsdirty");
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_running)
            EntityManager.EntityDirtied -= OnDirtied;
    }

    public bool Start(IConsoleShell shell, int seconds, out string error)
    {
        error = "";
        if (_running)
        {
            error = "A sample is already running.";
            return false;
        }

        var rate = _timing.TickRate;
        if (rate == 0)
            rate = 30;

        var range = _cfg.GetCVar(CVars.NetMaxUpdateRange);
        _rangeSquared = range * range;
        _shell = shell;
        _seconds = seconds;
        _need = seconds * rate;
        _ticks = 0;
        _entities = 0;
        _viewed = 0;
        _unseen = 0;
        _deleted = 0;
        _components.Clear();
        _prototypes.Clear();
        _viewSide = MathF.Max(range, _cfg.GetCVar(CVars.NetPvsPriorityRange));
        _viewIndex = 0;
        _viewSamples = 0;
        _viewTotal = 0;
        _viewContained = 0;
        _viewSolutions = 0;
        Array.Clear(_viewDepth);
        _viewMax = 0;
        _viewPrototypes.Clear();
        _viewPlayers.Clear();
        lock (_lock)
        {
            _pending.Clear();
        }

        _running = true;
        EntityManager.EntityDirtied += OnDirtied;
        return true;
    }

    private void OnDirtied(Entity<MetaDataComponent> ent)
    {
        lock (_lock)
        {
            _pending.Add((ent.Owner, _timing.CurTick));
        }
    }

    public override void Update(float frameTime)
    {
        if (!_running)
            return;

        var now = _timing.CurTick;
        _ready.Clear();
        lock (_lock)
        {
            var keep = 0;
            for (var i = 0; i < _pending.Count; i++)
            {
                var entry = _pending[i];
                if (entry.Tick < now)
                    _ready.Add(entry);
                else
                    _pending[keep++] = entry;
            }

            _pending.RemoveRange(keep, _pending.Count - keep);
        }

        CollectEyes();
        foreach (var (uid, tick) in _ready)
        {
            Count(uid, tick);
        }

        SampleView();
        _ticks++;
        if (_ticks < _need)
            return;

        Finish();
    }

    private void CollectEyes()
    {
        _eyes.Clear();
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } attached || TerminatingOrDeleted(attached))
                continue;

            var coords = _transform.GetMapCoordinates(attached);
            if (coords.MapId != MapId.Nullspace)
                _eyes.Add((coords.MapId, coords.Position));
        }
    }

    private void SampleView()
    {
        var sessions = _players.Sessions;
        if (sessions.Length == 0)
            return;

        var session = sessions[_viewIndex++ % sessions.Length];
        if (session.AttachedEntity is not { } attached || TerminatingOrDeleted(attached))
            return;

        var coords = _transform.GetMapCoordinates(attached);
        if (coords.MapId == MapId.Nullspace)
            return;

        var scale = 1f;
        if (TryComp(attached, out EyeComponent? eye))
        {
            scale = MathF.Max(eye.PvsScale, 0.1f);
            coords = coords.Offset(eye.Offset);
        }

        var half = (_viewSide * scale + ChunkSize) / 2f;
        _inView.Clear();
        _lookup.GetEntitiesIntersecting(coords.MapId, Box2.CenteredAround(coords.Position, new Vector2(half * 2f)), _inView, LookupFlags.All);

        var count = _inView.Count;
        foreach (var uid in _inView)
        {
            if ((MetaData(uid).Flags & MetaDataFlags.InContainer) != 0)
                _viewContained++;

            var depth = Depth(uid);
            _viewDepth[Math.Min(depth, _viewDepth.Length - 1)]++;
            if (depth >= 1 && HasComp<SolutionComponent>(uid))
                _viewSolutions++;
        }

        _viewSamples++;
        _viewTotal += count;
        if (count > _viewMax)
            _viewMax = count;
        _viewPlayers[session.Name] = (count, scale);

        foreach (var uid in _inView)
        {
            var id = MetaData(uid).EntityPrototype?.ID ?? "(no prototype)";
            _viewPrototypes[id] = _viewPrototypes.GetValueOrDefault(id) + 1;
        }
    }

    private int Depth(EntityUid uid)
    {
        var depth = 0;
        var xform = Transform(uid);
        while (xform.ParentUid.IsValid() && xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
        {
            depth++;
            xform = Transform(xform.ParentUid);
        }

        return depth;
    }

    private void Count(EntityUid uid, GameTick tick)
    {
        if (!TryComp(uid, out MetaDataComponent? meta) || meta.EntityLifeStage >= EntityLifeStage.Terminating)
        {
            _deleted++;
            return;
        }

        var viewers = Viewers(uid);
        _entities++;
        _viewed += viewers;
        if (viewers == 0)
            _unseen++;

        Add(_prototypes, meta.EntityPrototype?.ID ?? "(no prototype)", viewers);
        foreach (var (_, component) in EntityManager.GetNetComponents(uid, meta))
        {
            if (component.LastModifiedTick == tick)
                Add(_components, component.GetType().Name, viewers);
        }
    }

    private int Viewers(EntityUid uid)
    {
        var coords = _transform.GetMapCoordinates(uid);
        if (coords.MapId == MapId.Nullspace)
            return 0;

        var viewers = 0;
        foreach (var (map, pos) in _eyes)
        {
            if (map == coords.MapId && Vector2.DistanceSquared(pos, coords.Position) <= _rangeSquared)
                viewers++;
        }

        return viewers;
    }

    private static void Add(Dictionary<string, Tally> table, string key, int viewers)
    {
        table.TryGetValue(key, out var tally);
        tally.Dirty++;
        tally.Viewed += viewers;
        table[key] = tally;
    }

    private void Finish()
    {
        _running = false;
        EntityManager.EntityDirtied -= OnDirtied;
        lock (_lock)
        {
            _pending.Clear();
        }

        var ticks = Math.Max(_ticks, 1);
        var text = new StringBuilder();
        text.AppendLine($"pvsdirty: {_ticks} ticks, {_seconds} s, {_eyes.Count} players in game, range {MathF.Sqrt(_rangeSquared):F0}");
        text.AppendLine($"dirty entities {(double) _entities / ticks:F1}/tick, seen by nobody {(double) _unseen / ticks:F1}/tick, " +
                        $"entity x player {(double) _viewed / ticks:F1}/tick, deleted after dirty {(double) _deleted / ticks:F1}/tick");
        AppendTable(text, "components", _components, ticks);
        AppendTable(text, "prototypes", _prototypes, ticks);
        AppendView(text);

        var report = text.ToString().TrimEnd();
        LastReport = report;
        try
        {
            _shell?.WriteLine(report);
        }
        catch (Exception e)
        {
            _log.Warning($"Sample console is gone: {e.Message}");
        }

        _shell = null;
        _components.Clear();
        _prototypes.Clear();
        _viewPrototypes.Clear();
        _viewPlayers.Clear();
    }

    private double Percent(long value)
    {
        return _viewTotal > 0 ? (double) value / _viewTotal * 100d : 0d;
    }

    private void AppendView(StringBuilder text)
    {
        if (_viewSamples == 0)
            return;

        var players = new List<(string Name, int Count, float Scale)>(_viewPlayers.Count);
        long sum = 0;
        foreach (var (name, (count, scale)) in _viewPlayers)
        {
            players.Add((name, count, scale));
            sum += count;
        }

        text.AppendLine($"in view: {_viewSamples} samples, avg {(double) _viewTotal / _viewSamples:F0}, max {_viewMax} entities per player, " +
                        $"all players together {sum} per tick, box side {_viewSide:F0} + chunk, " +
                        $"in containers {Percent(_viewContained):F0}%");
        text.AppendLine($"in view by depth: top level {Percent(_viewDepth[0]):F0}%, children {Percent(_viewDepth[1]):F0}%, " +
                        $"deeper {Percent(_viewDepth[2]):F0}%, solutions below top level {Percent(_viewSolutions):F0}%");

        players.Sort(static (a, b) => b.Count.CompareTo(a.Count));
        text.Append("most in view:");
        for (var i = 0; i < players.Count && i < 8; i++)
        {
            text.Append($" {players[i].Name} {players[i].Count}");
            if (players[i].Scale > 1.01f)
                text.Append($" (scale {players[i].Scale:F1})");
            text.Append(';');
        }

        text.AppendLine();
        var zoomed = 0;
        foreach (var player in players)
        {
            if (player.Scale > 1.01f)
                zoomed++;
        }

        text.AppendLine($"players with pvs scale above 1: {zoomed}");

        var rows = new List<(string Id, long Count)>(_viewPrototypes.Count);
        foreach (var (id, count) in _viewPrototypes)
        {
            rows.Add((id, count));
        }

        rows.Sort(static (a, b) => b.Count.CompareTo(a.Count));
        text.AppendLine("in view by prototype: avg per player");
        for (var i = 0; i < rows.Count && i < ViewRows; i++)
        {
            text.AppendLine($"   {(double) rows[i].Count / _viewSamples,9:F1}  {rows[i].Id}");
        }
    }

    private static void AppendTable(StringBuilder text, string title, Dictionary<string, Tally> table, int ticks)
    {
        var rows = new List<(string Key, Tally Tally)>(table.Count);
        foreach (var (key, tally) in table)
        {
            rows.Add((key, tally));
        }

        rows.Sort(static (a, b) => b.Tally.Viewed != a.Tally.Viewed
            ? b.Tally.Viewed.CompareTo(a.Tally.Viewed)
            : b.Tally.Dirty.CompareTo(a.Tally.Dirty));
        text.AppendLine($"{title}: entity x player/tick, dirty/tick");
        for (var i = 0; i < rows.Count && i < TopRows; i++)
        {
            var tally = rows[i].Tally;
            text.AppendLine($"   {(double) tally.Viewed / ticks,9:F1}  {(double) tally.Dirty / ticks,8:F1}  {rows[i].Key}");
        }
    }

    private struct Tally
    {
        public long Dirty;
        public long Viewed;
    }
}
