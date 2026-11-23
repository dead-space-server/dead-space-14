// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Buffers.Text;
using System.IO;
using System.Threading;
using Prometheus;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Diagnostics;

public sealed partial class TickStarveProbeSystem : EntitySystem
{
    private const double SlowTickMs = 100d;
    private const double PausedTickMs = 30000d;
    private const double SampleEveryMs = 1000d;

    private const string SchedStatPath = "/proc/thread-self/schedstat";
    private const string ThreadStatusPath = "/proc/thread-self/status";
    private const string ProcessStatPath = "/proc/self/stat";
    private const string CpuPressurePath = "/proc/pressure/cpu";
    private const string MemoryPressurePath = "/proc/pressure/memory";
    private const string IoPressurePath = "/proc/pressure/io";

    private static readonly Counter MainRunSeconds = Metrics.CreateCounter(
        "ds_main_thread_run_seconds_total",
        "");

    private static readonly Counter MainWaitSeconds = Metrics.CreateCounter(
        "ds_main_thread_runqueue_wait_seconds_total",
        "");

    private static readonly Counter MainVoluntarySwitches = Metrics.CreateCounter(
        "ds_main_thread_voluntary_switches_total",
        "");

    private static readonly Counter MainInvoluntarySwitches = Metrics.CreateCounter(
        "ds_main_thread_involuntary_switches_total",
        "");

    private static readonly Counter MajorFaults = Metrics.CreateCounter(
        "ds_process_major_faults_total",
        "");

    private static readonly Counter PressureStallSeconds = Metrics.CreateCounter(
        "ds_host_pressure_stall_seconds_total",
        "",
        "resource", "kind");

    private static readonly Counter ThrottledSeconds = Metrics.CreateCounter(
        "ds_cgroup_throttled_seconds_total",
        "");

    private static readonly Counter ThrottledPeriods = Metrics.CreateCounter(
        "ds_cgroup_throttled_periods_total",
        "");

    private static readonly Counter GcPauseSeconds = Metrics.CreateCounter(
        "ds_gc_pause_seconds_total",
        "");

    private static readonly Gauge PoolPendingItems = Metrics.CreateGauge(
        "ds_threadpool_pending_items",
        "");

    private static readonly Gauge PoolThreads = Metrics.CreateGauge(
        "ds_threadpool_threads",
        "");

    [Dependency] private IGameTiming _timing = default!;

    private readonly byte[] _buffer = new byte[4096];
    private readonly Stopwatch _wall = new();

    private bool _enabled;
    private bool _primed;
    private string? _cgroupCpuStatPath;
    private TimeSpan _nextLog;
    private Sample _last;
    private double _sinceSampleMs;

    public override void Initialize()
    {
        base.Initialize();

        _enabled = OperatingSystem.IsLinux() && File.Exists(SchedStatPath);
        if (!_enabled)
            return;

        _cgroupCpuStatPath = FindCgroupCpuStat();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        var wallMs = _wall.Elapsed.TotalMilliseconds;
        _wall.Restart();
        _sinceSampleMs += wallMs;

        var slow = wallMs >= SlowTickMs && wallMs <= PausedTickMs;
        if (_primed && !slow && _sinceSampleMs < SampleEveryMs)
            return;

        var windowMs = _sinceSampleMs;
        _sinceSampleMs = 0d;

        var now = TakeSample();
        Export(now);
        PoolPendingItems.Set(ThreadPool.PendingWorkItemCount);
        PoolThreads.Set(ThreadPool.ThreadCount);

        var last = _last;
        _last = now;

        if (!_primed)
        {
            _primed = true;
            return;
        }

        if (!slow || _timing.CurTime < _nextLog)
            return;

        _nextLog = _timing.CurTime + TimeSpan.FromSeconds(5);

        var runMs = (now.RunNs - last.RunNs) / 1e6;
        var waitMs = (now.WaitNs - last.WaitNs) / 1e6;
        Log.Warning(
            $"Tick starve: wall {wallMs:0.0} ms, counted over {windowMs:0.0} ms: main run {runMs:0.0} ms, runqueue wait {waitMs:0.0} ms, other {windowMs - runMs - waitMs:0.0} ms, " +
            $"switches voluntary {now.Voluntary - last.Voluntary} involuntary {now.Involuntary - last.Involuntary}, major faults {now.MajorFaults - last.MajorFaults} | " +
            $"host stall cpu some {UsToMs(now.CpuSome - last.CpuSome):0.0} ms, memory some {UsToMs(now.MemorySome - last.MemorySome):0.0} full {UsToMs(now.MemoryFull - last.MemoryFull):0.0} ms, " +
            $"io some {UsToMs(now.IoSome - last.IoSome):0.0} full {UsToMs(now.IoFull - last.IoFull):0.0} ms | " +
            $"throttled {UsToMs(now.ThrottledUs - last.ThrottledUs):0.0} ms ({now.ThrottledPeriods - last.ThrottledPeriods} periods) | " +
            $"gc pause {(now.GcPause - last.GcPause).TotalMilliseconds:0.0} ms, gen0 {now.Gen0 - last.Gen0} gen1 {now.Gen1 - last.Gen1} gen2 {now.Gen2 - last.Gen2}, " +
            $"allocated {(now.Allocated - last.Allocated) / 1048576d:0.0} MB | " +
            $"pool pending {ThreadPool.PendingWorkItemCount} threads {ThreadPool.ThreadCount} | tick {_timing.CurTick}");
    }

