// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.Lavaland;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

[UsedImplicitly]
public sealed class LavalandSensorTabletBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private LavalandSensorTabletWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<LavalandSensorTabletWindow>();
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is LavalandSensorTabletState radar)
            _window?.SetState(radar);
    }
}

public sealed class LavalandSensorTabletWindow : DefaultWindow
{
    private readonly LavalandRadarControl _radar = new();
    private readonly Label _status = new()
    {
        HorizontalAlignment = Control.HAlignment.Center,
        Text = "Нет активных координатных датчиков на лаваленде",
    };
    private readonly Label _coordinates = new() { Text = "X: ---  Y: ---" };

    public LavalandSensorTabletWindow()
    {
        Title = "Планшет разведки лаваленда";
        MinSize = new Vector2(720, 610);
        SetSize = new Vector2(720, 610);

        var legendTop = new BoxContainer
        {
            HorizontalAlignment = Control.HAlignment.Center,
        };
        var legendBottom = new BoxContainer { HorizontalAlignment = Control.HAlignment.Center };
        AddLegend(legendTop, "● шахтёр", Color.White);
        AddLegend(legendTop, "● погибший", Color.Black);
        AddLegend(legendTop, "● фауна", Color.Red);
        AddLegend(legendBottom, "■ руда", Color.Gold);
        AddLegend(legendBottom, "■ лава", Color.OrangeRed);
        AddLegend(legendBottom, "■ база", new Color(115, 210, 230));
        AddLegend(legendBottom, "◯ моркит", new Color(40, 220, 190));
        var recenter = new Button { Text = "Центрировать карту" };
        recenter.OnPressed += _ => _radar.Recenter();
        var searchToggle = new Button { Text = "Зоны поиска", ToggleMode = true, Pressed = true, Margin = new Thickness(0, 0, 4, 0) };
        searchToggle.OnToggled += args => _radar.ShowSearchAreas = args.Pressed;
        var gridToggle = new Button { Text = "Сетка", ToggleMode = true, Pressed = true, Margin = new Thickness(0, 0, 4, 0) };
        gridToggle.OnToggled += args => _radar.ShowGrid = args.Pressed;
        var sweepToggle = new Button { Text = "Луч", ToggleMode = true, Pressed = true, Margin = new Thickness(0, 0, 4, 0) };
        sweepToggle.OnToggled += args => _radar.ShowSweep = args.Pressed;
        _radar.CoordinatesChanged += coordinates =>
            _coordinates.Text = $"X: {coordinates.X,7:0.0}  Y: {coordinates.Y,7:0.0}";
        var hint = new Label { Text = "Колесо — масштаб · ЛКМ с перетаскиванием — перемещение", HorizontalAlignment = Control.HAlignment.Center };
        var footer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children =
            {
                _coordinates,
                new Control { HorizontalExpand = true },
                searchToggle,
                gridToggle,
                sweepToggle,
                recenter,
            },
        };
        var frameStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#07100e"),
            BorderColor = Color.FromHex("#b96b2d"),
            BorderThickness = new Thickness(2),
            ContentMarginLeftOverride = 5,
            ContentMarginRightOverride = 5,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 5,
        };
        var radarFrame = new PanelContainer
        {
            PanelOverride = frameStyle,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { _radar },
        };
        var footerFrame = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#17110c"),
                BorderColor = Color.FromHex("#75431f"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            },
            Children = { footer },
        };
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children =
            {
                new Label
                {
                    Text = "\u25C8 ГЕОРАДАР ЛАВАЛЕНДА  \u00B7  КАНАЛ КОСТЮМНЫХ ДАТЧИКОВ",
                    HorizontalAlignment = Control.HAlignment.Center,
                    FontColorOverride = Color.FromHex("#ffb56b"),
                    Margin = new Thickness(0, 0, 0, 4),
                },
                legendTop, legendBottom, hint, _status, radarFrame, footerFrame,
            },
        };
        _radar.VerticalExpand = true;
        _radar.HorizontalExpand = true;
        var tabletBody = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#57240f"),
                BorderColor = Color.FromHex("#d8792f"),
                BorderThickness = new Thickness(4),
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 10,
                ContentMarginBottomOverride = 10,
            },
            Children = { content },
        };
        Contents.AddChild(tabletBody);
    }

    private static void AddLegend(BoxContainer legend, string text, Color color)
    {
        var swatch = new PanelContainer
        {
            MinSize = new Vector2(10, 10),
            VerticalAlignment = Control.VAlignment.Center,
            Margin = new Thickness(6, 0, 4, 0),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                BorderColor = color.WithAlpha(0.45f),
                BorderThickness = new Thickness(1),
            },
        };
        legend.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0d1714"),
                BorderColor = Color.FromHex("#2b4a3e"),
                BorderThickness = new Thickness(1),
            },
            Margin = new Thickness(3),
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children =
                    {
                        swatch,
                        new Label
                        {
                            Text = text.TrimStart('\u25CF', '\u25A0', '\u25EF', ' '),
                            FontColorOverride = Color.FromHex("#cfe8dc"),
                            Margin = new Thickness(0, 2, 8, 2),
                        },
                    },
                },
            },
        });
    }

    public void SetState(LavalandSensorTabletState state)
    {
        _status.Visible = !state.Points.Exists(p => p.Kind is LavalandRadarPointKind.LivingMiner or LavalandRadarPointKind.DeadMiner);
        if (state.BasePoints != null)
            _radar.SetBasePoints(state.BasePoints);
        _radar.SetPoints(state.Points);
        _radar.Storm = state.Storm;
    }
}

