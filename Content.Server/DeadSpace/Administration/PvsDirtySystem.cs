// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Text;
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

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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
