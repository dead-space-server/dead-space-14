// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Psychiatry;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client.DeadSpace.Psychiatry;

public sealed class EncephalographBoundUserInterface : BoundUserInterface
{
    private EncephalographWindow? _window;

    public EncephalographBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new EncephalographWindow();
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is EncephalographBoundUserInterfaceState st)
            _window?.Update(st);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _window?.Dispose();
        _window = null;
    }
}

public sealed class EncephalographWindow : DefaultWindow
{
    private readonly Dictionary<PsychiatryBrainRegion, EegTrace> _traces = new();
    private readonly Dictionary<PsychiatryBrainRegion, Label> _labels = new();
    private readonly BrainDiagram _brain;

    public EncephalographWindow()
    {
        Title = Loc.GetString("psychiatry-enceph-title");
        MinSize = SetSize = new Vector2(860, 600);

        var paper = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(Color.FromHex("#f3f6fb")),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var traces = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(8),
        };

        foreach (var region in Enum.GetValues<PsychiatryBrainRegion>())
        {
            var line = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalExpand = true,
            };
            var label = new Label
            {
                Text = RegionName(region),
                MinWidth = 120,
                FontColorOverride = Color.FromHex("#314056"),
                VerticalAlignment = VAlignment.Center,
            };
            var trace = new EegTrace
            {
                Region = region,
                MinHeight = 52,
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            _labels[region] = label;
            _traces[region] = trace;
            line.AddChild(label);
            line.AddChild(trace);
            traces.AddChild(line);
        }

        _brain = new BrainDiagram
        {
            MinSize = new Vector2(300, 520),
            Margin = new Thickness(4, 8, 8, 8),
            VerticalExpand = true,
        };
        row.AddChild(traces);
        row.AddChild(_brain);
        paper.AddChild(row);
        Contents.AddChild(paper);
    }

    public void Update(EncephalographBoundUserInterfaceState state)
    {
        foreach (var region in Enum.GetValues<PsychiatryBrainRegion>())
        {
            state.Activity.TryGetValue(region, out var value);
            var lit = Math.Clamp(value, 0f, 1f);
            if (_traces.TryGetValue(region, out var trace))
                trace.Activity = lit;
            if (_labels.TryGetValue(region, out var label))
                label.FontColorOverride = Mix(Color.FromHex("#314056"), LobeColor(region), lit);
        }

        _brain.Activity = state.Activity;
        _brain.Positronic = state.Positronic;
    }

    internal static string RegionName(PsychiatryBrainRegion region)
    {
        return Loc.GetString(region switch
        {
            PsychiatryBrainRegion.Hearing => "psychiatry-brain-hearing",
            PsychiatryBrainRegion.Voice => "psychiatry-brain-voice",
            PsychiatryBrainRegion.Vision => "psychiatry-brain-vision",
            PsychiatryBrainRegion.Arousal => "psychiatry-brain-arousal",
            PsychiatryBrainRegion.Fear => "psychiatry-brain-fear",
            PsychiatryBrainRegion.Movement => "psychiatry-brain-movement",
            PsychiatryBrainRegion.Memory => "psychiatry-brain-memory",
            _ => "psychiatry-brain-vision",
        });
    }

    internal static Color LobeColor(PsychiatryBrainRegion region)
    {
        return region switch
        {
            PsychiatryBrainRegion.Vision => Color.FromHex("#2f9ed8"),
            PsychiatryBrainRegion.Hearing => Color.FromHex("#2eae6a"),
            PsychiatryBrainRegion.Voice => Color.FromHex("#c9a227"),
            PsychiatryBrainRegion.Movement => Color.FromHex("#7d6ad8"),
            PsychiatryBrainRegion.Fear => Color.FromHex("#d24b4b"),
            PsychiatryBrainRegion.Arousal => Color.FromHex("#d06ad2"),
            PsychiatryBrainRegion.Memory => Color.FromHex("#d98a2b"),
            _ => Color.FromHex("#314056"),
        };
    }

    internal static Color Mix(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t);
    }
}

public sealed class EegTrace : Control
{
    public PsychiatryBrainRegion Region;
    public float Activity;
    private float _shown;
    private float _phase;

    private bool Violent => Region is PsychiatryBrainRegion.Fear or PsychiatryBrainRegion.Arousal;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var blend = 1f - MathF.Exp(-7f * args.DeltaSeconds);
        _shown += (Activity - _shown) * blend;
        if (MathF.Abs(Activity - _shown) < 0.003f)
            _shown = Activity;

        var speed = Violent ? 1.5f + _shown * 8f : 1.05f + _shown * 1.35f;
        _phase += args.DeltaSeconds * speed;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var grid = Color.FromHex("#d5deea");
        var ink = EncephalographWindow.Mix(Color.FromHex("#5d6b7c"), EncephalographWindow.LobeColor(Region), _shown);
        var mid = Size.Y * 0.5f;
        handle.DrawLine(new Vector2(0, mid), new Vector2(Size.X, mid), grid);

        var amp = Violent
            ? 3f + _shown * (Size.Y * 0.42f)
            : 4f + _shown * (Size.Y * 0.08f);
        var freq = Violent
            ? 1.05f + _shown * 6.2f
            : 0.75f + _shown * 0.85f;
        var harmonic = Violent ? 0.12f + _shown * 0.9f : 0.18f;
        Vector2? prev = null;
        for (var x = 0f; x <= Size.X; x += 2f)
        {
            var wave = MathF.Sin((x * 0.08f + _phase) * freq) * amp;
            wave += MathF.Sin((x * 0.21f - _phase * 1.4f) * freq) * amp * harmonic;
            var point = new Vector2(x, Math.Clamp(mid + wave, 1f, Size.Y - 1f));
            if (prev != null)
                handle.DrawLine(prev.Value, point, ink);
            prev = point;
        }
    }
}