public sealed class LavalandRadarControl : MapGridControl
{
    private List<LavalandRadarPoint> _points = new();
    private List<LavalandRadarPoint> _basePoints = new();
    private bool _initialized;
    private bool _dragging;
    private float _interferenceTime;
    private Vector2? _mouse;
    public event Action<Vector2>? CoordinatesChanged;

    /// <summary>Morkite search circles. The Celestial interference is always shown.</summary>
    public bool ShowSearchAreas = true;

    /// <summary>The ash storm: 0 - calm, 1 - coming, 2 - raging.</summary>
    public byte Storm;

    /// <summary>World grid and range rings.</summary>
    public bool ShowGrid = true;

    /// <summary>The rotating radar beam.</summary>
    public bool ShowSweep = true;
    protected override bool Draggable => true;

    public LavalandRadarControl() : base(4f, 600f, 48f)
    {
        MinSize = new Vector2(650, 360);
        SetSize = new Vector2(650, 360);
        RectClipContent = true;
    }

    /// <summary>The outpost plan: kept until the server sends a changed one.</summary>
    public void SetBasePoints(List<LavalandRadarPoint> points)
    {
        _basePoints = points;
    }

    public void SetPoints(List<LavalandRadarPoint> points)
    {
        _points = points;
        if (!_initialized && _points.Count + _basePoints.Count > 0)
        {
            Recenter();
            _initialized = true;
        }
    }

