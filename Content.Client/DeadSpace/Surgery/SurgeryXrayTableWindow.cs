// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryXrayTableWindow : DefaultWindow
{
    public event Action<byte, byte[]?>? PrintRequested;
    public event Action<string>? StartRequested;

    private readonly RichTextLabel _patient;
    private readonly RichTextLabel _status;
    private readonly RichTextLabel _overview;
    private readonly RichTextLabel _focusTitle;
    private readonly RichTextLabel _focus;
    private readonly SurgeryScreenButton _mode;
    private readonly SurgeryPawnView _pawn;
    private readonly SurgeryPawnGlow _glow;
    private readonly SurgeryFilmView _film;
    private readonly SurgeryXrayClientSystem _xray;
    private readonly IEntityManager _ents;
    private NetEntity _body;
    private byte _zone = 255;
    private bool _skeleton;
    private OwnedTexture? _shot;

    public SurgeryXrayTableWindow()
    {
        Title = Loc.GetString("surgery-xray-table");
        MinSize = new Vector2(1100, 640);
        _ents = IoCManager.Resolve<IEntityManager>();
        _xray = _ents.System<SurgeryXrayClientSystem>();
        _pawn = new SurgeryPawnView { MinSize = new Vector2(460, 520) };
        _pawn.Picked += zone =>
        {
            _zone = zone == null ? (byte) 255 : (byte) zone.Value;
            WriteCard();
        };
        _glow = new SurgeryPawnGlow(_pawn);
        _film = new SurgeryFilmView { MinSize = new Vector2(460, 520), Visible = false };

        _mode = new SurgeryScreenButton(Loc.GetString("surgery-xray-button"));
        _mode.Pressed += () =>
        {
            _skeleton = !_skeleton;
            _mode.Caption = _skeleton ? Loc.GetString("surgery-xray-on") : Loc.GetString("surgery-xray-button");
            if (_skeleton)
                _film.Reset();
            ShowPicture();
        };
        var print = new SurgeryScreenButton(Loc.GetString("surgery-xray-print"));
        print.Pressed += () =>
        {
            byte[]? image = null;
            if (_skeleton && _ents.TryGetEntity(_body, out var patient) && patient is { } pawn)
                image = _xray.Film(pawn, (int) _film.PixelWidth, (int) _film.PixelHeight, _film.Pan, _film.Zoom);
            PrintRequested?.Invoke(_zone, image);
        };

        var bar = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        bar.AddChild(_mode);
        bar.AddChild(print);

        var stage = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SizeFlagsStretchRatio = 15,
        };
        _glow.HorizontalExpand = true;
        _glow.VerticalExpand = true;
        _film.HorizontalExpand = true;
        _film.VerticalExpand = true;
        stage.AddChild(_glow);
        stage.AddChild(_film);

        _patient = new RichTextLabel();
        _status = new RichTextLabel();
        _overview = new RichTextLabel();
        _focusTitle = new RichTextLabel();
        _focus = new RichTextLabel();
        var form = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinSize = new Vector2(0, 0),
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        form.AddChild(Mark("[font size=11][italic][color=#6a9bb8]МЕД[/color][/italic][/font]"));
        form.AddChild(Mark($"[font size=20][bold][color=#d7eeff]{Loc.GetString("surgery-xray-title")}[/color][/bold][/font]"));
        form.AddChild(_patient);
        form.AddChild(_status);
        form.AddChild(Mark($"[font size=13][bold][italic][color=#8ec4e0]{Loc.GetString("surgery-xray-overview")}[/color][/italic][/bold][/font]"));
        form.AddChild(_overview);
        form.AddChild(_focusTitle);
        form.AddChild(_focus);
        _running = new RichTextLabel();
        form.AddChild(_running);
        var search = new LineEdit { PlaceHolder = Loc.GetString("surgery-xray-find") };
        search.OnTextChanged += args =>
        {
            _query = args.Text;
            FillOps();
        };
        _opsPage = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Visible = false,
        };
        _opsPage.AddChild(search);
        _ops = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        _opsPage.AddChild(_ops);
        FillOps();
        var pages = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        pages.AddChild(form);
        pages.AddChild(_opsPage);
        var exam = new SurgeryScreenButton(Loc.GetString("surgery-xray-tab-exam"));
        var operations = new SurgeryScreenButton(Loc.GetString("surgery-xray-tab-ops"));
        exam.Pressed += () =>
        {
            form.Visible = true;
            _opsPage.Visible = false;
        };
        operations.Pressed += () =>
        {
            form.Visible = false;
            _opsPage.Visible = true;
        };
        var tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
        tabs.AddChild(exam);
        tabs.AddChild(operations);
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        column.AddChild(tabs);
        column.AddChild(pages);
        var scroll = new ScrollContainer
        {
            MinSize = new Vector2(0, 520),
            HorizontalExpand = true,
            VerticalExpand = true,
            SizeFlagsStretchRatio = 10,
        };
        scroll.AddChild(column);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        row.AddChild(stage);
        row.AddChild(scroll);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
        };
        root.AddChild(bar);
        root.AddChild(row);

        var shell = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#07131f"),
                ContentMarginLeftOverride = 16,
                ContentMarginRightOverride = 16,
                ContentMarginTopOverride = 14,
                ContentMarginBottomOverride = 14,
            },
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        shell.AddChild(root);
        Contents.AddChild(shell);
    }

    public void ShowState(SurgeryXrayTableState state)
    {
        _body = state.Patient;
        if (_ents.TryGetEntity(_body, out var body))
        {
            _pawn.SetEntity(body);
        }

        WriteCard();
        ShowPicture();
    }

    private void WriteCard()
    {
        if (!_ents.TryGetEntity(_body, out var body) || !_ents.TryGetComponent<LimbBodyComponent>(body.Value, out var limbs))
        {
            _patient.Text = MarkText($"[font size=13][italic][color=#8fb4c9]{Loc.GetString("surgery-xray-empty")}[/color][/italic][/font]");
            _status.Text = "";
            _overview.Text = "";
            _focusTitle.Text = "";
            _focus.Text = "";
            return;
        }

        var life = _ents.TryGetComponent<MobStateComponent>(body.Value, out var mob)
            ? SurgeryXrayReport.Life(mob.CurrentState)
            : SurgeryXrayReport.Life(default);
        _ents.TryGetComponent<SurgeryCavityComponent>(body.Value, out var hold);
        var zone = _zone == 255 ? (LimbZone?) null : (LimbZone) _zone;
        var card = SurgeryXrayReport.Make(limbs, hold, zone, life);
        var name = Safe(_ents.GetComponent<MetaDataComponent>(body.Value).EntityName);
        _patient.Text = $"[font size=18][bold][color=#f4f8fc]{name}[/color][/bold][/font]";
        _life = mob?.CurrentState ?? MobState.Alive;
        _status.Text = LifeText(card.Status);
        _overview.Text = $"[font size=12][color=#c9d8e6]{Safe(card.Overview)}[/color][/font]";
        _focusTitle.Text = $"[font size=15][bold][color=#7ec8ea]{Safe(card.FocusTitle)}[/color][/bold][/font]";
        _focus.Text = $"[font size=12][color=#e7f2f8]{Safe(card.Focus)}[/color][/font]";
        var running = Running(body.Value);
        _running.Text = running.Length == 0
            ? ""
            : $"[font size=13][bold][color=#f0c36a]{Safe(running)}[/color][/bold][/font]";
    }

    private readonly RichTextLabel _running;
    private readonly BoxContainer _ops;
    private readonly BoxContainer _opsPage;
    private string _query = "";

    private string Running(EntityUid body)
    {
        if (!_ents.TryGetComponent<SurgerySiteComponent>(body, out var site))
            return "";

        var id = "";
        if (site.Part.Length > 0
            && IoCManager.Resolve<IPrototypeManager>().TryIndex<SurgeryPartPrototype>(site.Part, out var part))
            id = part.Operation;
        if (id.Length == 0)
            id = site.Queued;
        if (id.Length == 0)
            return "";

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var title = prototypes.TryIndex<SurgeryOperationPrototype>(id, out var operation) && operation.Name.Length > 0
            ? Loc.GetString(operation.Name)
            : id;
        return Loc.GetString("surgery-xray-running", ("op", title));
    }

    private void FillOps()
    {
        _ops.RemoveAllChildren();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        foreach (var operation in prototypes.EnumeratePrototypes<SurgeryOperationPrototype>())
        {
            if (operation.Zone.Length == 0)
                continue;

            var title = operation.Name.Length > 0 ? Loc.GetString(operation.Name) : operation.ID;
            if (_query.Length > 0
                && title.IndexOf(_query, StringComparison.OrdinalIgnoreCase) < 0
                && operation.ID.IndexOf(_query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var id = operation.ID;
            var button = new SurgeryScreenButton(title);
            button.Pressed += () => StartRequested?.Invoke(id);
            _ops.AddChild(button);
        }
    }

    private MobState _life = MobState.Alive;
    private float _blink;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_life != MobState.Critical)
        {
            _status.ModulateSelfOverride = null;
            return;
        }

        _blink += args.DeltaSeconds;
        var on = (int) (_blink * 3f) % 2 == 0;
        _status.ModulateSelfOverride = on ? Color.White : new Color(1f, 0.25f, 0.25f);
    }

    private string LifeText(string status)
    {
        if (_life == MobState.Dead)
            return $"[font size=14][bold][color=#ff4d4d]{Safe(status)}[/color][/bold][/font]";

        if (_life == MobState.Critical)
            return $"[font size=14][bold][color=#ff6b6b]{Safe(status)}[/color][/bold][/font]";

        return $"[font size=13][italic][color=#9fd0ea]{Safe(status)}[/color][/italic][/font]";
    }

    private static RichTextLabel Mark(string markup)
    {
        return new RichTextLabel { Text = markup };
    }

    private static string MarkText(string markup)
    {
        return markup;
    }

    private static string Safe(string text)
    {
        return text.Replace("[", "(").Replace("]", ")");
    }

    private void ShowPicture()
    {
        _glow.Visible = !_skeleton;
        _film.Visible = _skeleton;
        _shot?.Dispose();
        _shot = null;
        _film.Shot = null;
        if (!_skeleton || !_ents.TryGetEntity(_body, out var body))
            return;

        _shot = _xray.Sheet(body.Value);
        _film.Shot = _shot;
    }

    protected override void Dispose(bool disposing)
    {
        _shot?.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class SurgeryPawnGlow : Control
{
    public SurgeryPawnGlow(Control child)
    {
        MinSize = child.MinSize;
        HorizontalExpand = true;
        VerticalExpand = true;
        child.HorizontalExpand = true;
        child.VerticalExpand = true;
        AddChild(child);
    }
}

public sealed class SurgeryPawnView : Control
{
    public event Action<LimbZone?>? Picked;

    private EntityUid? _body;
    private float _span = 32f;

    public SurgeryPawnView()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    public void SetEntity(EntityUid? body)
    {
        _body = body;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_body is not { } body)
            return;

        var ents = IoCManager.Resolve<IEntityManager>();
        if (!ents.TryGetComponent(body, out SpriteComponent? sprite))
            return;

        var sprites = ents.System<SpriteSystem>();
        var saved = sprite.Rotation;
        var target = MathF.Min(PixelWidth, PixelHeight) * 0.82f;
        _span = MathF.Max(32f, target);
        var scale = _span / EyeManager.PixelsPerMeter;
        sprites.SetRotation((body, sprite), Angle.Zero);
        handle.DrawEntity(body, PixelSize / 2f, new Vector2(scale), Angle.Zero, default, Direction.South, sprite);
        sprites.SetRotation((body, sprite), saved);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Picked?.Invoke(Hit(args.RelativePixelPosition));
        args.Handle();
    }

    private LimbZone? Hit(Vector2 pixel)
    {
        if (_span <= 0f)
            return null;

        var delta = pixel - PixelSize / 2f;
        var meters = new Vector2(delta.X / _span, -delta.Y / _span);
        var spot = new Vector2((meters.X + 0.5f) * 32f, (0.5f - meters.Y) * 32f);
        var zone = LimbDollLayout.SpriteHit(spot);
        if (zone is LimbZone.Eyes or LimbZone.Mouth)
            return LimbZone.Head;

        return zone;
    }
}

