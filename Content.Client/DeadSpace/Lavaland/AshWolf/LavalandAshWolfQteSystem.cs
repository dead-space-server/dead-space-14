// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Resources;
using Content.Shared.DeadSpace.Lavaland.AshWolf;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland.AshWolf;

/// <summary>
/// The quick time event of the Ash Wolf's pounce, played on the client's own clock so the ping does not matter:
/// a needle goes round a dial, and LMB, RMB or Space has to be pressed while it is in the bright zone, a few times
/// in a row. A press outside the zone, or a full turn without one, fails it. Only the result goes to the server.
/// </summary>
public sealed class LavalandAshWolfQteSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private LavalandAshWolfQteOverlay _overlay = default!;

    public LavalandAshWolfQteStartEvent? Event { get; private set; }

    /// <summary>Real time of the start of the event, and of the turn of the needle now.</summary>
    private TimeSpan _start;

    public TimeSpan CheckStart { get; private set; }
    public int Check { get; private set; }

    /// <summary>Shown a moment after the end: true - escaped, false - failed.</summary>
    public bool? Outcome { get; private set; }

    public TimeSpan OutcomeUntil { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LavalandAshWolfQteStartEvent>(OnStart);
        SubscribeNetworkEvent<LavalandAshWolfQteStopEvent>(OnStop);
        _overlay = new LavalandAshWolfQteOverlay(this, _cache);
        _overlays.AddOverlay(_overlay);
        _input.FirstChanceOnKeyEvent += OnKey;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _input.FirstChanceOnKeyEvent -= OnKey;
        _overlays.RemoveOverlay(_overlay);
    }

    public TimeSpan Now => _timing.RealTime;

    /// <summary>Where the needle is in its turn (0..1); negative before the turn starts.</summary>
    public float Needle => Event is { } ev ? (float) ((Now - CheckStart).TotalSeconds / ev.TurnTime) : 0f;

    private void OnStart(LavalandAshWolfQteStartEvent ev)
    {
        Event = ev;
        _start = Now;
        Check = 0;
        CheckStart = _start + TimeSpan.FromSeconds(ev.FirstDelay);
        Outcome = null;
    }

    private void OnStop(LavalandAshWolfQteStopEvent ev)
    {
        if (Event?.Id == ev.Id)
            Event = null;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (Event is not { } ev)
            return;

        // A whole turn without a press, or the time is up.
        if (Needle >= 1f || (Now - _start).TotalSeconds > ev.Duration)
            Finish(false);
    }

    private void OnKey(KeyEventArgs args, KeyEventType type)
    {
        if (Event is not { } ev || type != KeyEventType.Down || args.IsRepeat ||
            args.Key is not (Keyboard.Key.MouseLeft or Keyboard.Key.MouseRight or Keyboard.Key.Space))
        {
            return;
        }

        // Pinned, the keys do nothing else meanwhile.
        args.Handle();

        var needle = Needle;
        // Between two turns a press does not count either way.
        if (needle < 0f)
            return;

        var zone = ev.ZoneStarts[Check];
        if (needle < zone || needle > zone + ev.ZoneWidth)
        {
            Finish(false);
            return;
        }

        Check++;
        if (Check >= ev.Checks)
        {
            Finish(true);
            return;
        }

        CheckStart = Now + TimeSpan.FromSeconds(ev.Gap);
    }

    private void Finish(bool success)
    {
        if (Event is not { } ev)
            return;

        RaiseNetworkEvent(new LavalandAshWolfQteResultEvent(ev.Id, success));
        Event = null;
        Outcome = success;
        OutcomeUntil = Now + TimeSpan.FromSeconds(0.7);
    }
}

/// <summary>The dial of the quick time event, low in the middle of the screen.</summary>
public sealed class LavalandAshWolfQteOverlay : Overlay
{
    private const string FontPath = "/Fonts/NotoSans/NotoSans-Bold.ttf";
    private const float Radius = 58f;
    private const int RingSegments = 64;

    private static readonly Color Back = Color.FromHex("#0b0b0dcc");
    private static readonly Color Ring = Color.FromHex("#d8d8df");
    private static readonly Color Zone = Color.FromHex("#ffd24a");
    private static readonly Color NeedleColor = Color.FromHex("#ff3b2e");

    private readonly LavalandAshWolfQteSystem _system;
    private readonly Font _font;
    private readonly Font _small;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public LavalandAshWolfQteOverlay(LavalandAshWolfQteSystem system, IResourceCache cache)
    {
        _system = system;
        _font = cache.GetFont(FontPath, 18);
        _small = cache.GetFont(FontPath, 13);
        ZIndex = 200;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.Event != null || _system.Outcome != null && _system.Now < _system.OutcomeUntil;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var center = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height * 0.68f);

        handle.DrawCircle(center, Radius + 14f, Back);

        if (_system.Event is not { } ev)
        {
            var success = _system.Outcome == true;
            DrawRing(handle, center, Radius, success ? Zone : NeedleColor, 4f);
            var text = Loc.GetString(success ? "lavaland-ash-wolf-qte-escaped" : "lavaland-ash-wolf-qte-failed");
            DrawCentered(handle, _font, text, center, success ? Zone : NeedleColor);
            return;
        }

        DrawRing(handle, center, Radius, Ring.WithAlpha(0.55f), 2f);

        var check = Math.Min(_system.Check, ev.Checks - 1);
        var zone = ev.ZoneStarts[check];
        var needle = _system.Needle;
        var waiting = needle < 0f;
        DrawArc(handle, center, zone, zone + ev.ZoneWidth, waiting ? Zone.WithAlpha(0.35f) : Zone);

        if (!waiting)
        {
            var tip = center + Polar(needle) * (Radius + 8f);
            handle.DrawLine(center, tip, NeedleColor);
            handle.DrawLine(center + new Vector2(1f, 0f), tip + new Vector2(1f, 0f), NeedleColor);
            handle.DrawLine(center + new Vector2(0f, 1f), tip + new Vector2(0f, 1f), NeedleColor);
        }

        handle.DrawCircle(center, 4f, NeedleColor);
        DrawCentered(handle, _font, $"{_system.Check + 1}/{ev.Checks}", center + new Vector2(0f, -18f), Ring);
        DrawCentered(handle, _small, Loc.GetString("lavaland-ash-wolf-qte-keys"), center + new Vector2(0f, Radius + 30f), Zone);
    }

    /// <summary>A point on the dial: 0 at the top, going clockwise.</summary>
    private static Vector2 Polar(float turn)
    {
        var angle = turn * MathF.Tau;
        return new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
    }

    private static void DrawRing(DrawingHandleScreen handle, Vector2 center, float radius, Color color, float width)
    {
        for (var w = 0f; w < width; w += 1f)
        {
            for (var i = 0; i < RingSegments; i++)
            {
                handle.DrawLine(center + Polar((float) i / RingSegments) * (radius + w),
                    center + Polar((float) (i + 1) / RingSegments) * (radius + w), color);
            }
        }
    }

    private static void DrawArc(DrawingHandleScreen handle, Vector2 center, float from, float to, Color color)
    {
        const float step = 0.004f;
        for (var t = from; t <= to; t += step)
        {
            var direction = Polar(t);
            handle.DrawLine(center + direction * (Radius - 9f), center + direction * (Radius + 9f), color);
        }
    }

    private static void DrawCentered(DrawingHandleScreen handle, Font font, string text, Vector2 at, Color color)
    {
        var size = handle.GetDimensions(font, text, 1f);
        handle.DrawString(font, at - size / 2f, text, color);
    }
}
