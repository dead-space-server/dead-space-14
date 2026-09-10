// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.Lavaland;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

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

    public LavalandSensorTabletWindow()
    {
        Title = "Планшет разведки лаваленда";
        MinSize = SetSize = new Vector2(720, 680);

        var legend = new Label
        {
            Text = "● шахтёр   ● погибший   ● фауна   ■ руда   ■ лава",
            HorizontalAlignment = Control.HAlignment.Center,
            Modulate = Color.LightGray,
        };
        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { legend, _status, _radar },
        };
        _radar.VerticalExpand = true;
        _radar.HorizontalExpand = true;
        Contents.AddChild(content);
    }

    public void SetState(LavalandSensorTabletState state)
    {
        _status.Visible = state.Points.Count == 0;
        _radar.SetPoints(state.Points);
    }
}

public sealed class LavalandRadarControl : Control
{
    private List<LavalandRadarPoint> _points = new();

    public LavalandRadarControl()
    {
        MinSize = new Vector2(640, 560);
        RectClipContent = true;
    }

    public void SetPoints(List<LavalandRadarPoint> points)
    {
        _points = points;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var bounds = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
        handle.DrawRect(bounds, Color.FromHex("#090604"));
        if (_points.Count == 0)
            return;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var point in _points)
        {
            min = Vector2.Min(min, point.Position);
            max = Vector2.Max(max, point.Position);
        }

        var span = Vector2.Max(max - min, new Vector2(20f));
        var scale = MathF.Min((PixelSize.X - 24f) / span.X, (PixelSize.Y - 24f) / span.Y);
        var used = span * scale;
        var margin = (PixelSize - used) / 2f;

        foreach (var point in _points)
        {
            var relative = (point.Position - min) * scale;
            var position = new Vector2(margin.X + relative.X, PixelSize.Y - margin.Y - relative.Y);
            var radius = MathF.Max(1.5f, scale * point.Size);
            if (point.Kind is LavalandRadarPointKind.Terrain or LavalandRadarPointKind.Rock or LavalandRadarPointKind.Ore)
            {
                handle.DrawRect(new UIBox2(position.X - radius, position.Y - radius,
                    position.X + radius, position.Y + radius), point.Color);
            }
            else
            {
                handle.DrawCircle(position, MathF.Max(3f, radius), point.Color);
            }
        }
    }
}
