// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Resources;
using Content.Client.Viewport;
using Content.Shared.DeadSpace.CameraArchives;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.CameraArchives;

public sealed class CameraArchiveControl : Control
{
    public readonly LineEdit Search;
    public readonly BoxContainer CameraRows;
    public readonly ScalingViewport View;
    public readonly TextureRect StaticView;
    public readonly Label FrameLabel;
    public readonly Button BackButton;
    public readonly Button ForwardButton;
    public readonly Button ZoomInButton;
    public readonly Button ZoomOutButton;
    public readonly Button PrintButton;
    public readonly Slider Frames;

    private readonly float[] _zoomSteps = [1f, 2f, 4f];
    private FixedEye? _eye;
    private Vector2 _baseZoom = Vector2.One;
    private Vector2 _pan;
    private int _zoomStep;

    public event Action<int>? CameraPicked;
    public event Action<int>? FramePicked;
    public event Action? PrintPressed;
    public event Action<string>? SearchChanged;

    private int _frameIndex;
    private int _frameCount;
    private readonly BoxContainer _chatRows;
    private readonly PanelContainer _chatPanel;

    private static readonly ProtoId<ShaderPrototype> CameraStaticShader = "CameraStatic";
    private readonly PanelContainer _cover;

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceCache _resources = default!;

    public CameraArchiveControl()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(720, 420);
        HorizontalExpand = true;
        VerticalExpand = true;