public sealed class BrainDiagram : Control
{
    private static readonly (float T, Color Color)[] HeatStops =
    [
        (0f, Color.FromHex("#163a9a")),
        (0.18f, Color.FromHex("#2a74d6")),
        (0.36f, Color.FromHex("#2f9d6a")),
        (0.52f, Color.FromHex("#7dce46")),
        (0.68f, Color.FromHex("#f4d03f")),
        (0.84f, Color.FromHex("#e67e22")),
        (1f, Color.FromHex("#d42525")),
    ];

    public Dictionary<PsychiatryBrainRegion, float> Activity = new();
    public bool Positronic;
    private readonly Dictionary<PsychiatryBrainRegion, float> _shown = new();
    private readonly Dictionary<PsychiatryBrainRegion, float> _vel = new();
    private readonly Texture _brainTex;
    private readonly Texture _cpuTex;
    private readonly Dictionary<PsychiatryBrainRegion, Texture> _zones = new();
    private readonly Dictionary<PsychiatryBrainRegion, Texture> _cpuZones = new();
    private readonly Font _font;

    public BrainDiagram()
    {
        Modulate = Color.White;
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/EngineFonts/NotoSans/NotoSans-Regular.ttf"), 11);
        _brainTex = cache.GetResource<TextureResource>("/Textures/_DeadSpace/Psychiatry/enceph/brain.png").Texture;
        _cpuTex = cache.GetResource<TextureResource>("/Textures/_DeadSpace/Psychiatry/enceph/cpu.png").Texture;
        foreach (var region in Enum.GetValues<PsychiatryBrainRegion>())
        {
            var name = region.ToString().ToLowerInvariant();
            _zones[region] = cache.GetResource<TextureResource>($"/Textures/_DeadSpace/Psychiatry/enceph/zone_{name}.png").Texture;
            _cpuZones[region] = cache.GetResource<TextureResource>($"/Textures/_DeadSpace/Psychiatry/enceph/cpu_zone_{name}.png").Texture;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var dt = Math.Clamp(args.DeltaSeconds, 0.0001f, 0.05f);
        const float omega = 5.5f;
        foreach (var region in Enum.GetValues<PsychiatryBrainRegion>())
        {
            Activity.TryGetValue(region, out var target);
            _shown.TryGetValue(region, out var shown);
            _vel.TryGetValue(region, out var vel);
            var accel = (target - shown) * (omega * omega) - vel * (2f * omega);
            vel += accel * dt;
            shown += vel * dt;
            if (shown < 0f)
            {
                shown = 0f;
                vel = 0f;
            }
            else if (shown > 1f)
            {
                shown = 1f;
                vel = 0f;
            }

            if (MathF.Abs(target - shown) < 0.002f && MathF.Abs(vel) < 0.02f)
            {
                shown = target;
                vel = 0f;
            }

            _shown[region] = shown;
            _vel[region] = vel;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Size.X < 16f || Size.Y < 64f)
            return;

        var mapBottom = Size.Y - 36f;
        var plate = Positronic ? _cpuTex : _brainTex;
        var zones = Positronic ? _cpuZones : _zones;
        var scale = MathF.Min(Size.X / plate.Size.X, mapBottom / plate.Size.Y);
        var w = plate.Size.X * scale;
        var h = plate.Size.Y * scale;
        var left = (Size.X - w) * 0.5f;
        var top = MathF.Max(0f, (mapBottom - h) * 0.5f);
        var box = new UIBox2(left, top, left + w, top + h);
        handle.DrawTextureRect(plate, box, Color.White);

        foreach (var region in Enum.GetValues<PsychiatryBrainRegion>())
        {
            _shown.TryGetValue(region, out var level);
            if (level < 0.004f || !zones.TryGetValue(region, out var zone))
                continue;

            var hot = Heat(Math.Clamp(0.22f + level * 0.78f, 0f, 1f));
            var color = new Color(hot.R, hot.G, hot.B, level * 0.82f);
            handle.DrawTextureRect(zone, box, color);
        }

        DrawScale(handle);
    }

    private static Color Heat(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        for (var i = 1; i < HeatStops.Length; i++)
        {
            if (t > HeatStops[i].T)
                continue;
            var prev = HeatStops[i - 1];
            var next = HeatStops[i];
            var span = next.T - prev.T;
            var u = span <= 0f ? 0f : (t - prev.T) / span;
            return EncephalographWindow.Mix(prev.Color, next.Color, u);
        }

        return HeatStops[^1].Color;
    }

    private void DrawScale(DrawingHandleScreen handle)
    {
        var bar = new UIBox2(8f, Size.Y - 18f, Size.X - 8f, Size.Y - 8f);
        var width = MathF.Max(1f, bar.Right - bar.Left);
        for (var x = bar.Left; x < bar.Right; x += 3f)
        {
            var t = 1f - (x - bar.Left) / width;
            handle.DrawRect(new UIBox2(x, bar.Top, MathF.Min(x + 3f, bar.Right), bar.Bottom), Heat(t));
        }

        handle.DrawString(_font, new Vector2(8f, Size.Y - 34f), Loc.GetString("psychiatry-enceph-scale-max"), Color.FromHex("#314056"));
        var min = Loc.GetString("psychiatry-enceph-scale-min");
        var dim = handle.GetDimensions(_font, min, 1f);
        handle.DrawString(_font, new Vector2(Size.X - 8f - dim.X, Size.Y - 34f), min, Color.FromHex("#314056"));
    }
}
