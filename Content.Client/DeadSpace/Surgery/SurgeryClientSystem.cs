// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.DragDrop;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryClientSystem : EntitySystem
{
    private const string LayerKey = "surgery-wound";
    private static readonly ResPath Rsi = new("/Textures/_DeadSpace/Mobs/Surgery/human_male.rsi");

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private const float NearDistance = 2.5f;

    private static readonly ResPath TorsoRsi = new("/Textures/Mobs/Species/Human/parts.rsi");
    private readonly Dictionary<EntityUid, string> _holes = new();

    private readonly SurgeryGuideOverlay _guide = new();

    private SurgeryBoardMessage? _board;
    private EntityUid? _patient;
    private string _markPart = "";
    private EntityUid? _headBody;
    private EntityUid? _picking;
    private EntityUid? _stay;
    private Control? _headMenu;
    private float _cutLo;
    private float _cutHi;
    private EntityUid? _tool;
    private SurgeryVerbKind _verb;
    private SurgeryVerbKind _step;
    private SurgeryGameKind _game;
    private SurgeryChestMap? _map;
    private SurgeryStageView? _stage;
    private SurgeryHint? _hint;
    private bool _windowMode;
    private bool _workPick;
    private bool _subscribed;
    private int _passes;
    private float _sawSign;
    private float _twist;
    private byte _layer;
    private byte _organ = 255;
    private bool _zoomed;
    private bool _armed;
    private bool _down;
    private float _frontier;
    private int _slips;
    private float _off;
    private float _left;
    private float _right;
    private int _grab;
    private bool _didPull;
    private float _nickWait;
    private bool _cutSent;
    private float _time;
    private readonly List<(EntityUid Body, Vector2 Local, Vector2 Dir)> _nicks = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
        UpdatesAfter.Add(typeof(PullingSystem));
        base.Initialize();
        SubscribeNetworkEvent<SurgeryBoardMessage>(OnBoard);
        SubscribeLocalEvent<ContentEyeComponent, GetEyeOffsetEvent>(OnEye);
        SubscribeLocalEvent<LimbBodyComponent, CanDragEvent>(OnCanDrag, after: [typeof(SharedBodySystem)]);
        SubscribeLocalEvent<SurgerySiteComponent, AfterAutoHandleStateEvent>(OnSite);
        _overlays.AddOverlay(_guide);
        _guide.Moved = () =>
        {
            if (_patient != null)
                PlaceMap(_patient.Value);
        };
        _cfg.OnValueChanged(CCCCVars.SurgeryWindow, OnSurgeryWindow, true);
    }

    private void OnSurgeryWindow(bool value)
    {
        _windowMode = value;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCCCVars.SurgeryWindow, OnSurgeryWindow);
        _overlays.RemoveOverlay(_guide);
        EndView();
    }

    public override void FrameUpdate(float frameTime)
    {
        PlaceNicks();
        HoldHoles();
        if (_board == null || _patient == null || _players.LocalEntity is not { } user)
        {
            if (_players.LocalEntity is { } aimer)
            {
                if (_picking != null)
                    Pick(aimer);
                else if (MarkHand(aimer, out _, out _))
                    Aim(aimer);
                else if (_stay is { } stay && Exists(stay))
                    Stay(stay);
                else
                    _guide.Active = false;
            }
            else
                _guide.Active = false;
            return;
        }

        if (!TryGetEntity(_board.Patient, out var patient) || !Exists(patient.Value))
        {
            EndView();
            return;
        }

        _time += frameTime;
        var here = _transform.GetWorldPosition(user);
        var there = _transform.GetWorldPosition(patient.Value);
        if (!InReach(here, there))
        {
            EndView();
            return;
        }

        var onTable = TryComp<BuckleComponent>(patient.Value, out var buckle) && buckle.Buckled;
        if (!onTable && !_standing.IsDown(patient.Value))
        {
            EndView();
            return;
        }

        _tool = _hands.TryGetActiveItem(user, out var held) && HasComp<SurgeryToolComponent>(held) ? held : null;
        if (_board != null && _board.StepVerb != 255)
        {
            var step = (SurgeryVerbKind) _board.StepVerb;
            var close = _board.CloseVerb == 255 ? SurgeryVerbKind.Inspect : (SurgeryVerbKind) _board.CloseVerb;
            var undo = _board.UndoVerb == 255 ? SurgeryVerbKind.Inspect : (SurgeryVerbKind) _board.UndoVerb;
            _step = step;
            if (_tool != null && ToolHas(step))
                _verb = step;
            else if (_tool != null && undo != SurgeryVerbKind.Inspect && ToolHas(undo))
                _verb = undo;
            else if (_tool != null && close != SurgeryVerbKind.Inspect && ToolHas(close))
                _verb = close;
            else
                _verb = SurgeryVerbKind.Inspect;
            _game = _verb == step || _verb == close || _verb == undo
                ? _board.StepGame == 255 || _verb != step ? GameOf(_verb) : (SurgeryGameKind) _board.StepGame
                : SurgeryGameKind.Cut;
            if (_verb == SurgeryVerbKind.Retract)
                _game = SurgeryGameKind.Spread;
            if (_verb == SurgeryVerbKind.Cauterize)
                _game = SurgeryGameKind.Cut;
        }

        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
        if (_windowMode && _stage != null)
            down = _stage.Down;
        if (_input.IsKeyDown(Keyboard.Key.MouseRight))
        {
            EndView();
            return;
        }

        if (down && !_down && Lodging(user))
            Send(1f, 0, false, SurgeryVerbKind.Insert);

        Trace(patient.Value, down, frameTime);
        _down = down;
        ShowGuide(patient.Value);
        Paint(patient.Value, VisualAmount());
        WorkUi();
        PlaceMap(patient.Value);
    }

    private void OnCanDrag(Entity<LimbBodyComponent> ent, ref CanDragEvent args)
    {
        if (_players.LocalEntity is not { } user)
            return;

        if (!_hands.TryGetActiveItem(user, out var held) || !HasComp<SurgeryToolComponent>(held))
            return;

        var buckled = TryComp<BuckleComponent>(ent, out var buckle) && buckle.Buckled;
        if (!buckled && !_standing.IsDown(ent.Owner))
            return;

        args.Handled = false;
    }

    private void OnEye(EntityUid uid, ContentEyeComponent comp, ref GetEyeOffsetEvent args)
    {
        if (!_zoomed || uid != _players.LocalEntity || _patient is not { } patient)
            return;

        args.Offset += _transform.GetWorldPosition(patient) - _transform.GetWorldPosition(uid);
    }

    private void OnBoard(SurgeryBoardMessage message, EntitySessionEventArgs args)
    {
        if (_players.LocalEntity is not { } user || !TryGetEntity(message.Patient, out var patient))
            return;

        if (message.Play == 0)
        {
            if (_patient == patient)
            {
                _board = message;
                _step = message.StepVerb == 255 ? StepOf(message) : (SurgeryVerbKind) message.StepVerb;
                var next = StepOf(message);
                if (_verb != next)
                {
                    _verb = next;
                    _game = message.StepGame == 255 ? GameOf(next) : (SurgeryGameKind) message.StepGame;
                    _armed = false;
                    _frontier = OpenAmount(message);
                    _grab = 0;
                    _didPull = false;
                    _left = 0f;
                    _right = 0f;
                }

                _layer = message.StepLayer != 255 ? message.StepLayer : _layer;
                if (!_armed)
                    Paint(patient.Value, OpenAmount(message));
            }

            return;
        }

        _board = message;
        var switched = _patient != patient;
        _patient = patient;
        _tool = _hands.TryGetActiveItem(user, out var held) ? held : null;
        _organ = message.Organs.Length > 0 ? message.Organs[0] : (byte) 255;
        var verb = StepOf(message);
        _step = message.StepVerb == 255 ? verb : (SurgeryVerbKind) message.StepVerb;
        var continuing = !switched && _zoomed && _verb == verb;
        _verb = verb;
        _game = message.StepGame == 255 ? GameOf(verb) : (SurgeryGameKind) message.StepGame;
        _layer = message.StepLayer != 255 ? message.StepLayer : (byte) Math.Max(0, Top(message));
        if (!continuing)
        {
            _armed = false;
            _frontier = OpenAmount(message);
            _slips = 0;
            _off = 0f;
            _left = 0f;
            _right = 0f;
            _grab = 0;
            _didPull = false;
        }
        if (!_windowMode && !_zoomed)
        {
            RaiseNetworkEvent(new SurgeryFocusMessage { On = 1 });
            _zoomed = true;
        }

        Paint(patient.Value, _frontier);
    }

    private void Trace(EntityUid patient, bool down, float frameTime)
    {
        if (_board == null || _verb == SurgeryVerbKind.Inspect)
            return;

        if (!_windowMode && _input.MouseScreenPosition.Window == WindowId.Invalid)
            return;

        _nickWait = MathF.Max(0f, _nickWait - frameTime);
        if (!Segment((LimbZone) _board.Zone, out var localStart, out var localEnd))
            return;
        var rot = TryComp<SpriteComponent>(patient, out var sprite) ? sprite.Rotation : Angle.Zero;
        var origin = BodyPos(patient, sprite);
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (_windowMode && _stage is { Inside: true })
        {
            mouse = new MapCoordinates(origin + rot.RotateVec(_stage.Meters), Transform(patient).MapID);
        }
        else if (_windowMode && _stage?.Parent != null)
        {
            return;
        }
        if (mouse.MapId != Transform(patient).MapID)
            return;

        var start = origin + rot.RotateVec(localStart);
        var end = origin + rot.RotateVec(localEnd);
        var delta = end - start;
        var length2 = delta.LengthSquared();
        if (length2 < 0.0001f)
            return;

        var length = MathF.Sqrt(length2);
        var rel = mouse.Position - start;
        var t = Math.Clamp(Vector2.Dot(rel, delta) / length2, 0f, 1f);
        var sideDir = new Vector2(-delta.Y, delta.X) / length;
        var signed = Vector2.Dot(rel, sideDir);
        var side = MathF.Abs(signed);
        var onLine = side < 0.16f;
        var axis = localEnd - localStart;
        if (axis.LengthSquared() > 0.0001f)
            axis /= axis.Length();
        else
            axis = new Vector2(0f, -1f);

        if (_verb == SurgeryVerbKind.SawBone)
        {
            TraceSaw(down, onLine);
            return;
        }

        if (_verb == SurgeryVerbKind.Retract)
        {
            TraceSpread(down, signed, t);
            return;
        }

        if (_verb is SurgeryVerbKind.Clamp or SurgeryVerbKind.Extract or SurgeryVerbKind.Insert)
        {
            if (down && !_down && onLine)
                Send(1f, 0, false);
            return;
        }

        var startWindow = length < 0.12f ? 0.5f : 0.22f;
        if (down && !_down)
        {
            _cutSent = false;
            _armed = t < startWindow && side < 0.16f;
            _frontier = _armed ? t : _frontier;
            if (!_armed && Cuts(_verb) && t > 0.05f && t < 0.95f && side > 0.24f && side < 0.34f)
                Nick(patient, Beside(localStart, localEnd, t, signed, side), axis);
        }

        if (down && _armed)
        {
            if (side >= 0.28f)
            {
                _off = 0f;
            }
            else if (t <= _frontier + 0.2f)
            {
                _frontier = MathF.Max(_frontier, t);
            }
            else if (Cuts(_verb) && side < 0.2f)
            {
                Nick(patient, Beside(localStart, localEnd, t, signed, side), axis);
            }

            if (_frontier >= 0.92f && !_cutSent)
            {
                _armed = false;
                _cutSent = true;
                Send(1f, _slips, false);
            }
        }

        if (!down && _down && _armed && !_cutSent)
        {
            _armed = false;
            _cutSent = true;
            if (_frontier >= 0.85f)
                Send(1f, _slips, false);
            else if (_frontier > 0.25f)
                Send(_frontier, Cuts(_verb) ? _slips : 0, false);
        }
    }

    private void TraceSaw(bool down, bool onLine)
    {
        if (!(down && !_down) || !onLine)
            return;

        var phase = (MathF.Sin(_time * 3.4f) + 1f) * 0.5f;
        var half = MathF.Max(0.06f, 0.16f - _passes * 0.03f);
        if (MathF.Abs(phase - _guide.SawWindow) > half)
            return;

        _guide.SawWindow = Random.Shared.NextSingle() * 0.7f + 0.15f;
        _passes++;
        if (_passes < 3)
            return;

        _passes = 0;
        Send(1f, 0, false);
    }

    private void WorkUi()
    {
        if (_board == null)
            return;

        if (_windowMode)
        {
            _map?.Orphan();
            OpenStage();
            return;
        }

        _stage?.Orphan();
        if (_board.Organs.Length > 0 || _board.Empty.Length > 0)
        {
            OpenWork(_game == SurgeryGameKind.Clamp, Inserting());
            return;
        }

        if (_game == SurgeryGameKind.Clamp)
        {
            OpenWork(true);
            return;
        }

        if (_game == SurgeryGameKind.Sever || _board.Organs.Length > 0 && ToolHas(SurgeryVerbKind.Extract))
        {
            OpenWork(false);
            return;
        }

        _map?.Orphan();
        _guide.Cone = false;
    }

    private void OpenStage()
    {
        if (_board == null || !TryGetEntity(_board.Patient, out var patient))
            return;

        _stage ??= new SurgeryStageView();
        if (_stage.Parent == null)
        {
            _stage.Picked += OnOrganPicked;
            _stage.Dragged += OnOrganSevered;
            _ui.WindowRoot.AddChild(_stage);
        }

        _stage.SetPatient(patient.Value);
        var inserting = Inserting();
        _stage.SetOrgans(inserting || _board.Organs.Length > 0 && (_game is SurgeryGameKind.Clamp or SurgeryGameKind.Sever || ToolHas(SurgeryVerbKind.Extract)), inserting ? _board.Empty : _board.Organs);
    }

    private bool Inserting()
    {
        return _board != null && _board.Empty.Length > 0 && Has(_board.Verbs, SurgeryVerbKind.Insert);
    }

    private void OpenWork(bool pick, bool insert = false)
    {
        if (_board == null)
            return;

        _map ??= new SurgeryChestMap();
        if (!_subscribed)
        {
            _map.Picked += OnOrganPicked;
            _map.Severed += OnOrganSevered;
            _map.TakeLodged += OnTakeLodged;
            _subscribed = true;
        }

        _workPick = pick;
        if (_map.Parent == null)
            _ui.WindowRoot.AddChild(_map);

        _map.Lodged = ItemIcon(_board.LodgedItem);
        _map.Show(_board.Organs, _board.Empty, _board.Chosen, !pick && !insert, insert, (LimbZone) _board.Zone);
    }

    private void OnOrganPicked(byte organ)
    {
        if (_board == null)
            return;

        var wasChosen = _board.Chosen == organ;
        RaiseNetworkEvent(new SurgeryPickMessage { Patient = _board.Patient, Organ = organ });
        _board.Chosen = organ;
        _organ = organ;
        if (_game == SurgeryGameKind.Clamp || _verb == SurgeryVerbKind.Clamp)
        {
            Send(1f, 0, false);
            return;
        }

        if (Inserting())
        {
            if (!wasChosen)
                return;

            Send(1f, 0, false, SurgeryVerbKind.Insert);
            return;
        }

        if (ToolHas(SurgeryVerbKind.Extract))
            OnOrganSevered();
    }

    private void OnTakeLodged()
    {
        if (_board == null)
            return;

        RaiseNetworkEvent(new SurgeryCutMessage
        {
            Patient = _board.Patient,
            Verb = (byte) SurgeryVerbKind.Extract,
            Layer = _layer,
            Organ = 254,
            Progress = 1f,
        });
    }

    private void OnOrganSevered()
    {
        if (Inserting())
            return;

        _guide.Cone = false;
        Send(1f, 0, false, SurgeryVerbKind.Extract);
    }

    private void PlaceMap(EntityUid patient)
    {
        var mid = (_guide.Start + _guide.End) * 0.5f;
        var screen = _eye.WorldToScreen(mid);
        _guide.SawBar = _verb == SurgeryVerbKind.SawBone;
        _guide.SawPhase = (MathF.Sin(_time * 3.4f) + 1f) * 0.5f;
        _guide.SawAt = screen;
        _guide.Saw = _passes;
        var panel = _stage?.Parent != null ? (Control) _stage : _map?.Parent != null ? _map : null;
        if (panel == null)
        {
            _guide.Cone = false;
        }
        else
        {
            var view = _ui.RootControl.PixelSize;
            var x = screen.X + 36f;
            var y = screen.Y - panel.MinSize.Y - 24f;
            if (y < 8f)
                y = screen.Y + 28f;

            x = Math.Clamp(x, 8f, Math.Max(8f, view.X - panel.MinSize.X - 8f));
            y = Math.Clamp(y, 8f, Math.Max(8f, view.Y - panel.MinSize.Y - 8f));
            LayoutContainer.SetAnchorPreset(panel, LayoutContainer.LayoutPreset.TopLeft);
            LayoutContainer.SetMarginLeft(panel, x);
            LayoutContainer.SetMarginTop(panel, y);
            LayoutContainer.SetGrowHorizontal(panel, LayoutContainer.GrowDirection.End);
            LayoutContainer.SetGrowVertical(panel, LayoutContainer.GrowDirection.End);
            _guide.Cone = true;
            _guide.ConeFrom = screen;
            _guide.PanelPos = panel.GlobalPixelPosition;
            _guide.PanelSize = panel.PixelSize;
        }

        PlaceHint(screen);
    }

    private void PlaceHint(Vector2 screen)
    {
        if (_board == null || _board.StepVerb == 255)
        {
            _hint?.Orphan();
            _guide.Bubble = false;
            return;
        }

        _hint ??= new SurgeryHint();
        if (_hint.Parent == null)
            _ui.WindowRoot.AddChild(_hint);

        var shown = (SurgeryVerbKind) _board.StepVerb;
        if (_board.UndoVerb != 255 && _verb == (SurgeryVerbKind) _board.UndoVerb)
            shown = _verb;
        else if (_board.CloseVerb != 255 && _verb == (SurgeryVerbKind) _board.CloseVerb)
            shown = _verb;
        _hint.Show(shown, _board.UndoVerb == 255 ? SurgeryVerbKind.Inspect : (SurgeryVerbKind) _board.UndoVerb);
        _hint.SetSize = _hint.MinSize;
        var size = _hint.MinSize * _hint.UIScale;
        var view = _ui.RootControl.PixelSize;
        var x = Math.Clamp(screen.X + 20f, 8f, Math.Max(8f, view.X - size.X - 8f));
        var y = Math.Clamp(screen.Y - 120f, 8f, Math.Max(8f, view.Y - size.Y - 8f));
        LayoutContainer.SetAnchorPreset(_hint, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetMarginLeft(_hint, x);
        LayoutContainer.SetMarginTop(_hint, y);
        _guide.Bubble = true;
        _guide.BubbleFrom = screen;
        _guide.BubblePos = _hint.GlobalPixelPosition;
        _guide.BubbleSize = size;
    }

    private bool InReach(Vector2 here, Vector2 there)
    {
        return (here - there).LengthSquared() <= NearDistance * NearDistance;
    }

    private bool Lodging(EntityUid user)
    {
        if (_board == null || !Has(_board.Verbs, SurgeryVerbKind.Insert))
            return false;

        if (!_hands.TryGetActiveItem(user, out var held))
            return false;

        return !HasComp<OrganComponent>(held) && !HasComp<BodyPartComponent>(held);
    }

    private bool ToolHas(SurgeryVerbKind verb)
    {
        return _tool != null && TryComp<SurgeryToolComponent>(_tool.Value, out var tool) && tool.Verbs.Contains(verb);
    }

    private static SurgeryGameKind GameOf(SurgeryVerbKind verb)
    {
        return verb switch
        {
            SurgeryVerbKind.Retract => SurgeryGameKind.Spread,
            SurgeryVerbKind.SawBone => SurgeryGameKind.Saw,
            SurgeryVerbKind.Clamp => SurgeryGameKind.Clamp,
            SurgeryVerbKind.Extract => SurgeryGameKind.Sever,
            _ => SurgeryGameKind.Cut,
        };
    }

    private void TraceSpread(bool down, float signed, float along)
    {
        var side = MathF.Abs(signed);
        var onArrow = along > 0.02f && along < 0.98f && side < 0.22f;
        if (down && !_down && onArrow)
        {
            _grab = signed >= 0f ? 1 : -1;
            _armed = true;
        }

        if (down && _grab != 0)
        {
            var pulled = Math.Clamp(side, 0.04f, 0.2f);
            if (_grab > 0)
                _right = MathF.Max(_right, pulled);
            else
                _left = MathF.Max(_left, pulled);
            _didPull = true;
        }

        if (down || !_down)
            return;

        _grab = 0;
        _armed = false;
        if (_left > 0.09f && _right > 0.09f && !LayerHeld())
            Send(1f, 0, false);
    }

    private bool LayerHeld()
    {
        if (_board == null)
            return false;

        var layer = Top(_board);
        return layer >= 0 && _board.Retracted[layer] != 0;
    }

    private float Spread()
    {
        return MathF.Max(_left, _right);
    }

    private bool CanRetract()
    {
        if (_board == null)
            return false;

        var layer = Top(_board);
        if (layer < 0)
            return false;

        return _board.Retracted[layer] != 0 || _board.Integrity[layer] <= 0.92f;
    }

    private float VisualAmount()
    {
        if (_board == null)
            return 0f;

        if (_verb == SurgeryVerbKind.Retract)
            return Spread() > 0.07f && CanRetract() ? 1f : OpenAmount(_board);

        return Cuts(_verb) ? _frontier : OpenAmount(_board);
    }

    private static Vector2 Beside(Vector2 start, Vector2 end, float along, float signed, float side)
    {
        var span = end - start;
        var sideLocal = new Vector2(-span.Y, span.X);
        if (sideLocal.LengthSquared() > 0.0001f)
            sideLocal /= sideLocal.Length();

        return start + span * along + sideLocal * MathF.Min(side, 0.12f) * MathF.Sign(signed);
    }

    private void Nick(EntityUid patient, Vector2 local, Vector2 dir)
    {
        if (_nickWait > 0f)
            return;

        _nickWait = 0.35f;
        if (_nicks.Count < 24)
            _nicks.Add((patient, local, dir));

        Send(0f, 1, false);
    }

    private void PlaceNicks()
    {
        _guide.Cuts.Clear();
        foreach (var nick in _nicks)
        {
            if (!Exists(nick.Body) || !TryComp<SpriteComponent>(nick.Body, out var sprite))
                continue;

            var origin = BodyPos(nick.Body, sprite);
            var mid = origin + sprite.Rotation.RotateVec(nick.Local);
            var dir = sprite.Rotation.RotateVec(nick.Dir) * 0.05f;
            _guide.Cuts.Add((mid - dir, mid + dir));
        }
    }

    private static bool Cuts(SurgeryVerbKind verb)
    {
        return verb is SurgeryVerbKind.Incise or SurgeryVerbKind.SawBone or SurgeryVerbKind.Amputate or SurgeryVerbKind.Cauterize;
    }

    private void ShowGuide(EntityUid patient)
    {
        if (_board == null)
        {
            _guide.Active = false;
            return;
        }

        if (!Segment((LimbZone) _board.Zone, out var localStart, out var localEnd))
        {
            _guide.Active = false;
            return;
        }
        var rot = TryComp<SpriteComponent>(patient, out var sprite) ? sprite.Rotation : Angle.Zero;
        var origin = BodyPos(patient, sprite);
        _guide.Anchor = patient;
        _guide.LocalA = localStart;
        _guide.LocalB = localEnd;
        _guide.Start = origin + rot.RotateVec(localStart);
        _guide.End = origin + rot.RotateVec(localEnd);
        _guide.Mark = _windowMode ? SurgeryMark.None : MarkFor(_verb, _game);
        _guide.Ink = new Color(0.12f, 0.12f, 0.13f, 0.95f);
        _guide.Radius = 0.018f;
        _guide.Cover = _frontier;
        _guide.Left = MathF.Max(_left, 0.08f);
        _guide.Right = MathF.Max(_right, 0.08f);
        _guide.Active = true;
    }

    private Vector2 BodyPos(EntityUid uid, SpriteComponent? sprite)
    {
        var pos = _transform.GetWorldPosition(uid);
        if (sprite != null)
            pos += sprite.Rotation.RotateVec(sprite.Offset);

        return pos;
    }

    private void Aim(EntityUid user)
    {
        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
        if (!MarkHand(user, out _, out _))
        {
            _down = down;
            return;
        }

        if (down && !_down && TryBody(user, out var body))
        {
            _picking = body;
            _stay = null;
            if (!_zoomed)
            {
                RaiseNetworkEvent(new SurgeryFocusMessage { On = 1 });
                _zoomed = true;
            }
        }

        _down = down;
    }

    private void Pick(EntityUid user)
    {
        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
        if (_picking == null || !Exists(_picking.Value) || !MarkHand(user, out var thick, out var ink))
        {
            _picking = null;
            _down = down;
            return;
        }

        if (_input.IsKeyDown(Keyboard.Key.MouseRight))
        {
            _picking = null;
            _headMenu?.Orphan();
            if (_zoomed)
            {
                RaiseNetworkEvent(new SurgeryFocusMessage { On = 0 });
                _zoomed = false;
            }

            _guide.Active = false;
            _down = down;
            return;
        }

        _guide.Radius = thick ? 0.028f : 0.012f;
        _guide.Ink = ink;
        if (!TryAim(user, out var patient, out var zone, out _) || patient != _picking || !Segment(zone, out var localStart, out var localEnd))
        {
            _guide.Active = false;
            _down = down;
            return;
        }

        var rot = TryComp<SpriteComponent>(patient, out var sprite) ? sprite.Rotation : Angle.Zero;
        var origin = BodyPos(patient, sprite);
        _guide.Anchor = patient;
        _guide.LocalA = localStart;
        _guide.LocalB = localEnd;
        _guide.Start = origin + rot.RotateVec(localStart);
        _guide.End = origin + rot.RotateVec(localEnd);
        _guide.Mark = SurgeryMark.Dash;
        _guide.Active = true;
        if (down && !_down)
        {
            if (zone == LimbZone.Head)
                ShowHead(patient);
            else
            {
                _picking = null;
                RaiseNetworkEvent(new SurgeryZoneMessage
                {
                    Patient = GetNetEntity(patient),
                    Zone = (byte) zone,
                });
            }
        }

        _down = down;
    }

    private void Stay(EntityUid patient)
    {
        if (!TryComp<SurgerySiteComponent>(patient, out var site) || !CutOpen(site) || !Segment(site.Zone, out var localStart, out var localEnd))
        {
            _stay = null;
            _guide.Active = false;
            return;
        }

        var rot = TryComp<SpriteComponent>(patient, out var sprite) ? sprite.Rotation : Angle.Zero;
        var origin = BodyPos(patient, sprite);
        _guide.Anchor = patient;
        _guide.LocalA = localStart;
        _guide.LocalB = localEnd;
        _guide.Start = origin + rot.RotateVec(localStart);
        _guide.End = origin + rot.RotateVec(localEnd);
        _guide.Mark = SurgeryMark.Dash;
        _guide.Ink = new Color(0.12f, 0.12f, 0.13f, 0.95f);
        _guide.Radius = 0.018f;
        _guide.Active = true;
    }

    private static bool CutOpen(SurgerySiteComponent site)
    {
        foreach (var layer in site.Layers)
        {
            if (layer.Integrity < 0.9f || layer.Retracted)
                return true;
        }

        return false;
    }

    private bool MarkHand(EntityUid user, out bool thick, out Color ink)
    {
        thick = true;
        ink = new Color(0.12f, 0.12f, 0.13f, 0.95f);
        if (!_hands.TryGetActiveItem(user, out var held))
            return false;

        if (HasComp<SurgeryMarkerComponent>(held))
            return true;

        if (!_tags.HasTag(held.Value, "Pen"))
            return false;

        thick = false;
        if (TryComp<SpriteComponent>(held, out var sprite) && sprite.Color.A > 0.2f)
            ink = sprite.Color.WithAlpha(0.95f);
        return true;
    }

    private bool TryBody(EntityUid user, out EntityUid body)
    {
        if (TryAim(user, out var patient, out _, out _))
        {
            body = patient;
            return true;
        }

        body = default;
        return false;
    }

    private Texture? ItemIcon(NetEntity net)
    {
        if (!TryGetEntity(net, out var uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            return null;

        if (sprite.BaseRSI is not { } rsi)
            return null;

        var stateName = _sprites.LayerGetRsiState((uid.Value, sprite), 0);
        return rsi.TryGetState(stateName, out var state) ? state.GetFrame(RsiDirection.South, 0) : null;
    }

    private bool TryAim(EntityUid user, out EntityUid patient, out LimbZone zone, out SurgeryMarkPrototype mark)
    {
        patient = default;
        zone = default;
        mark = null!;
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace)
            return false;

        var here = _transform.GetWorldPosition(user);
        var best = float.MaxValue;
        var query = EntityQueryEnumerator<LimbBodyComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            if (xform.MapID != mouse.MapId)
                continue;

            var there = _transform.GetWorldPosition(uid);
            if ((here - there).LengthSquared() > NearDistance * NearDistance)
                continue;

            var buckled = TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled;
            if (!buckled && !_standing.IsDown(uid))
                continue;

            var local = (-sprite.Rotation).RotateVec(mouse.Position - BodyPos(uid, sprite));
            var px = local.X * 32f + 16f;
            var py = 16f - local.Y * 32f;
            foreach (var candidate in _proto.EnumeratePrototypes<SurgeryMarkPrototype>())
            {
                if (candidate.Hw <= 0 || candidate.Hh <= 0 || !Enum.TryParse<LimbZone>(candidate.ID, out var part))
                    continue;

                if (part is LimbZone.Eyes or LimbZone.Mouth)
                    continue;

                if (px < candidate.Hx || py < candidate.Hy || px > candidate.Hx + candidate.Hw || py > candidate.Hy + candidate.Hh)
                    continue;

                var dist = (mouse.Position - there).LengthSquared();
                if (dist >= best)
                    continue;

                best = dist;
                patient = uid;
                zone = part;
                mark = candidate;
            }
        }

        return mark != null;
    }

    private void ShowHead(EntityUid patient)
    {
        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid)
            return;

        _headBody = patient;
        _headMenu?.Orphan();
        var menu = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };
        var parts = new List<SurgeryPartPrototype>();
        foreach (var part in _proto.EnumeratePrototypes<SurgeryPartPrototype>())
        {
            if (part.Zone == "Head")
                parts.Add(part);
        }

        parts.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var part in parts)
        {
            var id = part.ID;
            var name = id switch
            {
                "Brain" => "Мозг",
                "EyeRight" => "Правый глаз",
                "EyeLeft" => "Левый глаз",
                "Tongue" => "Язык",
                "Teeth" => "Зубы",
                _ => id,
            };
            var button = new Button { Text = name, MinSize = new Vector2(120, 28) };
            button.OnPressed += _ => ChooseHead(id);
            menu.AddChild(button);
        }

        _ui.WindowRoot.AddChild(menu);
        if (mouse.IsValid)
        {
            LayoutContainer.SetAnchorPreset(menu, LayoutContainer.LayoutPreset.TopLeft);
            LayoutContainer.SetMarginLeft(menu, mouse.Position.X + 16f);
            LayoutContainer.SetMarginTop(menu, mouse.Position.Y - 18f);
        }

        _headMenu = menu;
    }

    private void ChooseHead(string id)
    {
        _headMenu?.Orphan();
        _headMenu = null;
        if (_headBody == null)
            return;

        if (_proto.TryIndex<SurgeryPartPrototype>(id, out var part) && part.Operation.Length == 0)
        {
            _headBody = null;
            return;
        }

        _picking = null;
        RaiseNetworkEvent(new SurgeryZoneMessage
        {
            Patient = GetNetEntity(_headBody.Value),
            Zone = (byte) LimbZone.Head,
            Part = id,
        });
        _headBody = null;
    }

    private string HeadPart()
    {
        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid)
            return "";

        foreach (var (id, box, _) in _guide.HeadBoxes)
        {
            if (box.Contains(mouse.Position))
                return id;
        }

        return "";
    }

    private bool CanStart(SurgeryMarkPrototype mark)
    {
        if (_tool == null || !TryComp<SurgeryToolComponent>(_tool.Value, out var tool))
            return false;

        if (mark.Start.Count == 0)
            return true;

        foreach (var verb in mark.Start)
        {
            if (tool.Verbs.Contains(verb))
                return true;
        }

        return false;
    }

    private bool Segment(LimbZone zone, out Vector2 start, out Vector2 end)
    {
        start = default;
        end = default;
        if (_board != null
            && _board.Part.Length > 0
            && _proto.TryIndex<SurgeryPartPrototype>(_board.Part, out var part)
            && (part.Ax != part.Bx || part.Ay != part.By))
        {
            start = new Vector2((part.Ax - 16) / 32f, (16 - part.Ay) / 32f);
            end = new Vector2((part.Bx - 16) / 32f, (16 - part.By) / 32f);
            return true;
        }

        if (!_proto.TryIndex<SurgeryMarkPrototype>(zone.ToString(), out var mark))
            return false;

        start = new Vector2((mark.Ax - 16) / 32f, (16 - mark.Ay) / 32f);
        end = new Vector2((mark.Bx - 16) / 32f, (16 - mark.By) / 32f);
        return true;
    }

    private static SurgeryMark MarkFor(SurgeryVerbKind verb, SurgeryGameKind game)
    {
        if (verb == SurgeryVerbKind.Retract || game == SurgeryGameKind.Spread)
            return SurgeryMark.Spread;
        if (game == SurgeryGameKind.Hold)
            return SurgeryMark.Dash;
        if (game == SurgeryGameKind.Tap)
            return SurgeryMark.Fine;
        if (game == SurgeryGameKind.Saw || verb == SurgeryVerbKind.SawBone)
            return SurgeryMark.None;
        if (game == SurgeryGameKind.Spread || verb == SurgeryVerbKind.Retract)
            return SurgeryMark.Spread;
        if (verb is SurgeryVerbKind.Clamp or SurgeryVerbKind.Extract)
            return SurgeryMark.Scissors;
        if (verb == SurgeryVerbKind.Cauterize)
            return SurgeryMark.Spark;
        if (verb == SurgeryVerbKind.Incise)
            return SurgeryMark.Dash;
        return SurgeryMark.None;
    }

    private void Send(float progress, int slips, bool yank, SurgeryVerbKind? verb = null)
    {
        if (_board == null)
            return;

        RaiseNetworkEvent(new SurgeryCutMessage
        {
            Patient = _board.Patient,
            Verb = (byte) (verb ?? _verb),
            Layer = _layer,
            Organ = _board.Chosen != 255 ? _board.Chosen : _organ,
            Progress = Math.Clamp(progress, 0f, 1f),
            Slips = (byte) Math.Clamp(slips, 0, 12),
            Yank = yank ? (byte) 1 : (byte) 0,
        });
    }

    private void EndView()
    {
        if (_zoomed)
            RaiseNetworkEvent(new SurgeryFocusMessage { On = 0 });

        _zoomed = false;
        _picking = null;
        _stay = _patient;
        _headMenu?.Orphan();
        _guide.Active = false;
        _guide.SawBar = false;
        _guide.Cone = false;
        _guide.HeadChoice = false;
        _guide.Bubble = false;
        _guide.Mark = SurgeryMark.None;
        _map?.Orphan();
        _stage?.Orphan();
        _hint?.Orphan();
        _board = null;
        _patient = null;
        _tool = null;
        _armed = false;
    }

    private void Paint(EntityUid patient, float amount)
    {
        if (!MaleHuman(patient) || !TryComp<SpriteComponent>(patient, out var sprite))
            return;

        var state = WoundState(amount);
        if (state.StartsWith("torso") && _game == SurgeryGameKind.Spread && _didPull)
        {
            var left = _left > 0.08f;
            var right = _right > 0.08f;
            if (left && !right)
                state = "torso_open_l";
            else if (right && !left)
                state = "torso_open_r";
            else if (left && right)
                state = state.EndsWith("_organs") ? "torso_organs" : "torso_open";
        }

        if (state == "torso_ribs")
            state = "torso_open";

        if (state.StartsWith("torso"))
        {
            SetWoundLayer(sprite, LayerKey, "none", false);
            _holes[patient] = state;
            EnsureChest(patient, sprite, state);
            return;
        }

        if (_holes.Remove(patient))
            RestoreChest(patient, sprite);
        SetWoundLayer(sprite, LayerKey, state, state != "none");
        if (state != "none")
            _sprites.LayerSetOffset((patient, sprite), LayerKey, Vector2.Zero);
    }

    private void OnSite(Entity<SurgerySiteComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!MaleHuman(ent) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var state = WoundOf(ent.Comp);
        if (state == "torso_ribs")
            state = "torso_open";

        if (state.StartsWith("torso"))
        {
            SetWoundLayer(sprite, LayerKey, "none", false);
            _holes[ent.Owner] = state;
            EnsureChest(ent, sprite, state);
            return;
        }

        if (_holes.Remove(ent))
            RestoreChest(ent, sprite);
        SetWoundLayer(sprite, LayerKey, state, state != "none");
        if (state != "none")
            _sprites.LayerSetOffset((ent, sprite), LayerKey, Vector2.Zero);
    }

    private string WoundOf(SurgerySiteComponent site)
    {
        var count = site.Layers.Count;
        if (count == 0)
            return "none";

        var kinds = new byte[count];
        var integrity = new float[count];
        var retracted = new byte[count];
        for (var i = 0; i < count; i++)
        {
            kinds[i] = (byte) site.Layers[i].Kind;
            integrity[i] = site.Layers[i].Integrity;
            retracted[i] = site.Layers[i].Retracted ? (byte) 1 : (byte) 0;
        }

        if (!_proto.TryIndex<SurgeryMarkPrototype>(site.Zone.ToString(), out var mark) || mark.Prefix.Length == 0)
            return "none";

        return SurgeryProcedure.Wound(mark.Prefix, kinds, integrity, retracted, 1f);
    }

    private void HoldHoles()
    {
        if (_holes.Count == 0)
            return;

        List<EntityUid>? gone = null;
        foreach (var (patient, state) in _holes)
        {
            if (!Exists(patient) || !TryComp<SpriteComponent>(patient, out var sprite))
            {
                gone ??= new List<EntityUid>();
                gone.Add(patient);
                continue;
            }

            EnsureChest(patient, sprite, state);
        }

        if (gone == null)
            return;

        foreach (var patient in gone)
            _holes.Remove(patient);
    }

    private void EnsureChest(EntityUid patient, SpriteComponent sprite, string state)
    {
        if (!_sprites.LayerMapTryGet((patient, sprite), HumanoidVisualLayers.Chest, out var chest, false))
            return;

        if (_sprites.LayerGetRsiState((patient, sprite), chest) == state)
            return;

        var color = sprite[chest].Color;
        _sprites.LayerSetRsi((patient, sprite), HumanoidVisualLayers.Chest, Rsi, state);
        _sprites.LayerSetColor((patient, sprite), HumanoidVisualLayers.Chest, color);
    }

    private void RestoreChest(EntityUid patient, SpriteComponent sprite)
    {
        if (!_sprites.LayerMapTryGet((patient, sprite), HumanoidVisualLayers.Chest, out var chest, false))
            return;

        var color = sprite[chest].Color;
        _sprites.LayerSetRsi((patient, sprite), HumanoidVisualLayers.Chest, TorsoRsi, "torso_m");
        _sprites.LayerSetColor((patient, sprite), HumanoidVisualLayers.Chest, color);
    }

    private void SetWoundLayer(SpriteComponent sprite, string key, string state, bool visible)
    {
        if (!sprite.LayerMapTryGet(key, out var layer))
        {
            layer = sprite.AddLayer(new SpriteSpecifier.Rsi(Rsi, state == "none" ? "none" : state));
            sprite.LayerMapSet(key, layer);
        }
        else if (state != "none")
        {
            sprite.LayerSetState(layer, state);
        }

        sprite.LayerSetVisible(layer, visible);
    }

    private string WoundState(float amount)
    {
        if (_board == null)
            return "none";

        var zone = (LimbZone) _board.Zone;
        if (!_proto.TryIndex<SurgeryMarkPrototype>(zone.ToString(), out var mark) || mark.Prefix.Length == 0)
            return "none";

        var layer = _board.StepLayer;
        if (layer != 255 && layer < _board.LayerKinds.Length && layer < _board.Retracted.Length)
        {
            var kind = (SurgeryLayerKind) _board.LayerKinds[layer];
            var held = _board.Retracted[layer] != 0;
            if (kind == SurgeryLayerKind.Skin && !held)
                return CutLook(mark.Prefix, amount);
            if (kind == SurgeryLayerKind.Skin && held)
                return mark.Prefix + "_open";
            if (kind == SurgeryLayerKind.Bone)
                return mark.Prefix == "torso" ? "torso_open" : mark.Prefix + "_open";
            if (kind is SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe)
                return mark.Prefix is "torso" or "head" ? mark.Prefix + "_organs" : mark.Prefix + "_open";
        }

        return SurgeryProcedure.Wound(mark.Prefix, _board.LayerKinds, _board.Integrity, _board.Retracted, amount);
    }

    private static string CutLook(string part, float amount)
    {
        if (amount > 0.8f)
            return part + "_cut4";
        if (amount > 0.55f)
            return part + "_cut3";
        if (amount > 0.3f)
            return part + "_cut2";
        if (amount > 0.05f)
            return part + "_cut1";
        return "none";
    }

    private bool MaleHuman(EntityUid uid)
    {
        return TryComp<HumanoidAppearanceComponent>(uid, out var look)
               && look.Species.Id == "Human"
               && look.Sex == Sex.Male;
    }

    private static float OpenAmount(SurgeryBoardMessage board)
    {
        var layer = Top(board);
        if (layer < 0)
            return 0f;

        if (board.Retracted[layer] != 0 || board.Integrity[layer] < 0.2f)
            return 1f;

        return Math.Clamp(1f - board.Integrity[layer], 0f, 1f);
    }

    private static SurgeryVerbKind Pick(SurgeryBoardMessage board)
    {
        if (board.Embedded != 0 && Has(board.Verbs, SurgeryVerbKind.PullEmbedded))
            return SurgeryVerbKind.PullEmbedded;
        if (board.Organs.Length > 0 && Has(board.Verbs, SurgeryVerbKind.Extract))
            return SurgeryVerbKind.Extract;
        if (Has(board.Verbs, SurgeryVerbKind.Insert))
            return SurgeryVerbKind.Insert;

        var layer = Top(board);
        if ((uint) layer >= (uint) board.LayerKinds.Length || (uint) layer >= (uint) board.Integrity.Length)
            return SurgeryVerbKind.Inspect;

        var kind = (SurgeryLayerKind) board.LayerKinds[layer];
        if (Has(board.Verbs, SurgeryVerbKind.Incise) && kind is not SurgeryLayerKind.Bone && board.Integrity[layer] > 0.12f)
            return SurgeryVerbKind.Incise;
        if (Has(board.Verbs, SurgeryVerbKind.SawBone) && kind is SurgeryLayerKind.Bone)
            return SurgeryVerbKind.SawBone;
        if (Has(board.Verbs, SurgeryVerbKind.Amputate))
            return SurgeryVerbKind.Amputate;
        if (Has(board.Verbs, SurgeryVerbKind.Retract))
            return SurgeryVerbKind.Retract;
        if (Has(board.Verbs, SurgeryVerbKind.Cauterize))
            return SurgeryVerbKind.Cauterize;
        if (Has(board.Verbs, SurgeryVerbKind.SetBone))
            return SurgeryVerbKind.SetBone;
        if (Has(board.Verbs, SurgeryVerbKind.MendBone))
            return SurgeryVerbKind.MendBone;
        if (Has(board.Verbs, SurgeryVerbKind.Clamp))
            return SurgeryVerbKind.Clamp;
        if (Has(board.Verbs, SurgeryVerbKind.Seat))
            return SurgeryVerbKind.Seat;
        return SurgeryVerbKind.Inspect;
    }

    private static int Top(SurgeryBoardMessage board)
    {
        for (var i = 0; i < board.Retracted.Length; i++)
        {
            if (board.Retracted[i] == 0)
                return i;
        }

        return board.Retracted.Length - 1;
    }

    private static SurgeryVerbKind StepOf(SurgeryBoardMessage message)
    {
        if (message.StepVerb == 255)
            return Pick(message);

        var step = (SurgeryVerbKind) message.StepVerb;
        return Has(message.Verbs, step) ? step : SurgeryVerbKind.Inspect;
    }

    private static bool Has(byte[] verbs, SurgeryVerbKind verb)
    {
        foreach (var item in verbs)
        {
            if (item == (byte) verb)
                return true;
        }

        return false;
    }
}