        Search = new LineEdit { PlaceHolder = Loc.GetString("camera-archive-name-search"), HorizontalExpand = true };
        Search.OnTextChanged += _ => SearchChanged?.Invoke(Search.Text);
        CameraRows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        var cameraScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
        };
        cameraScroll.AddChild(CameraRows);
        View = new ScalingViewport
        {
            MinSize = new Vector2(360, 280),
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        View.ViewportSize = View.PixelSize.X > 16 ? View.PixelSize : new Vector2i(480, 360);
        IoCManager.InjectDependencies(View);
        View.Visible = false;
        StaticView = new TextureRect
        {
            MinSize = new Vector2(500, 500),
            Stretch = TextureRect.StretchMode.Scale,
            VerticalExpand = true,
            HorizontalExpand = true,
            MouseFilter = Control.MouseFilterMode.Ignore,
            Texture = _resources.GetTexture("/Textures/Interface/Nano/square_black.png"),
            ShaderOverride = _prototypes.Index(CameraStaticShader).Instance().Duplicate(),
        };
        _cover = new PanelContainer
        {
            MinSize = new Vector2(500, 500),
            Visible = false,
            MouseFilter = Control.MouseFilterMode.Ignore,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.White },
        };

        FrameLabel = new Label();
        _chatRows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        var chatScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
            Margin = new Thickness(6, 0, 6, 6),
        };
        chatScroll.AddChild(_chatRows);
        _chatPanel = new PanelContainer
        {
            MinWidth = 260,
            HorizontalExpand = false,
            VerticalExpand = true,
            Visible = false,
            PanelOverride = new StyleBoxFlat(Color.FromHex("#121820")),
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("camera-archive-chat"),
                            Margin = new Thickness(8, 8, 8, 4),
                            FontColorOverride = Color.FromHex("#d7e6f5"),
                        },
                        chatScroll,
                    },
                },
            },
        };
        SetChat(null, false);
        BackButton = new Button { Text = Loc.GetString("camera-archive-back") };
        ForwardButton = new Button { Text = Loc.GetString("camera-archive-forward") };
        ZoomInButton = new Button { Text = Loc.GetString("camera-archive-zoom-in") };
        ZoomOutButton = new Button { Text = Loc.GetString("camera-archive-zoom-out") };
        PrintButton = new Button { Text = Loc.GetString("camera-archive-print") };
        Frames = new Slider { HorizontalExpand = true, MinHeight = 20, MinValue = 0, MaxValue = 1 };

        var drag = new FrameDrag
        {
            MouseFilter = Control.MouseFilterMode.Stop,
            VerticalExpand = true,
            HorizontalExpand = true,
            MinSize = new Vector2(500, 500),
        };
        drag.AddChild(View);
        drag.AddChild(StaticView);
        drag.AddChild(_cover);
        drag.Pan += delta =>
        {
            if (_eye == null || _zoomStep == 0)
                return;

            var step = _zoomSteps[_zoomStep];
            var meters = delta / (CameraArchiveViewMath.PixelsPerMeter * step);
            _pan -= new Vector2(meters.X, -meters.Y);
            ApplyEye();
        };

        BackButton.OnPressed += _ => FramePicked?.Invoke(Math.Max(0, _frameIndex - 1));
        ForwardButton.OnPressed += _ => FramePicked?.Invoke(Math.Min(_frameCount - 1, _frameIndex + 1));
        ZoomInButton.OnPressed += _ =>
        {
            _zoomStep = Math.Min(_zoomSteps.Length - 1, _zoomStep + 1);
            ApplyEye();
        };
        ZoomOutButton.OnPressed += _ =>
        {
            _zoomStep = Math.Max(0, _zoomStep - 1);
            ApplyEye();
        };
        PrintButton.OnPressed += _ => PrintPressed?.Invoke();
        Frames.OnReleased += slider => FramePicked?.Invoke((int) MathF.Round(slider.Value));

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    MinWidth = 280,
                    Children = { Search, cameraScroll },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    Children =
                    {
                        drag,
                        FrameLabel,
                        Frames,
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Horizontal,
                            Children = { BackButton, ForwardButton, ZoomOutButton, ZoomInButton, PrintButton },
                        },
                    },
                },
                _chatPanel,
            },
        });
    }

    public void SetChat(IReadOnlyList<string>? lines, bool show)
    {
        _chatPanel.Visible = show;
        if (!show)
            return;

        _chatRows.RemoveAllChildren();
        if (lines == null || lines.Count == 0)
        {
            _chatRows.AddChild(new Label
            {
                Text = Loc.GetString("camera-archive-chat-empty"),
                FontColorOverride = Color.FromHex("#7d8b99"),
                Margin = new Thickness(4, 8, 4, 4),
            });
            return;
        }

        foreach (var line in lines)
        {
            var split = line.Split(':', 2);
            var speaker = split.Length == 2 ? split[0].Trim() : string.Empty;
            var body = split.Length == 2 ? split[1].Trim() : line;
            var text = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(8, 6, 8, 6),
            };
            if (speaker.Length > 0)
            {
                text.AddChild(new Label
                {
                    Text = speaker,
                    FontColorOverride = Color.FromHex("#8ec8ff"),
                });
            }

            text.AddChild(new Label
            {
                Text = body,
                FontColorOverride = Color.FromHex("#f2f5f8"),
            });
            _chatRows.AddChild(new PanelContainer
            {
                Margin = new Thickness(0, 0, 0, 6),
                PanelOverride = new StyleBoxFlat(Color.FromHex("#1c2836"))
                {
                    ContentMarginLeftOverride = 0,
                    ContentMarginRightOverride = 0,
                },
                Children = { text },
            });
        }
    }

    public void ShowCameras(IReadOnlyList<(string Name, bool Offline, bool Burned, int Index)> rows)
    {
        CameraRows.RemoveAllChildren();
        foreach (var row in rows)
        {
            var button = new Button { HorizontalExpand = true, MinHeight = 28 };
            var text = row.Burned
                ? $"{row.Name} [color=#FF8844]{Loc.GetString("camera-archive-burned")}[/color]"
                : row.Offline
                    ? $"{row.Name} [color=#FF5555]Offline[/color]"
                    : row.Name;
            var message = new FormattedMessage();
            message.AddMarkupPermissive(text);
            var label = new RichTextLabel { MouseFilter = Control.MouseFilterMode.Ignore };
            label.SetMessage(message);
            button.AddChild(label);
            var index = row.Index;
            button.OnPressed += _ => CameraPicked?.Invoke(index);
            CameraRows.AddChild(button);
        }
    }

    public void ShowFrame(FixedEye eye, int index, int count, TimeSpan time)
    {
        var keepView = _eye != null && View.Visible;
        _cover.Visible = false;
        StaticView.Visible = false;
        View.Visible = true;
        _eye = eye;
        _baseZoom = eye.Zoom;
        if (!keepView)
        {
            _zoomStep = 0;
            _pan = Vector2.Zero;
        }
        _frameIndex = index;
        _frameCount = count;
        FrameLabel.Text = Loc.GetString("camera-archive-frame",
            ("time", time.ToString("hh\\:mm\\:ss")),
            ("index", index + 1),
            ("count", count));
        BackButton.Disabled = index <= 0;
        ForwardButton.Disabled = index >= count - 1;
        PrintButton.Disabled = count <= 0;
        Frames.MaxValue = Math.Max(1, count - 1);
        Frames.Value = Math.Clamp(index, 0, (int) Frames.MaxValue);
        Frames.Disabled = count < 2;
        ApplyEye();
    }

    public void ShowCover(int index, int count, TimeSpan time)
    {
        ShowStatic(index, count, time, count > 0);
        StaticView.Visible = false;
        _cover.Visible = true;
    }

    public void ShowStatic(int index, int count, TimeSpan time, bool hasFrame)
    {
        _cover.Visible = false;
        StaticView.Visible = true;
        View.Visible = false;
        _eye = null;
        if (!hasFrame)
        {
            FrameLabel.Text = string.Empty;
            BackButton.Disabled = true;
            ForwardButton.Disabled = true;
            PrintButton.Disabled = true;
            Frames.Disabled = true;
            ZoomOutButton.Disabled = true;
            ZoomInButton.Disabled = true;
            return;
        }

        _frameIndex = index;
        _frameCount = count;
        FrameLabel.Text = Loc.GetString("camera-archive-frame",
            ("time", time.ToString("hh\\:mm\\:ss")),
            ("index", index + 1),
            ("count", count));
        BackButton.Disabled = index <= 0;
        ForwardButton.Disabled = index >= count - 1;
        PrintButton.Disabled = count <= 0;
        Frames.MaxValue = Math.Max(1, count - 1);
        Frames.Value = Math.Clamp(index, 0, (int) Frames.MaxValue);
        Frames.Disabled = count < 2;
        ZoomOutButton.Disabled = true;
        ZoomInButton.Disabled = true;
    }

    private void ApplyEye()
    {
        if (_eye == null)
            return;

        var px = View.PixelSize;
        if (px.X > 16 && px.Y > 16)
            View.ViewportSize = px;

        var step = _zoomSteps[_zoomStep];
        var half = CameraArchiveViewMath.OriginalHalf(View.ViewportSize, _baseZoom);
        _pan = CameraArchiveViewMath.ClampPan(_pan, half, step);
        _eye.Zoom = CameraArchiveViewMath.EyeZoom(_baseZoom, step);
        _eye.Offset = _pan;
        View.Eye = _eye;
        ZoomOutButton.Disabled = _zoomStep <= 0;
        ZoomInButton.Disabled = _zoomStep >= _zoomSteps.Length - 1;
    }

    private sealed class FrameDrag : Control
    {
        public event Action<Vector2>? Pan;
        private bool _drag;

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            _drag = true;
            args.Handle();
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);
            _drag = false;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);
            if (!_drag)
                return;

            Pan?.Invoke(args.Relative);
        }
    }
}