    public void Recenter()
    {
        if (_points.Count + _basePoints.Count == 0)
            return;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var point in AllPoints())
        {
            min = Vector2.Min(min, point.Position - new Vector2(point.Size));
            max = Vector2.Max(max, point.Position + new Vector2(point.Size));
        }
        Offset = (min + max) / 2;
        WorldRange = ActualRadarRange = Math.Clamp(MathF.Max(max.X - min.X, max.Y - min.Y) * 0.6f, 12f, 600f);
    }

    private IEnumerable<LavalandRadarPoint> AllPoints()
    {
        foreach (var point in _basePoints)
            yield return point;
        foreach (var point in _points)
            yield return point;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.Use)
        {
            _dragging = true;
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick || args.Function == EngineKeyFunctions.Use)
            _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        _mouse = args.RelativePixelPosition;
        if (_dragging)
            Offset -= new Vector2(args.Relative.X, -args.Relative.Y) / GetScale();
        var relative = args.RelativePosition - PixelSize / 2;
        CoordinatesChanged?.Invoke(Offset + new Vector2(relative.X, -relative.Y) / GetScale());
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _dragging = false;
        _mouse = null;
    }

    private float GetScale() => MathF.Min(PixelSize.X, PixelSize.Y) / (MathF.Max(WorldRange, 0.1f) * 2);

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _interferenceTime += args.DeltaSeconds;
    }

    private static readonly Color Screen = Color.FromHex("#04100c");
    private static readonly Color Phosphor = Color.FromHex("#4fe0a8");

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var bounds = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
        var center = (Vector2) PixelSize / 2f;
        handle.DrawRect(bounds, Screen);
        var scale = GetScale();
        var glitch = _interferenceTime % 7f > 6.55f;
        LavalandRadarPoint? hovered = null;
        var hoveredPosition = Vector2.Zero;
        var hoveredDistance = 10f;

        if (ShowGrid)
        {
            DrawWorldGrid(handle, scale);
            DrawRangeRings(handle, center);
        }

        foreach (var point in AllPoints())
        {
            var relative = (point.Position - Offset) * scale;
            var position = PixelSize / 2 + new Vector2(relative.X, -relative.Y);

            // Only what lands on the screen is drawn; wide circles keep a margin of their size.
            var reach = MathF.Max(24f, scale * point.Size * 1.2f);
            if (position.X < -reach || position.Y < -reach ||
                position.X > PixelSize.X + reach || position.Y > PixelSize.Y + reach)
            {
                continue;
            }

            if (glitch)
                position.X += MathF.Round(MathF.Sin(MathF.Floor(position.Y / 16f) * 2.1f + _interferenceTime * 35f) * 5f);
            var radius = MathF.Max(1.5f, scale * point.Size);
            switch (point.Kind)
            {
                case LavalandRadarPointKind.CelestialNoise:
                    // Always shown: the toggle only hides the morkite search circles.
                    DrawCelestialNoise(handle, position, MathF.Max(16f, scale * point.Size), point.Color);
                    break;

                case LavalandRadarPointKind.MorkiteSearchArea:
                    if (!ShowSearchAreas)
                        break;
                    DrawMorkiteHint(handle, position, MathF.Max(12f, scale * point.Size), point.Color);
                    break;

                case LavalandRadarPointKind.LivingMiner:
                case LavalandRadarPointKind.DeadMiner:
                case LavalandRadarPointKind.Fauna:
                    DrawMob(handle, point, position, MathF.Max(3f, radius), glitch);
                    break;

                case LavalandRadarPointKind.Beacon:
                    DrawBeacon(handle, position, point.Color);
                    if (_mouse is { } mouse && (mouse - position).Length() is var distance && distance < hoveredDistance)
                    {
                        hovered = point;
                        hoveredPosition = position;
                        hoveredDistance = distance;
                    }
                    break;

                case LavalandRadarPointKind.BaseWall:
                    handle.DrawRect(new UIBox2(position.X - radius - 1.5f, position.Y - radius - 1.5f,
                        position.X + radius + 1.5f, position.Y + radius + 1.5f), point.Color.WithAlpha(0.18f));
                    handle.DrawRect(new UIBox2(position.X - radius, position.Y - radius,
                        position.X + radius, position.Y + radius), point.Color);
                    break;

                default:
                    handle.DrawRect(new UIBox2(position.X - radius, position.Y - radius,
                        position.X + radius, position.Y + radius), point.Color.WithAlpha(point.Color.A * 0.85f));
                    break;
            }
        }

        if (ShowSweep)
            DrawSweep(handle, center);
        DrawInterference(handle);
        DrawStorm(handle);
        DrawScanlines(handle);
        DrawVignette(handle);
        DrawCorners(handle);

        if (hovered?.Label is { } label)
            DrawBeaconTooltip(handle, hoveredPosition, label);
    }

    /// <summary>A marker beacon: a small diamond with a glow.</summary>
    private void DrawBeacon(DrawingHandleScreen handle, Vector2 position, Color color)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(_interferenceTime * 3f + position.Y * 0.07f);
        handle.DrawCircle(position, 7f + pulse * 2f, color.WithAlpha(0.18f));
        var size = 5f;
        var top = position + new Vector2(0, -size);
        var right = position + new Vector2(size, 0);
        var bottom = position + new Vector2(0, size);
        var left = position + new Vector2(-size, 0);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, new[] { top, right, bottom, left }, color);
        handle.DrawLine(top, right, Color.White.WithAlpha(0.8f));
        handle.DrawLine(right, bottom, Color.White.WithAlpha(0.8f));
        handle.DrawLine(bottom, left, Color.White.WithAlpha(0.8f));
        handle.DrawLine(left, top, Color.White.WithAlpha(0.8f));
    }

    /// <summary>The beacon message in a box next to the mouse, wrapped to a few lines.</summary>
    private void DrawBeaconTooltip(DrawingHandleScreen handle, Vector2 anchor, string text)
    {
        var font = UserInterfaceManager.ThemeDefaults.DefaultFont;
        const float maxWidth = 260f;
        const float padding = 6f;

        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = string.Empty;
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && handle.GetDimensions(font, candidate, 1f).X > maxWidth)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
        }

        var lineHeight = font.GetLineHeight(1f);
        var width = 0f;
        foreach (var line in lines)
            width = MathF.Max(width, handle.GetDimensions(font, line, 1f).X);

        var size = new Vector2(width + padding * 2, lineHeight * lines.Count + padding * 2);
        var origin = anchor + new Vector2(12, -size.Y - 6);
        origin.X = Math.Clamp(origin.X, 0, MathF.Max(0, PixelSize.X - size.X));
        origin.Y = Math.Clamp(origin.Y, 0, MathF.Max(0, PixelSize.Y - size.Y));

        var box = new UIBox2(origin, origin + size);
        handle.DrawRect(box, Color.FromHex("#04100c").WithAlpha(0.92f));
        handle.DrawRect(box, Phosphor, false);
        for (var i = 0; i < lines.Count; i++)
            handle.DrawString(font, origin + new Vector2(padding, padding + lineHeight * i), lines[i], 1f, Phosphor);
    }

    private void DrawMob(DrawingHandleScreen handle, LavalandRadarPoint point, Vector2 position, float radius, bool glitch)
    {
        if (glitch)
        {
            handle.DrawCircle(position + new Vector2(-3, 0), radius, new Color(235, 65, 65, 90));
            handle.DrawCircle(position + new Vector2(3, 0), radius, new Color(65, 210, 235, 90));
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(_interferenceTime * 4f + position.X * 0.05f);
        switch (point.Kind)
        {
            case LavalandRadarPointKind.LivingMiner:
                handle.DrawCircle(position, radius * (2.4f + pulse * 0.8f), Phosphor.WithAlpha(0.12f));
                handle.DrawCircle(position, radius + 1.5f, Phosphor);
                handle.DrawCircle(position, radius, point.Color);
                break;

            case LavalandRadarPointKind.DeadMiner:
                handle.DrawCircle(position, radius + 1.5f, Color.LightGray);
                handle.DrawCircle(position, radius, point.Color);
                handle.DrawLine(position - new Vector2(radius, radius), position + new Vector2(radius, radius), Color.LightGray);
                handle.DrawLine(position + new Vector2(-radius, radius), position + new Vector2(radius, -radius), Color.LightGray);
                break;

            default:
                handle.DrawCircle(position, radius * (1.8f + pulse * 0.6f), point.Color.WithAlpha(0.15f));
                handle.DrawCircle(position, radius, point.Color);
                break;
        }
    }

    /// <summary>World grid every 10 tiles, brighter every 50.</summary>
    private void DrawWorldGrid(DrawingHandleScreen handle, float scale)
    {
        const float step = 10f;
        if (scale * step < 6f)
            return;

        var half = (Vector2) PixelSize / 2f / scale;
        var min = Offset - half;
        var max = Offset + half;
        for (var x = MathF.Floor(min.X / step) * step; x <= max.X; x += step)
        {
            var px = PixelSize.X / 2 + (x - Offset.X) * scale;
            var major = MathF.Abs(x % 50f) < 0.01f;
            handle.DrawLine(new Vector2(px, 0), new Vector2(px, PixelSize.Y), Phosphor.WithAlpha(major ? 0.13f : 0.05f));
        }

        for (var y = MathF.Floor(min.Y / step) * step; y <= max.Y; y += step)
        {
            var py = PixelSize.Y / 2 - (y - Offset.Y) * scale;
            var major = MathF.Abs(y % 50f) < 0.01f;
            handle.DrawLine(new Vector2(0, py), new Vector2(PixelSize.X, py), Phosphor.WithAlpha(major ? 0.13f : 0.05f));
        }
    }

    private void DrawRangeRings(DrawingHandleScreen handle, Vector2 center)
    {
        var max = MathF.Min(PixelSize.X, PixelSize.Y) / 2f;
        for (var i = 1; i <= 4; i++)
        {
            handle.DrawCircle(center, max * i / 4f, Phosphor.WithAlpha(0.10f), false);
        }

        handle.DrawLine(new Vector2(center.X, 0), new Vector2(center.X, PixelSize.Y), Phosphor.WithAlpha(0.08f));
        handle.DrawLine(new Vector2(0, center.Y), new Vector2(PixelSize.X, center.Y), Phosphor.WithAlpha(0.08f));
    }

    /// <summary>A rotating radar beam with a fading trail.</summary>
    private void DrawSweep(DrawingHandleScreen handle, Vector2 center)
    {
        var length = ((Vector2) PixelSize).Length() / 2f;
        var angle = _interferenceTime * 1.3f;
        const int trail = 28;
        for (var i = 0; i < trail; i++)
        {
            var a = angle - i * 0.025f;
            var end = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * length;
            handle.DrawLine(center, end, Phosphor.WithAlpha(0.28f * (1f - i / (float) trail)));
        }
    }

    private void DrawScanlines(DrawingHandleScreen handle)
    {
        for (var y = 0f; y < PixelSize.Y; y += 3f)
        {
            handle.DrawRect(new UIBox2(0, y, PixelSize.X, y + 1f), new Color(0f, 0f, 0f, 0.16f));
        }
    }

    private void DrawVignette(DrawingHandleScreen handle)
    {
        const int bands = 10;
        var color = new Color(0f, 0f, 0f, 0.07f);
        for (var i = 0; i < bands; i++)
        {
            var inset = i * 5f;
            handle.DrawRect(new UIBox2(inset, inset, PixelSize.X - inset, inset + 5f), color);
            handle.DrawRect(new UIBox2(inset, PixelSize.Y - inset - 5f, PixelSize.X - inset, PixelSize.Y - inset), color);
            handle.DrawRect(new UIBox2(inset, inset + 5f, inset + 5f, PixelSize.Y - inset - 5f), color);
            handle.DrawRect(new UIBox2(PixelSize.X - inset - 5f, inset + 5f, PixelSize.X - inset, PixelSize.Y - inset - 5f), color);
        }
    }

    private void DrawCorners(DrawingHandleScreen handle)
    {
        const float len = 22f;
        const float pad = 6f;
        var color = Color.FromHex("#ffb56b").WithAlpha(0.8f);
        var w = PixelSize.X;
        var h = PixelSize.Y;
        var corners = new[]
        {
            (new Vector2(pad, pad), 1f, 1f),
            (new Vector2(w - pad, pad), -1f, 1f),
            (new Vector2(pad, h - pad), 1f, -1f),
            (new Vector2(w - pad, h - pad), -1f, -1f),
        };
        foreach (var (corner, dx, dy) in corners)
        {
            handle.DrawLine(corner, corner + new Vector2(len * dx, 0), color);
            handle.DrawLine(corner, corner + new Vector2(0, len * dy), color);
        }
    }

    /// <summary>Purple static over a wide circle: somewhere inside is the Celestial pit.</summary>
    private void DrawCelestialNoise(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        var frame = (int) (_interferenceTime * 12f);
        for (var i = 0; i < 160; i++)
        {
            var hash = (uint) (i * 2654435761u ^ (uint) frame * 40503u);
            var angle = (hash & 0xFFFF) / 65535f * MathF.Tau;
            var distance = MathF.Sqrt(((hash >> 16) & 0xFFFF) / 65535f) * radius;
            var dot = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            var dotSize = 1f + (hash >> 29);
            handle.DrawRect(new UIBox2(dot.X - dotSize, dot.Y - dotSize, dot.X + dotSize, dot.Y + dotSize),
                color.WithAlpha(color.A * (0.35f + ((hash >> 8) & 0xFF) / 400f)));
        }

        const int segments = 32;
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            var jitter = 1f + 0.06f * MathF.Sin(i * 5.1f + _interferenceTime * 9f);
            var current = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * jitter;
            handle.DrawLine(previous, current, color);
            previous = current;
        }
    }

    private void DrawMorkiteHint(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        const int segments = 18;
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            var wobble = 1f + 0.12f * MathF.Sin(i * 2.73f + _interferenceTime * 1.7f);
            var current = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * wobble;
            handle.DrawLine(previous, current, color);
            previous = current;
        }
    }

    private static readonly Color StormPink = Color.FromHex("#ff5c8a");
    private static readonly Color StormRed = Color.FromHex("#e0283f");

    /// <summary>
    /// The ash storm over the whole screen in pink and red: a haze and streaks of ash blowing across it -
    /// faint while it is coming, thick while it rages - and its name at the top.
    /// </summary>
    private void DrawStorm(DrawingHandleScreen handle)
    {
        if (Storm == 0)
            return;

        var raging = Storm >= 2;
        var w = PixelSize.X;
        var h = PixelSize.Y;
        var pulse = 0.5f + 0.5f * MathF.Sin(_interferenceTime * (raging ? 3f : 1.5f));
        handle.DrawRect(new UIBox2(0, 0, w, h), StormRed.WithAlpha((raging ? 0.16f : 0.06f) + pulse * 0.04f));

        // Streaks of ash driven across the screen by the wind.
        var count = raging ? 70 : 24;
        var speed = raging ? 260f : 110f;
        for (var i = 0; i < count; i++)
        {
            var seed = unchecked((uint) (i * 2654435761u));
            var lane = (seed & 0xFFFF) / 65535f;
            var offset = ((seed >> 16) & 0xFFFF) / 65535f;
            var length = (raging ? 26f : 14f) + (seed % 18);
            var x = ((offset * (w + 200f) + _interferenceTime * speed * (0.7f + lane * 0.6f)) % (w + 200f)) - 100f;
            var y = lane * h + MathF.Sin(_interferenceTime * 2f + i) * 6f;
            var color = (i % 3 == 0 ? StormRed : StormPink).WithAlpha(raging ? 0.55f : 0.3f);
            handle.DrawLine(new Vector2(x, y), new Vector2(x + length, y + length * 0.35f), color);
        }

        var label = raging ? "ПЕПЕЛЬНАЯ БУРЯ" : "ПРИБЛИЖАЕТСЯ БУРЯ";
        var font = UserInterfaceManager.ThemeDefaults.DefaultFont;
        var labelWidth = handle.GetDimensions(font, label, 1f).X;
        handle.DrawString(font, new Vector2((w - labelWidth) / 2f, 10f), label, 1f,
            (raging ? StormRed : StormPink).WithAlpha(0.7f + pulse * 0.3f));
    }

    private void DrawInterference(DrawingHandleScreen handle)
    {
        // Quantized static and brief block dropouts imitate an unstable low-resolution display.
        var phase = (int) (_interferenceTime * 12f);
        var burst = _interferenceTime % 7f > 6.55f;
        for (var i = 0; i < (burst ? 180 : 20); i++)
        {
            var seed = unchecked(phase * 1103515245 + i * 12345);
            seed ^= seed >> 13;
            seed = unchecked(seed * 1274126177);
            var positive = seed & int.MaxValue;
            var y = positive % Math.Max(1, (int) PixelSize.Y) / 4 * 4;
            var x = (positive >> 10) % Math.Max(1, (int) PixelSize.X) / 4 * 4;
            var size = burst && i % 17 == 0 ? 16 : 2;
            var color = i % 3 == 0
                ? new Color(0, 0, 0, burst ? (byte) 85 : (byte) 20)
                : new Color(100, 195, 155, burst ? (byte) 65 : (byte) 18);
            handle.DrawRect(new UIBox2(x, y, Math.Min(PixelSize.X, x + size),
                Math.Min(PixelSize.Y, y + size)), color);
        }
    }
}