public sealed class SurgeryFilmView : Control
{
    public Texture? Shot;
    public Vector2 Pan;
    public float Zoom = 1f;
    private float _minZoom = 1f;
    private bool _fitted;
    private bool _drag;
    private Vector2 _last;

    public SurgeryFilmView()
    {
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    public void Reset()
    {
        Pan = Vector2.Zero;
        _fitted = false;
    }

    private void Fit()
    {
        var span = MathF.Min(PixelWidth, PixelHeight);
        if (span <= 1f)
            return;

        Zoom = span / 32f;
        _minZoom = Zoom;
        _fitted = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!_fitted)
            Fit();

        handle.DrawRect(new UIBox2(0f, 0f, PixelWidth, PixelHeight), Color.FromHex("#07131f"));
        if (Shot == null || Zoom <= 0f)
            return;

        var size = 32f * Zoom;
        var left = PixelWidth / 2f + Pan.X - size / 2f;
        var top = PixelHeight / 2f + Pan.Y - size / 2f;
        handle.DrawTextureRect(Shot, new UIBox2(left, top, left + size, top + size));
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        var factor = MathF.Pow(1.12f, args.Delta.Y);
        var next = MathF.Max(_minZoom, Math.Clamp(Zoom * factor, _minZoom, 28f));
        var mouse = args.RelativePixelPosition;
        var center = PixelSize / 2f;
        var before = (mouse - center - Pan) / Zoom;
        Zoom = next;
        Pan = mouse - center - before * Zoom;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _drag = true;
        _last = args.RelativePixelPosition;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _drag = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_drag)
            return;

        Pan += args.RelativePixelPosition - _last;
        _last = args.RelativePixelPosition;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _drag = false;
    }
}

public sealed class SurgeryScreenButton : PanelContainer
{
    public event Action? Pressed;
    private readonly RichTextLabel _label;

    public string Caption
    {
        set => _label.Text = $"[font size=14][bold][color=#e7f6ff]{value}[/color][/bold][/font]";
    }

    public SurgeryScreenButton(string caption)
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(148, 36);
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#10283c"),
            BorderColor = Color.FromHex("#7ec8ea"),
            BorderThickness = new Thickness(1),
        };
        _label = new RichTextLabel
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(12, 6),
        };
        Caption = caption;
        AddChild(_label);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Pressed?.Invoke();
        args.Handle();
    }
}