    private Sample TakeSample()
    {
        var sample = new Sample();

        var span = ReadFile(SchedStatPath);
        if (TryParseAt(span, 0, out sample.RunNs, out var next))
            TryParseAt(span, next, out sample.WaitNs, out _);

        span = ReadFile(ThreadStatusPath);
        sample.Voluntary = FindLong(span, "\nvoluntary_ctxt_switches:"u8);
        sample.Involuntary = FindLong(span, "\nnonvoluntary_ctxt_switches:"u8);

        sample.MajorFaults = ReadMajorFaults();

        span = ReadFile(CpuPressurePath);
        sample.CpuSome = FindLong(span, "total="u8);

        span = ReadFile(MemoryPressurePath);
        sample.MemorySome = FindLong(span, "total="u8);
        sample.MemoryFull = FindLong(span, "full"u8, "total="u8);

        span = ReadFile(IoPressurePath);
        sample.IoSome = FindLong(span, "total="u8);
        sample.IoFull = FindLong(span, "full"u8, "total="u8);

        if (_cgroupCpuStatPath != null)
        {
            span = ReadFile(_cgroupCpuStatPath);
            sample.ThrottledUs = FindLong(span, "throttled_usec"u8);
            sample.ThrottledPeriods = FindLong(span, "nr_throttled"u8);
        }

        sample.GcPause = GC.GetTotalPauseDuration();
        sample.Gen0 = GC.CollectionCount(0);
        sample.Gen1 = GC.CollectionCount(1);
        sample.Gen2 = GC.CollectionCount(2);
        sample.Allocated = GC.GetTotalAllocatedBytes();

        return sample;
    }

    private static void Export(in Sample sample)
    {
        MainRunSeconds.IncTo(sample.RunNs / 1e9);
        MainWaitSeconds.IncTo(sample.WaitNs / 1e9);
        MainVoluntarySwitches.IncTo(sample.Voluntary);
        MainInvoluntarySwitches.IncTo(sample.Involuntary);
        MajorFaults.IncTo(sample.MajorFaults);
        PressureStallSeconds.WithLabels("cpu", "some").IncTo(sample.CpuSome / 1e6);
        PressureStallSeconds.WithLabels("memory", "some").IncTo(sample.MemorySome / 1e6);
        PressureStallSeconds.WithLabels("memory", "full").IncTo(sample.MemoryFull / 1e6);
        PressureStallSeconds.WithLabels("io", "some").IncTo(sample.IoSome / 1e6);
        PressureStallSeconds.WithLabels("io", "full").IncTo(sample.IoFull / 1e6);
        ThrottledSeconds.IncTo(sample.ThrottledUs / 1e6);
        ThrottledPeriods.IncTo(sample.ThrottledPeriods);
        GcPauseSeconds.IncTo(sample.GcPause.TotalSeconds);
    }

    private long ReadMajorFaults()
    {
        var span = ReadFile(ProcessStatPath);
        var start = span.LastIndexOf((byte) ')');
        if (start < 0)
            return 0;

        var field = 0;
        var i = start + 1;
        while (i < span.Length)
        {
            while (i < span.Length && span[i] == (byte) ' ')
                i++;

            if (field == 9)
                return TryParseAt(span, i, out var value, out _) ? value : 0;

            while (i < span.Length && span[i] != (byte) ' ')
                i++;

            field++;
        }

        return 0;
    }

    private ReadOnlySpan<byte> ReadFile(string path)
    {
        try
        {
            using var handle = File.OpenHandle(path);
            var read = RandomAccess.Read(handle, _buffer, 0);
            return _buffer.AsSpan(0, read);
        }
        catch (IOException)
        {
            return ReadOnlySpan<byte>.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return ReadOnlySpan<byte>.Empty;
        }
        catch (NotSupportedException)
        {
            return ReadOnlySpan<byte>.Empty;
        }
    }

    private static string? FindCgroupCpuStat()
    {
        try
        {
            foreach (var line in File.ReadAllLines("/proc/self/cgroup"))
            {
                if (!line.StartsWith("0::", StringComparison.Ordinal))
                    continue;

                var path = $"/sys/fs/cgroup{line[3..]}/cpu.stat";
                return File.Exists(path) ? path : null;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static long FindLong(ReadOnlySpan<byte> span, ReadOnlySpan<byte> key)
    {
        var index = span.IndexOf(key);
        if (index < 0)
            return 0;

        return TryParseAt(span, index + key.Length, out var value, out _) ? value : 0;
    }

    private static long FindLong(ReadOnlySpan<byte> span, ReadOnlySpan<byte> section, ReadOnlySpan<byte> key)
    {
        var index = span.IndexOf(section);
        return index < 0 ? 0 : FindLong(span[index..], key);
    }

    private static bool TryParseAt(ReadOnlySpan<byte> span, int start, out long value, out int next)
    {
        var i = start;
        while (i < span.Length && (span[i] == (byte) ' ' || span[i] == (byte) '\t'))
            i++;

        if (i < span.Length && Utf8Parser.TryParse(span[i..], out value, out var consumed))
        {
            next = i + consumed;
            return true;
        }

        value = 0;
        next = i;
        return false;
    }

    private static double UsToMs(long us)
    {
        return us / 1000d;
    }

    private struct Sample
    {
        public long RunNs;
        public long WaitNs;
        public long Voluntary;
        public long Involuntary;
        public long MajorFaults;
        public long CpuSome;
        public long MemorySome;
        public long MemoryFull;
        public long IoSome;
        public long IoFull;
        public long ThrottledUs;
        public long ThrottledPeriods;
        public TimeSpan GcPause;
        public int Gen0;
        public int Gen1;
        public int Gen2;
        public long Allocated;
    }
}
