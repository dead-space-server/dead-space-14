// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.DragDrop;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Standing;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Surgery;

public sealed class SurgeryClientSystem : EntitySystem
{
    private const string WoundPrefix = "surgery-wound-";
    private const string LinePrefix = "line";
    private static readonly ResPath Rsi = new("/Textures/_DeadSpace/Mobs/Surgery/human_male.rsi");
    private static readonly ResPath LineRsi = new("/Textures/_DeadSpace/Mobs/Surgery/line.rsi");

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly SurgeryNearbySystem _nearby = default!;
    [Dependency] private readonly LimbImplantSystem _implants = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;

    private const float NearDistance = 2.5f;
    private const float BodyTexels = 32f;
    private static readonly Color FleshInk = new(0.12f, 0.12f, 0.13f, 0.95f);
    private static readonly Color MachineInk = new(0.12f, 0.95f, 0.28f, 0.95f);
    private const int ConeFrom = 2;
    private const string BackSuffix = "Back";
    private (string Part, bool Across)? _preview;
    private (EntityUid Patient, LimbZone Zone)? _menuFor;
    private readonly HashSet<EntityUid> _blind = new();
    private readonly Dictionary<EntityUid, HashSet<HumanoidVisualLayers>> _rotTint = new();
    private readonly Dictionary<string, Texture> _woundArt = new();
    private readonly List<SurgeryKept> _openWounds = new();
    private readonly Dictionary<EntityUid, HashSet<string>> _woundKeys = new();
    private readonly List<EntityUid> _rotGone = new();

    private readonly SurgeryGuideOverlay _guide = new();

    private SurgeryBoardMessage? _board;
    private EntityUid? _patient;
    private EntityUid? _picking;
    private EntityUid? _stay;
    private Control? _headMenu;
    private EntityUid? _tool;
    private SurgeryVerbKind _verb;
    private SurgeryVerbKind _step;
    private SurgeryGameKind _game;
    private SurgeryChestMap? _map;
    private SurgeryHint? _hint;
    private Vector2? _coneLocal;
    private EntityUid? _coneFor;
    private Vector2? _fanLocal;
    private EntityUid? _fanFor;
    private bool _reverse;
    private bool _workPick;
    private bool _subscribed;
    private int _passes;
    private byte _layer;
    private byte _organ = 255;
    private bool _zoomed;
    private bool _armed;
    private bool _down;
    private float _frontier;
    private int _slips;
    private float _left;
    private float _right;
    private int _grab;
    private bool _didPull;
    private float _nickWait;
    private bool _cutSent;
    private float _time;
    private readonly List<(EntityUid Body, LimbZone Zone, Vector2 Local, Vector2 Dir)> _nicks = new();
    private readonly List<(float Along, float Left)> _sear = new();
    private readonly List<(int Crack, Vector2 Local, bool Done)> _glue = new();
    private float _sawPhase;
    private float _searRadius;
    private const float SearTime = 0.7f;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AnimationPlayerSystem));
        UpdatesAfter.Add(typeof(PullingSystem));
        base.Initialize();
        SubscribeNetworkEvent<SurgeryBoardMessage>(OnBoard);
        SubscribeLocalEvent<ContentEyeComponent, GetEyeOffsetEvent>(OnEye);
        SubscribeLocalEvent<LimbBodyComponent, CanDragEvent>(OnCanDrag, after: [typeof(SharedBodySystem)]);
        SubscribeLocalEvent<LimbBodyComponent, InteractUsingEvent>(OnUse);
        SubscribeLocalEvent<SurgerySiteComponent, AfterAutoHandleStateEvent>(OnSite);
        SubscribeLocalEvent<SurgerySiteComponent, ComponentShutdown>(OnSiteGone);
        SubscribeLocalEvent<SurgeryProneComponent, ComponentStartup>(OnProne);
        SubscribeLocalEvent<SurgeryProneComponent, ComponentShutdown>(OnSupine);
        _overlays.AddOverlay(_guide);
        _guide.Map = ToWorld;
        _guide.Moved = () =>
        {
            if (_patient != null)
                PlaceMap(_patient.Value);
        };
    }

    private void OnProne(Entity<SurgeryProneComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.DirectionOverride = Direction.North;
        sprite.EnableDirectionOverride = true;
        Repaint(ent, true);
    }

    private void OnSupine(Entity<SurgeryProneComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            sprite.EnableDirectionOverride = false;
        Repaint(ent, false);
    }

    private void Repaint(EntityUid uid, bool prone)
    {
        if (TryComp<SurgerySiteComponent>(uid, out var site) && _patient != uid)
            ShowWounds(uid, site, prone);
    }

    private bool Prone(EntityUid uid)
    {
        return HasComp<SurgeryProneComponent>(uid);
    }

    private bool Wraps(EntityUid held)
    {
        foreach (var wrap in _proto.EnumeratePrototypes<SurgeryWrapPrototype>())
        {
            foreach (var tag in wrap.Tags)
            {
                if (_tags.HasTag(held, tag))
                    return true;
            }
        }

        return false;
    }

    private void TintRot()
    {
        foreach (var (uid, layers) in _rotTint)
        {
            if (RotZones(uid).Count > 0)
                continue;

            if (TryComp<HumanoidAppearanceComponent>(uid, out var look) && TryComp<SpriteComponent>(uid, out var sprite))
            {
                foreach (var layer in layers)
                {
                    if (_sprites.LayerMapTryGet((uid, sprite), layer, out _, false))
                        _sprites.LayerSetColor((uid, sprite), layer, look.SkinColor);
                }
            }

            _rotGone.Add(uid);
        }

        foreach (var uid in _rotGone)
            _rotTint.Remove(uid);
        _rotGone.Clear();

        var query = EntityQueryEnumerator<LimbBodyComponent, HumanoidAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var limbs, out var look, out var sprite))
        {
            var rot = RotZones(uid);
            if (rot.Count == 0)
                continue;

            if (!_rotTint.TryGetValue(uid, out var tinted))
            {
                tinted = new HashSet<HumanoidVisualLayers>();
                _rotTint[uid] = tinted;
            }

            var rejected = CompOrNull<SurgeryRejectionComponent>(uid);
            foreach (var slot in LimbLookRules.Slots)
            {
                if (!_sprites.LayerMapTryGet((uid, sprite), slot.Layer, out _, false))
                    continue;

                var index = LimbWoundRules.Index(slot.Zone);
                var dead = (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Atrophied;
                if (!rot.Contains(slot.Zone))
                {
                    if (tinted.Remove(slot.Layer))
                        _sprites.LayerSetColor((uid, sprite), slot.Layer, look.SkinColor);
                    continue;
                }

                if (slot.Layer is HumanoidVisualLayers.Eyes or HumanoidVisualLayers.Hair or HumanoidVisualLayers.FacialHair)
                    continue;

                var black = dead && rejected != null && rejected.Zones.Contains(slot.Zone);
                var target = black ? new Color(0.2f, 0.18f, 0.2f) : dead ? new Color(0.42f, 0.47f, 0.33f) : new Color(0.62f, 0.78f, 0.4f);
                var mix = black ? 0.85f : dead ? 0.75f : 0.45f;
                var skin = look.SkinColor;
                var tint = new Color(
                    skin.R + (target.R * skin.R - skin.R) * mix,
                    skin.G + (target.G * skin.G - skin.G) * mix,
                    skin.B + (target.B * skin.B - skin.B) * mix,
                    skin.A);
                _sprites.LayerSetColor((uid, sprite), slot.Layer, tint);
                tinted.Add(slot.Layer);
            }
        }
    }

    private bool MachineBody(EntityUid uid)
    {
        return TryComp<HumanoidAppearanceComponent>(uid, out var look) && SurgerySpeciesPrototype.IsMachine(_proto, look.Species.Id);
    }

    private bool SoftBody(EntityUid uid)
    {
        return TryComp<LimbBodyComponent>(uid, out var limbs) && limbs.Soft;
    }

    private bool PlantBody(EntityUid uid)
    {
        return TryComp<LimbBodyComponent>(uid, out var limbs) && limbs.Plant;
    }

    private HashSet<LimbZone> RotZones(EntityUid uid)
    {
        var zones = new HashSet<LimbZone>();
        if (TryComp<SurgeryInfectionComponent>(uid, out var infection))
            zones.UnionWith(infection.Rotting);

        if (TryComp<SurgeryRejectionComponent>(uid, out var rejection) && TryComp<LimbBodyComponent>(uid, out var limbs))
        {
            foreach (var zone in rejection.Zones)
            {
                var index = LimbWoundRules.Index(zone);
                if ((uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Atrophied)
                    zones.Add(zone);
            }
        }

        if (HasComp<RottingComponent>(uid))
        {
            foreach (var slot in LimbLookRules.Slots)
                zones.Add(slot.Zone);
        }

        return zones;
    }

    private void HideEyes()
    {
        var query = EntityQueryEnumerator<LimbBodyComponent, HumanoidAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var limbs, out var look, out var sprite))
        {
            if (!_sprites.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Eyes, out _, false))
                continue;

            if (limbs.EyesGone >= 2)
            {
                _sprites.LayerSetColor((uid, sprite), HumanoidVisualLayers.Eyes, look.SkinColor);
                _blind.Add(uid);
            }
            else if (_blind.Remove(uid))
            {
                _sprites.LayerSetColor((uid, sprite), HumanoidVisualLayers.Eyes, look.EyeColor);
            }
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_guide);
        EndView();
    }

    public override void FrameUpdate(float frameTime)
    {
        PlaceNicks();
        HideEyes();
        TintRot();
        KeepFan();
        if (_board == null || _patient == null || _players.LocalEntity is not { } user)
        {
            if (_players.LocalEntity is { } aimer)
            {
                if (_picking != null)
                    Pick(aimer, frameTime);
                else if (MarkHand(aimer, out _, out _))
                    Aim(aimer);
                else if (_zoomed)
                    CancelPick();
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
        _sawPhase += frameTime * 3.4f * (1f + 0.5f * _passes);
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

        _tool = _hands.TryGetActiveItem(user, out var held)
                && (HasComp<SurgeryToolComponent>(held) || GraftMaterial(patient.Value, held.Value) || HasComp<BodyPartComponent>(held) || HasComp<SurgeryAppendageComponent>(held)
                    || Wraps(held.Value) || HasComp<SolutionContainerManagerComponent>(held))
            ? held
            : null;
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
            else if (_tool != null && FewOrgans() && ToolHas(SurgeryVerbKind.Extract))
                _verb = SurgeryVerbKind.Extract;
            else if (held == null && step == SurgeryVerbKind.SetBone)
                _verb = step;
            else
                _verb = SurgeryVerbKind.Inspect;
            _game = _verb == step || _verb == close || _verb == undo
                ? _board.StepGame == 255 || _verb != step ? SurgeryProcedure.Game(_verb) : (SurgeryGameKind) _board.StepGame
                : _verb == SurgeryVerbKind.Extract ? SurgeryProcedure.Game(_verb) : SurgeryGameKind.Cut;
            if (_verb == SurgeryVerbKind.Retract && (_verb != step || _board.StepGame == 255))
                _game = SurgeryGameKind.Spread;
            if (_game is not (SurgeryGameKind.Sear or SurgeryGameKind.Inject or SurgeryGameKind.Dots or SurgeryGameKind.Steady))
                _sear.Clear();
            if (_game != SurgeryGameKind.Glue)
                _glue.Clear();
        }

        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
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

        if (_board != null && _patient == ent.Owner)
        {
            args.Handled = false;
            return;
        }

        if (!_hands.TryGetActiveItem(user, out var held) || !HasComp<SurgeryToolComponent>(held))
            return;

        var buckled = TryComp<BuckleComponent>(ent, out var buckle) && buckle.Buckled;
        if (!buckled && !_standing.IsDown(ent.Owner))
            return;

        args.Handled = false;
    }

    private void OnUse(Entity<LimbBodyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _board == null || _patient != ent.Owner)
            return;

        var wanted = _step switch
        {
            SurgeryVerbKind.Bandage => Wraps(args.Used),
            SurgeryVerbKind.Append => HasComp<SurgeryAppendageComponent>(args.Used),
            SurgeryVerbKind.Inject => HasComp<SolutionContainerManagerComponent>(args.Used),
            _ => false,
        };
        if (wanted)
            args.Handled = true;
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

        if (message.Finished != 0)
        {
            EndView();
            return;
        }

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
                    _game = message.StepGame == 255 ? SurgeryProcedure.Game(next) : (SurgeryGameKind) message.StepGame;
                    _armed = false;
                    _frontier = OpenAmount(message);
                    _grab = 0;
                    _didPull = false;
                    _left = 0f;
                    _right = 0f;
                    _sear.Clear();
                    _glue.Clear();
                    _cutSent = false;
                    if (_game is SurgeryGameKind.Hold or SurgeryGameKind.Steady)
                        _frontier = 0f;
                }
                else if (_cutSent && _game is SurgeryGameKind.Sear or SurgeryGameKind.Inject or SurgeryGameKind.Glue or SurgeryGameKind.Dots)
                {
                    _sear.Clear();
                    _glue.Clear();
                    _cutSent = false;
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
        _game = message.StepGame == 255 ? SurgeryProcedure.Game(verb) : (SurgeryGameKind) message.StepGame;
        _layer = message.StepLayer != 255 ? message.StepLayer : (byte) Math.Max(0, Top(message));
        if (!continuing)
        {
            _armed = false;
            _frontier = OpenAmount(message);
            _slips = 0;
            _left = 0f;
            _right = 0f;
            _grab = 0;
            _didPull = false;
            _sear.Clear();
            _glue.Clear();
        }
        if (!continuing && _game is SurgeryGameKind.Hold or SurgeryGameKind.Steady)
        {
            _frontier = 0f;
            _cutSent = false;
        }
        if (!_zoomed)
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

        if (_input.MouseScreenPosition.Window == WindowId.Invalid)
            return;

        _nickWait = MathF.Max(0f, _nickWait - frameTime);
        if (!Segment((LimbZone) _board.Zone, out var localStart, out var localEnd))
            return;
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId != Transform(patient).MapID)
            return;

        var cutting = _game == SurgeryGameKind.Cut
                      && _verb is not (SurgeryVerbKind.Retract or SurgeryVerbKind.Clamp or SurgeryVerbKind.Extract
                          or SurgeryVerbKind.Insert or SurgeryVerbKind.SawBone or SurgeryVerbKind.Drill);
        if (cutting && down && !_down)
        {
            var near = (mouse.Position - ToWorld(patient, localEnd)).LengthSquared();
            var far = (mouse.Position - ToWorld(patient, localStart)).LengthSquared();
            _reverse = near < far;
        }

        if (cutting && _reverse)
            (localStart, localEnd) = (localEnd, localStart);

        var start = ToWorld(patient, localStart);
        var end = ToWorld(patient, localEnd);
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
        var onLine = side < 0.16f * Steady();
        var axis = localEnd - localStart;
        if (axis.LengthSquared() > 0.0001f)
            axis /= axis.Length();
        else
            axis = new Vector2(0f, -1f);

        if (_game is SurgeryGameKind.Sear or SurgeryGameKind.Inject or SurgeryGameKind.Dots)
        {
            TraceSear(down, mouse.Position, start, end, frameTime);
            return;
        }

        if (_game == SurgeryGameKind.Steady)
        {
            TraceSteady(down, mouse.Position, start, end, frameTime);
            return;
        }

        if (_game == SurgeryGameKind.Glue)
        {
            TraceGlue(patient, down, mouse.Position, localStart, localEnd);
            return;
        }

        if (_game == SurgeryGameKind.Stanch)
            return;

        if (_verb is SurgeryVerbKind.SawBone or SurgeryVerbKind.Drill or SurgeryVerbKind.Chop
            || _game is SurgeryGameKind.Saw or SurgeryGameKind.Drill or SurgeryGameKind.Chop)
        {
            TraceSaw(down, onLine);
            return;
        }

        if (_game == SurgeryGameKind.Hold)
        {
            var along = Vector2.Dot(rel, delta) / length2;
            var near = side < 0.45f && along > -0.25f && along < 1.25f;
            if (down && near && !_cutSent)
            {
                _frontier = MathF.Min(1f, _frontier + frameTime * 0.6f);
                if (_frontier >= 1f)
                {
                    _cutSent = true;
                    Send(1f, 0, false);
                }
            }
            else if (!down && _cutSent)
            {
                _frontier = 0f;
                _cutSent = false;
            }

            return;
        }

        if (_game == SurgeryGameKind.Tap)
        {
            if (down && !_down && onLine)
                Send(1f, 0, false);
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
            if (side < 0.28f && t <= _frontier + 0.2f)
                _frontier = MathF.Max(_frontier, t);
            else if (side < 0.28f && Cuts(_verb) && side < 0.2f)
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

    private void TraceSear(bool down, Vector2 mouse, Vector2 start, Vector2 end, float frameTime)
    {
        var length = (end - start).Length();
        var dots = _game == SurgeryGameKind.Dots;
        if (_sear.Count == 0)
        {
            var count = dots
                ? Math.Clamp((int) MathF.Round(length / 0.05f), 3, 5)
                : Math.Clamp((int) MathF.Round(length / 0.065f), 1, 4);
            var radius = dots ? 0.04f : Math.Clamp(length / (count * 2.6f), 0.03f, 0.06f);
            for (var i = 0; i < count; i++)
                _sear.Add(((i + 0.5f) / count, radius));
            _searRadius = radius;
            _cutSent = false;
        }

        if (down && !_cutSent)
        {
            for (var i = 0; i < _sear.Count; i++)
            {
                var (along, left) = _sear[i];
                if (left <= 0f)
                    continue;

                var center = start + (end - start) * along;
                if ((mouse - center).Length() > left + (dots ? 0.04f : 0.025f))
                    continue;

                _sear[i] = (along, dots ? 0f : MathF.Max(0f, left - _searRadius * frameTime / SearTime));
                break;
            }
        }

        foreach (var (_, left) in _sear)
        {
            if (left > 0f)
                return;
        }

        if (_cutSent)
            return;

        _cutSent = true;
        Send(1f, 0, false);
    }

    private void TraceSteady(bool down, Vector2 mouse, Vector2 start, Vector2 end, float frameTime)
    {
        var target = 0.5f + 0.42f * MathF.Sin(_time * 1.5f);
        _sear.Clear();
        _sear.Add((target, 0.03f));
        if (_cutSent)
        {
            if (!down)
            {
                _cutSent = false;
                _frontier = 0f;
            }

            return;
        }

        var center = start + (end - start) * target;
        if (down && (mouse - center).Length() <= 0.06f)
            _frontier = MathF.Min(1f, _frontier + frameTime * 0.45f);

        if (_frontier < 1f)
            return;

        _cutSent = true;
        Send(1f, 0, false);
    }

    private void TraceGlue(EntityUid patient, bool down, Vector2 mouse, Vector2 localStart, Vector2 localEnd)
    {
        if (_glue.Count == 0)
        {
            var span = localEnd - localStart;
            var length = span.Length();
            if (length < 0.01f)
                return;

            var dir = span / length;
            var side = new Vector2(-dir.Y, dir.X);
            var cracks = Math.Clamp((int) MathF.Round(length / 0.08f), 1, 3);
            var reach = MathF.Min(0.045f, length / (cracks * 2.2f));
            for (var crack = 0; crack < cracks; crack++)
            {
                var center = localStart + span * ((crack + 0.5f) / cracks);
                var tilt = Random.Shared.NextSingle() * 0.8f - 0.4f;
                for (var j = 0; j < 4; j++)
                {
                    var along = -reach + reach * 2f * j / 3f;
                    var wobble = (Random.Shared.NextSingle() - 0.5f) * 0.03f;
                    _glue.Add((crack, center + dir * along + side * (along * tilt + wobble), false));
                }
            }

            _cutSent = false;
        }

        if (down && !_cutSent)
        {
            for (var i = 0; i < _glue.Count; i++)
            {
                var (crack, local, done) = _glue[i];
                if (!done && (ToWorld(patient, local) - mouse).Length() < 0.035f)
                    _glue[i] = (crack, local, true);
            }
        }

        foreach (var (_, _, done) in _glue)
        {
            if (!done)
                return;
        }

        if (_cutSent)
            return;

        _cutSent = true;
        Send(1f, 0, false);
    }

    private float SawHalf()
    {
        return MathF.Max(0.03f, (0.12f - _passes * 0.03f) * Steady());
    }

    private float Steady()
    {
        return _board == null ? 1f : Math.Clamp(1f - _board.SurfaceSlip * 0.5f, 0.5f, 1f);
    }

    private void TraceSaw(bool down, bool onLine)
    {
        if (!(down && !_down) || !onLine)
            return;

        var phase = (MathF.Sin(_sawPhase) + 1f) * 0.5f;
        if (MathF.Abs(phase - _guide.SawWindow) > SawHalf())
        {
            if (_nickWait <= 0f)
            {
                _nickWait = 0.35f;
                Send(0f, 1, false);
            }

            return;
        }

        _guide.SawWindow = Random.Shared.NextSingle() * 0.7f + 0.15f;
        _passes++;
        var need = _board == null || _board.SawHits < 1 ? 3 : _board.SawHits;
        if (_passes < need)
            return;

        _passes = 0;
        Send(1f, 0, false);
    }

    private void WorkUi()
    {
        if (_board == null)
            return;

        if (_game == SurgeryGameKind.Stanch && ToolHas(SurgeryVerbKind.Stanch))
        {
            OpenWork(false);
            _map?.Bleed(_board.Bleeds);
            return;
        }

        _map?.StopBleed();
        if (_patient != null && Prone(_patient.Value))
        {
            _map?.Orphan();
            _guide.Cone = false;
            return;
        }

        var lodged = Lodged();
        var count = _board.Organs.Length + _board.Empty.Length + (lodged ? 1 : 0);
        var coneFrom = _patient is { } body && MachineBody(body) ? 0 : ConeFrom;
        if (_board.Part.Length > 0 || count <= coneFrom)
        {
            if (Inserting())
                _organ = _board.Empty[0];
            else if (_board.Organs.Length > 0)
                _organ = _board.Organs[0];
            else if (lodged)
                _organ = 254;
            _map?.Orphan();
            _guide.Cone = false;
            return;
        }

        if (lodged && ToolHas(SurgeryVerbKind.Extract))
        {
            OpenWork(false);
            return;
        }

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

    private bool Lodged()
    {
        return _board != null && _board.LodgedItem.Valid && LayerOpen(SurgeryLayerKind.Cavity);
    }

    private bool Inserting()
    {
        if (_board == null || _board.Empty.Length == 0 || !Has(_board.Verbs, SurgeryVerbKind.Insert))
            return false;

        var user = _players.LocalEntity;
        return user != null && _hands.TryGetActiveItem(user.Value, out var held) && HasComp<OrganComponent>(held);
    }

    private void OpenWork(bool pick, bool insert = false)
    {
        if (_board == null)
            return;

        _map ??= new SurgeryChestMap();
        if (!_subscribed)
        {
            _map.Picked += OnOrganPicked;
            _map.TakeLodged += OnTakeLodged;
            _map.Stanched += () => Send(1f, 0, false, SurgeryVerbKind.Stanch);
            _subscribed = true;
        }

        _workPick = pick;
        if (_map.Parent == null)
            _ui.WindowRoot.AddChild(_map);

        _map.Lodged = ItemIcon(_board.LodgedItem);
        var species = _patient is { } shown && TryComp<HumanoidAppearanceComponent>(shown, out var shownLook) ? shownLook.Species.Id : "";
        _map.Show(_board.Organs, _board.Empty, _board.Chosen, !pick && !insert, insert, (LimbZone) _board.Zone, species);
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

        if (ToolHas(SurgeryVerbKind.Extract) || _patient is { } body && MachineBody(body))
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
        _guide.SawBar = _verb is SurgeryVerbKind.SawBone or SurgeryVerbKind.Drill or SurgeryVerbKind.Chop
                        || _game is SurgeryGameKind.Saw or SurgeryGameKind.Drill or SurgeryGameKind.Chop;
        _guide.SawVertical = _board != null && (_board.Drill != 0 || _game == SurgeryGameKind.Drill);
        _guide.SawNeed = _board == null || _board.SawHits < 1 ? 3 : _board.SawHits;
        _guide.Point = _game == SurgeryGameKind.Tap;
        _guide.SawPhase = (MathF.Sin(_sawPhase) + 1f) * 0.5f;
        _guide.SawHalf = SawHalf();
        _guide.SawAt = screen;
        _guide.Saw = _passes;
        var panel = _map?.Parent != null ? (Control) _map : null;
        UIBox2? panelBox = null;
        if (panel == null)
        {
            _guide.Cone = false;
            _coneLocal = null;
        }
        else
        {
            var from = ConeAnchor(patient, screen);
            var view = _ui.RootControl.PixelSize;
            var size = panel.MinSize * UiScale();
            var body = BodyBox(patient, from);
            var x = body.Right + 16f;
            if (x + size.X > view.X - 8f)
                x = body.Left - 16f - size.X;
            var y = from.Y - size.Y * 0.5f;

            x = Math.Clamp(x, 8f, Math.Max(8f, view.X - size.X - 8f));
            y = Math.Clamp(y, 8f, Math.Max(8f, view.Y - size.Y - 8f));
            LayoutContainer.SetAnchorPreset(panel, LayoutContainer.LayoutPreset.TopLeft);
            LayoutContainer.SetMarginLeft(panel, x / UiScale());
            LayoutContainer.SetMarginTop(panel, y / UiScale());
            LayoutContainer.SetGrowHorizontal(panel, LayoutContainer.GrowDirection.End);
            LayoutContainer.SetGrowVertical(panel, LayoutContainer.GrowDirection.End);
            _guide.Cone = true;
            _guide.ConeFrom = from;
            _guide.PanelPos = panel.GlobalPixelPosition;
            _guide.PanelSize = panel.PixelSize;
            panelBox = UIBox2.FromDimensions(new Vector2(x, y), size);
        }

        PlaceHint(screen, panelBox);
    }

    private UIBox2 BodyBox(EntityUid patient, Vector2 fallback)
    {
        var box = new UIBox2(fallback, fallback);
        foreach (var corner in new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f) })
        {
            var at = _eye.WorldToScreen(ToWorld(patient, corner));
            box = new UIBox2(MathF.Min(box.Left, at.X), MathF.Min(box.Top, at.Y), MathF.Max(box.Right, at.X), MathF.Max(box.Bottom, at.Y));
        }

        return box;
    }

    private Vector2 ConeAnchor(EntityUid patient, Vector2 fallback)
    {
        if (!TryComp<SpriteComponent>(patient, out var sprite))
            return fallback;

        if (_coneLocal is not { } local || _coneFor != patient)
        {
            var start = ToLocal(patient, sprite, _guide.Start);
            var end = ToLocal(patient, sprite, _guide.End);
            local = (start + end) * 0.5f;
            var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
            var span = end - start;
            if (mouse.MapId != MapId.Nullspace && span.LengthSquared() > 0.0001f)
            {
                var picked = ToLocal(patient, sprite, mouse.Position);
                var t = Math.Clamp(Vector2.Dot(picked - start, span) / span.LengthSquared(), 0f, 1f);
                local = start + span * t;
            }

            _coneLocal = local;
            _coneFor = patient;
        }

        return _eye.WorldToScreen(ToWorld(patient, local));
    }

    private void PlaceHint(Vector2 screen, UIBox2? panel = null)
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
        if (_hint.Parent is { } root && _hint.GetPositionInParent() != root.ChildCount - 1)
            _hint.SetPositionLast();

        var shown = (SurgeryVerbKind) _board.StepVerb;
        if (_board.CloseVerb != 255 && _verb == (SurgeryVerbKind) _board.CloseVerb)
            shown = _verb;
        var need = _board.SawHits < 1 ? 3 : _board.SawHits;
        var suffix = _game is SurgeryGameKind.Saw or SurgeryGameKind.Drill or SurgeryGameKind.Chop
            ? Loc.GetString("surgery-hint-left", ("n", Math.Max(1, need - _passes)))
            : "";
        var extra = "";
        if (Lodged() || OrgansReady())
            extra = _patient is { } body && MachineBody(body) ? "MachinePry" : "Extract";
        _hint.Show(shown, SurgeryVerbKind.Inspect, shown == (SurgeryVerbKind) _board.StepVerb ? _board.StepHint : "", suffix, extra);
        _hint.SetSize = _hint.MinSize;
        var size = _hint.MinSize * UiScale();
        var view = _ui.RootControl.PixelSize;
        var x = screen.X + 20f;
        var y = screen.Y - 120f;
        if (panel is { } box)
        {
            x = box.Left;
            y = box.Top - size.Y - 10f;
            if (y < 8f)
                y = box.Bottom + 10f;
        }

        x = Math.Clamp(x, 8f, Math.Max(8f, view.X - size.X - 8f));
        y = Math.Clamp(y, 8f, Math.Max(8f, view.Y - size.Y - 8f));
        LayoutContainer.SetAnchorPreset(_hint, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetMarginLeft(_hint, x / UiScale());
        LayoutContainer.SetMarginTop(_hint, y / UiScale());
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

        return !HasComp<OrganComponent>(held) && !HasComp<BodyPartComponent>(held) && !HasComp<SurgeryAppendageComponent>(held);
    }

    private bool GraftMaterial(EntityUid patient, EntityUid held)
    {
        var stack = GraftStack(patient);
        if (stack.Length > 0)
            return TryComp<Content.Shared.Stacks.StackComponent>(held, out var pile) && pile.StackTypeId == stack;

        return HasComp<SurgerySkinRollComponent>(held);
    }

    private string GraftStack(EntityUid patient)
    {
        return TryComp<HumanoidAppearanceComponent>(patient, out var look)
               && _proto.TryIndex<SurgerySpeciesPrototype>(look.Species, out var species)
            ? species.GraftStack
            : "";
    }

    private bool ToolHas(SurgeryVerbKind verb)
    {
        if (_tool != null && verb == SurgeryVerbKind.Cauterize && HasComp<WelderComponent>(_tool.Value) && !_toggle.IsActivated(_tool.Value))
            return false;

        if (_tool != null && verb == SurgeryVerbKind.Graft && _patient is { } bark && GraftMaterial(bark, _tool.Value))
            return true;

        if (_tool != null && verb == SurgeryVerbKind.MendBone
            && _patient is { } gelBody && SoftBody(gelBody)
            && HasComp<SurgerySlimeGelComponent>(_tool.Value))
            return true;

        if (_tool != null && verb == SurgeryVerbKind.SealSap && HasComp<SurgeryDionaSapComponent>(_tool.Value))
            return true;

        if (_tool != null && verb == SurgeryVerbKind.Seat && HasComp<BodyPartComponent>(_tool.Value))
            return true;

        if (_tool != null && verb == SurgeryVerbKind.Bandage && Wraps(_tool.Value))
            return true;

        if (_tool != null && verb == SurgeryVerbKind.Append && HasComp<SurgeryAppendageComponent>(_tool.Value))
            return true;

        if (_tool != null && verb == SurgeryVerbKind.Inject && HasComp<SolutionContainerManagerComponent>(_tool.Value))
            return true;

        var machine = _patient is { } m && MachineBody(m);
        var plant = _patient is { } p && PlantBody(p);
        var soft = _patient is { } s && SoftBody(s);
        return _tool != null
               && TryComp<SurgeryToolComponent>(_tool.Value, out var tool)
               && tool.VerbsFor(machine, plant, soft).Contains(verb);
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
        var opened = _left > 0.09f && _right > 0.09f || MathF.Max(_left, _right) > 0.14f;
        _left = 0.035f;
        _right = 0.035f;
        if (opened && !LayerHeld())
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
            _nicks.Add((patient, _board == null ? LimbZone.Torso : (LimbZone) _board.Zone, local, dir));

        Send(0f, 1, false);
    }

    private void PlaceNicks()
    {
        _guide.Cuts.Clear();
        foreach (var nick in _nicks)
        {
            if (!Exists(nick.Body) || ZoneSevered(nick.Body, nick.Zone))
                continue;

            _guide.Cuts.Add((ToWorld(nick.Body, nick.Local - nick.Dir * 0.05f), ToWorld(nick.Body, nick.Local + nick.Dir * 0.05f)));
        }
    }

    private static bool Cuts(SurgeryVerbKind verb)
    {
        return verb is SurgeryVerbKind.Incise or SurgeryVerbKind.SawBone or SurgeryVerbKind.Amputate;
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
        Guide(patient, localStart, localEnd, MarkFor(_verb, _game));
        _guide.Ink = DashInk(patient);
        _guide.Radius = 0.014f;
        _guide.Cover = _frontier;
        _guide.Fill = _game is SurgeryGameKind.Hold or SurgeryGameKind.Steady && _verb != SurgeryVerbKind.Inspect ? _frontier : 0f;
        _guide.Left = MathF.Max(_left, 0.08f);
        _guide.Right = MathF.Max(_right, 0.08f);
        _guide.Sear.Clear();
        _guide.Sear.AddRange(_sear);
        _guide.SearGreen = _game == SurgeryGameKind.Inject;
        _guide.Glue.Clear();
        foreach (var (crack, local, done) in _glue)
            _guide.Glue.Add((crack, ToWorld(patient, local), done));
    }

    private void Guide(EntityUid patient, Vector2 localStart, Vector2 localEnd, SurgeryMark mark)
    {
        _guide.Anchor = patient;
        _guide.LocalA = localStart;
        _guide.LocalB = localEnd;
        _guide.Start = ToWorld(patient, localStart);
        _guide.End = ToWorld(patient, localEnd);
        _guide.Mark = mark;
        _guide.Fill = 0f;
        _guide.Sear.Clear();
        _guide.Active = true;
    }

    public Vector2 ToWorld(EntityUid uid, Vector2 local)
    {
        var pos = _transform.GetWorldPosition(uid);
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return pos + local;

        return pos + sprite.Offset + sprite.Rotation.RotateVec(local * sprite.Scale);
    }

    private Vector2 ToLocal(EntityUid uid, SpriteComponent sprite, Vector2 world)
    {
        var rel = (-sprite.Rotation).RotateVec(world - _transform.GetWorldPosition(uid) - sprite.Offset);
        return new Vector2(rel.X / MathF.Max(0.05f, sprite.Scale.X), rel.Y / MathF.Max(0.05f, sprite.Scale.Y));
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

    private void Pick(EntityUid user, float frameTime)
    {
        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
        if (_picking == null || !Exists(_picking.Value) || !MarkHand(user, out var thick, out var ink)
            || _input.IsKeyDown(Keyboard.Key.MouseRight))
        {
            CancelPick();
            _down = down;
            return;
        }

        _guide.Radius = thick ? 0.014f : 0.009f;
        _guide.Ink = ink;
        if (_headMenu?.Parent != null && _menuFor is { } menu && Exists(menu.Patient))
        {
            var box = UIBox2.FromDimensions(_headMenu.GlobalPixelPosition, _headMenu.PixelSize);
            if (down && !_down && !box.Contains(_input.MouseScreenPosition.Position))
            {
                _headMenu.Orphan();
                _headMenu = null;
                _menuFor = null;
                _preview = null;
            }
            else
            {
                if (Segment(menu.Zone, out var menuStart, out var menuEnd))
                    Guide(menu.Patient, menuStart, menuEnd, SurgeryMark.Dash);
                _down = down;
                return;
            }
        }

        if (!TryAim(user, out var patient, out var zone, out _) || patient != _picking || !Offers(patient, zone) || !Segment(zone, out var localStart, out var localEnd))
        {
            _guide.Active = false;
            _down = down;
            return;
        }

        Guide(patient, localStart, localEnd, SurgeryMark.Dash);
        if (down && !_down)
        {
            if (!ShowMenu(patient, zone))
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

    private void CancelPick()
    {
        _picking = null;
        _hint?.Orphan();
        _headMenu?.Orphan();
        _headMenu = null;
        _menuFor = null;
        _preview = null;
        if (_zoomed)
        {
            RaiseNetworkEvent(new SurgeryFocusMessage { On = 0 });
            _zoomed = false;
        }

        _guide.Active = false;
    }

    private void Stay(EntityUid patient)
    {
        if (!TryComp<SurgerySiteComponent>(patient, out var site) || !CutOpen(site) || site.Back != Prone(patient)
            || !Line(site.Zone, site.Part, AcrossOf(site), site.Back, out var localStart, out var localEnd))
        {
            _stay = null;
            _guide.Active = false;
            return;
        }

        Guide(patient, localStart, localEnd, SurgeryMark.Dash);
        _guide.Ink = DashInk(patient);
        _guide.Radius = 0.014f;
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

    private Color DashInk(EntityUid? patient)
    {
        return patient != null && MachineBody(patient.Value) ? MachineInk : FleshInk;
    }

    private bool CanMark(EntityUid user, EntityUid patient)
    {
        if (!_hands.TryGetActiveItem(user, out var held))
            return false;

        var machine = MachineBody(patient);
        if (TryComp<SurgeryToolComponent>(held, out var tool) && tool.Probe)
            return machine;

        return !machine && (HasComp<SurgeryMarkerComponent>(held) || _tags.HasTag(held.Value, "Pen"));
    }

    private bool MarkHand(EntityUid user, out bool thick, out Color ink)
    {
        thick = true;
        ink = FleshInk;
        if (!_hands.TryGetActiveItem(user, out var held))
            return false;

        if (TryComp<SurgeryToolComponent>(held, out var tool) && tool.Probe)
        {
            ink = MachineInk;
            return true;
        }

        if (HasComp<SurgeryMarkerComponent>(held))
            return true;

        if (!_tags.HasTag(held.Value, "Pen"))
            return false;

        thick = false;
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

            if (TryComp<HumanoidAppearanceComponent>(uid, out var kind) && !SurgerySpeciesPrototype.CanOperate(_proto, kind.Species.Id))
                continue;

            if (!CanMark(user, uid))
                continue;

            var local = ToLocal(uid, sprite, mouse.Position);
            var px = local.X * BodyTexels + BodyTexels * 0.5f;
            if (Prone(uid))
                px = BodyTexels - 1f - px;
            var py = BodyTexels * 0.5f - local.Y * BodyTexels;
            foreach (var candidate in _proto.EnumeratePrototypes<SurgeryMarkPrototype>())
            {
                if (candidate.Hw <= 0 || candidate.Hh <= 0 || !Enum.TryParse<LimbZone>(candidate.ID, out var part))
                    continue;

                if (part is LimbZone.Eyes or LimbZone.Mouth)
                    continue;

                if (px < candidate.Hx || py < candidate.Hy || px > candidate.Hx + candidate.Hw || py > candidate.Hy + candidate.Hh)
                    continue;

                var middle = new Vector2(candidate.Hx + candidate.Hw * 0.5f - px, candidate.Hy + candidate.Hh * 0.5f - py);
                var dist = (mouse.Position - there).LengthSquared() + middle.LengthSquared() * 0.0001f;
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

    private bool ShowMenu(EntityUid patient, LimbZone zone)
    {
        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid)
            return false;

        var entries = new List<SurgeryFanIcon>();
        var acts = new List<Action>();
        var previews = new List<(string Part, bool Across)>();
        var name = zone.ToString();
        var prone = Prone(patient);
        var parts = new List<SurgeryPartPrototype>();
        foreach (var part in _proto.EnumeratePrototypes<SurgeryPartPrototype>())
        {
            if (!prone && part.Zone == name && part.Machine == MachineBody(patient) && !ZoneSevered(patient, zone) && !PartBlocked(patient, part))
                parts.Add(part);
        }

        parts.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var part in parts)
        {
            var layers = new List<Texture>();
            foreach (var state in part.States.Count > 0 ? part.States : new List<string> { part.State })
            {
                if (Frame(part.Sprite, state) is { } layer)
                    layers.Add(layer);
            }

            if (layers.Count == 0)
                continue;

            var badge = SurgeryFanBadge.None;
            var partOperation = part.Operation;
            if (part.Organ.Length > 0 && Enum.TryParse<LimbOrgan>(part.Organ, out var organ))
            {
                if (!TryComp<LimbBodyComponent>(patient, out var body) || (body.Present & (LimbOrganMask) (1 << (int) organ)) == 0)
                    continue;

                var missing = OrganGone(patient, organ);
                if (missing && part.Put.Length == 0)
                    continue;

                badge = missing ? SurgeryFanBadge.Put : SurgeryFanBadge.Take;
                if (missing)
                    partOperation = part.Put;
            }
            else if (part.Implant.Length > 0)
            {
                if (!_implants.HasSlot(patient, part.Implant))
                    continue;

                var empty = !_implants.Occupied(patient, part.Implant);
                if (empty && (part.Put.Length == 0
                              || !_proto.TryIndex<SurgeryOperationPrototype>(part.Put, out var put)
                              || !Suits(patient, put)
                              || !_nearby.Has(patient, put, _players.LocalEntity)))
                    continue;

                badge = empty ? SurgeryFanBadge.Put : SurgeryFanBadge.Take;
                if (empty)
                    partOperation = part.Put;
            }
            else if (part.Put.Length > 0)
            {
                var gone = TryComp<SurgeryPartsGoneComponent>(patient, out var lost) && lost.Parts.Contains(part.ID);
                badge = gone ? SurgeryFanBadge.Put : SurgeryFanBadge.Take;
                if (gone)
                    partOperation = part.Put;
            }

            var (partColor, partName) = FanLook(partOperation);
            entries.Add(new SurgeryFanIcon(layers, badge, partColor, partName));
            var id = part.ID;
            acts.Add(() => ChoosePart(patient, zone, id));
            previews.Add((id, false));
        }

        var operations = new List<SurgeryOperationPrototype>();
        var field = !OnTable(patient);
        var hasField = false;
        foreach (var operation in _proto.EnumeratePrototypes<SurgeryOperationPrototype>())
        {
            if (!MenuFits(patient, zone, operation))
                continue;

            operations.Add(operation);
            hasField |= operation.Field;
        }

        operations.RemoveAll(operation => operation.Field != (field && hasField));
        operations.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var operation in operations)
        {
            if (Frame(operation.Icon!.Sprite, operation.Icon.State) is not { } icon)
                continue;

            var badge = operation.Refill && ZoneMissing(patient, zone) ? SurgeryFanBadge.Put : SurgeryFanBadge.None;
            var (color, title) = FanLook(operation.ID);
            entries.Add(new SurgeryFanIcon(new List<Texture> { icon }, badge, color, title));
            var id = operation.ID;
            acts.Add(() => ChooseOperation(patient, zone, id));
            previews.Add(("", operation.Across));
        }

        if (entries.Count == 0)
            return false;

        _menuFor = (patient, zone);
        ShowFan(patient, mouse.Position, entries, acts, previews);
        return true;
    }

    private (Color Color, string Name) FanLook(string operationId)
    {
        if (!_proto.TryIndex<SurgeryOperationPrototype>(operationId, out var operation))
            return (Color.White, "");

        var name = operation.Name.Length > 0 ? Loc.GetString(operation.Name) : "";
        var key = operation.Steps.Count > 0 ? operation.Steps[0].Verb : SurgeryVerbKind.Inspect;
        foreach (var step in operation.Steps)
        {
            if (step.Verb is SurgeryVerbKind.Incise or SurgeryVerbKind.Retract)
                continue;

            key = step.Verb;
            break;
        }

        var color = _proto.TryIndex<SurgeryHintPrototype>(key.ToString(), out var hint) ? hint.Color : Color.White;
        return (color, name);
    }

    private bool Offers(EntityUid patient, LimbZone zone)
    {
        if (!Prone(patient))
            return true;

        foreach (var operation in _proto.EnumeratePrototypes<SurgeryOperationPrototype>())
        {
            if (operation.Icon != null
                    ? MenuFits(patient, zone, operation)
                    : operation.Faces(true) && operation.Fits(zone) && operation.When.Length > 0 && Suits(patient, operation)
                      && !ZoneSevered(patient, zone) && NeedsCare(patient, zone, operation.When))
                return true;
        }

        return false;
    }

    private bool MenuFits(EntityUid patient, LimbZone zone, SurgeryOperationPrototype operation)
    {
        if (operation.Icon == null || !operation.Faces(Prone(patient)) || operation.Field && OnTable(patient))
            return false;

        if (!operation.Fits(zone) || !Suits(patient, operation))
            return false;

        if (!_nearby.Has(patient, operation, _players.LocalEntity))
            return false;

        if (operation.Appendage.Length > 0
            && (!SurgeryAppendagePrototype.Allowed(_proto, EntityManager, patient, operation.Appendage)
                || operation.Attach == HasAppendage(patient, operation.Appendage)))
            return false;

        if (operation.Implant.Length > 0)
        {
            if (!_implants.HasSlot(patient, operation.Implant))
                return false;

            if (operation.ImplantPut ? !_implants.Free(patient, operation.Implant) : !_implants.Occupied(patient, operation.Implant))
                return false;
        }

        var severed = ZoneSevered(patient, zone);
        if (operation.Stump)
            return severed;

        if (severed || operation.Whole && !LimbWoundRules.CanSever(zone))
            return false;

        return operation.When.Length == 0 || NeedsCare(patient, zone, operation.When);
    }

    private bool NeedsCare(EntityUid patient, LimbZone zone, string when)
    {
        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count)
            return false;

        var infected = TryComp<SurgeryInfectionComponent>(patient, out var infection) && infection.Rots(zone);
        return SurgeryConditions.Has(when, limbs.Zones[index], HasBone(patient, zone), infected, SurgeryConditions.OrganCracked(limbs.Organs, zone));
    }

    private bool HasBone(EntityUid patient, LimbZone zone)
    {
        var anatomy = zone switch
        {
            LimbZone.Torso => "Torso",
            LimbZone.Head => "Head",
            LimbZone.Groin => "Groin",
            _ => "FleshLimb",
        };

        if (TryComp<HumanoidAppearanceComponent>(patient, out var look)
            && _proto.TryIndex<SurgerySpeciesPrototype>(look.Species.Id, out var species))
        {
            var pick = zone switch
            {
                LimbZone.Torso => species.Torso,
                LimbZone.Head => species.Head,
                LimbZone.Groin => species.Groin,
                _ => species.Limb,
            };
            if (pick is { } chosen && chosen.Id.Length > 0)
                anatomy = chosen.Id;
        }

        return _proto.TryIndex<SurgeryAnatomyPrototype>(anatomy, out var found) && found.Layers.Contains(SurgeryLayerKind.Bone);
    }

    private bool HasAppendage(EntityUid patient, string id)
    {
        return _proto.TryIndex<SurgeryAppendagePrototype>(id, out var appendage)
               && TryComp<HumanoidAppearanceComponent>(patient, out var look)
               && look.MarkingSet.Markings.TryGetValue(appendage.Category, out var list)
               && list.Count > 0;
    }

    private bool OnTable(EntityUid patient)
    {
        return TryComp<BuckleComponent>(patient, out var buckle)
               && buckle.BuckledTo is { } strap
               && HasComp<SurgerySurfaceComponent>(strap);
    }

    private bool ZoneMissing(EntityUid patient, LimbZone zone)
    {
        var name = zone.ToString();
        foreach (var organ in _proto.EnumeratePrototypes<SurgeryOrganPrototype>())
        {
            if (organ.Zone == name && Enum.TryParse<LimbOrgan>(organ.ID, out var kind) && kind != LimbOrgan.Eyes && OrganGone(patient, kind))
                return true;
        }

        return false;
    }

    private bool OrganGone(EntityUid patient, LimbOrgan organ)
    {
        if (!TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        if (organ == LimbOrgan.Eyes && limbs.EyesGone > 0)
            return true;

        var index = (int) organ;
        return (uint) index < (uint) limbs.Organs.Count && limbs.Organs[index].Missing;
    }

    private void ChoosePart(EntityUid patient, LimbZone zone, string id)
    {
        _headMenu?.Orphan();
        _headMenu = null;
        _picking = null;
        _preview = null;
        RaiseNetworkEvent(new SurgeryZoneMessage
        {
            Patient = GetNetEntity(patient),
            Zone = (byte) zone,
            Part = id,
        });
    }

    private void ShowFan(EntityUid patient, Vector2 mouse, List<SurgeryFanIcon> icons, List<Action> acts, List<(string Part, bool Across)> previews)
    {
        _headMenu?.Orphan();
        _preview = null;
        _fanLocal = null;
        if (icons.Count == 0)
            return;

        if (TryComp<SpriteComponent>(patient, out var sprite))
        {
            var world = _eye.PixelToMap(_input.MouseScreenPosition);
            if (world.MapId != MapId.Nullspace)
            {
                _fanLocal = ToLocal(patient, sprite, world.Position);
                _fanFor = patient;
            }
        }

        var fan = new SurgeryFan();
        fan.SetIcons(icons);
        fan.Picked += index =>
        {
            if ((uint) index < (uint) acts.Count)
                acts[index]();
        };
        fan.Hovered += index =>
        {
            _preview = (uint) index < (uint) previews.Count ? previews[index] : null;
        };
        _ui.WindowRoot.AddChild(fan);
        LayoutContainer.SetAnchorPreset(fan, LayoutContainer.LayoutPreset.TopLeft);
        PinFan(fan, mouse);
        _headMenu = fan;
    }

    private void KeepFan()
    {
        if (_headMenu is not SurgeryFan { Parent: not null } fan || _fanLocal is not { } local || !Exists(_fanFor))
            return;

        PinFan(fan, _eye.WorldToScreen(ToWorld(_fanFor.Value, local)));
    }

    private void PinFan(SurgeryFan fan, Vector2 pixel)
    {
        var at = pixel / UiScale();
        LayoutContainer.SetMarginLeft(fan, at.X - fan.MinSize.X * fan.Origin.X);
        LayoutContainer.SetMarginTop(fan, at.Y - fan.MinSize.Y * fan.Origin.Y);
    }

    private float UiScale()
    {
        return MathF.Max(0.01f, _ui.RootControl.UIScale);
    }

    private Texture? Frame(string path, string state)
    {
        if (path.Length == 0 || state.Length == 0)
            return null;

        var rsi = _cache.GetResource<RSIResource>(TexturePath(path));
        return rsi.RSI.TryGetState(state, out var frame) ? frame.GetFrame(RsiDirection.South, 0) : null;
    }

    public static string TexturePath(string path)
    {
        return path.StartsWith('/') ? path : "/Textures/" + path;
    }

    private void ChooseOperation(EntityUid patient, LimbZone zone, string operation)
    {
        _headMenu?.Orphan();
        _headMenu = null;
        _picking = null;
        RaiseNetworkEvent(new SurgeryZoneMessage
        {
            Patient = GetNetEntity(patient),
            Zone = (byte) zone,
            Operation = operation,
        });
    }

    private bool PartBlocked(EntityUid patient, SurgeryPartPrototype part)
    {
        if (part.Operation.Length == 0 || !_proto.TryIndex<SurgeryOperationPrototype>(part.Operation, out var operation))
            return true;

        return !Suits(patient, operation);
    }

    private bool Suits(EntityUid patient, SurgeryOperationPrototype operation)
    {
        return operation.Suits(MachineBody(patient), SoftBody(patient), PlantBody(patient));
    }

    private bool ZoneSevered(EntityUid patient, LimbZone zone)
    {
        if (!TryComp<LimbBodyComponent>(patient, out var limbs))
            return false;

        var index = LimbWoundRules.Index(zone);
        return (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Severed;
    }

    private bool Segment(LimbZone zone, out Vector2 start, out Vector2 end)
    {
        var patient = _board != null && TryGetEntity(_board.Patient, out var board) ? board : _picking;
        var stump = patient != null && ZoneSevered(patient.Value, zone);
        var back = patient != null && Prone(patient.Value);
        if (_preview is { } preview && _board == null && _headMenu?.Parent != null && _menuFor?.Zone == zone)
            return Line(zone, preview.Part, preview.Across || stump, back, out start, out end);

        return Line(zone, _board?.Part ?? "", _board is { Across: not 0 } || stump, back, out start, out end);
    }

    private bool Line(LimbZone zone, string partId, bool across, bool back, out Vector2 start, out Vector2 end)
    {
        start = default;
        end = default;
        if (partId.Length > 0
            && _proto.TryIndex<SurgeryPartPrototype>(partId, out var part)
            && (part.Ax != part.Bx || part.Ay != part.By))
        {
            start = Texel(part.Ax, part.Ay, back);
            end = Texel(part.Bx, part.By, back);
            return true;
        }

        if (!MarkOf(zone, back, out var mark))
            return false;

        var ax = mark.Ax;
        var ay = mark.Ay;
        var bx = mark.Bx;
        var by = mark.By;
        if (across && (mark.Cx != mark.Dx || mark.Cy != mark.Dy))
        {
            ax = mark.Cx;
            ay = mark.Cy;
            bx = mark.Dx;
            by = mark.Dy;
        }

        start = Texel(ax, ay, back);
        end = Texel(bx, by, back);
        return true;
    }

    private bool MarkOf(LimbZone zone, bool back, out SurgeryMarkPrototype mark)
    {
        if (back && _proto.TryIndex(zone + BackSuffix, out SurgeryMarkPrototype? behind))
        {
            mark = behind;
            return true;
        }

        if (_proto.TryIndex(zone.ToString(), out SurgeryMarkPrototype? front))
        {
            mark = front;
            return true;
        }

        mark = null!;
        return false;
    }

    private static Vector2 Texel(float x, float y, bool back)
    {
        return Texel(back ? BodyTexels - 1f - x : x, y);
    }

    private static Vector2 Texel(float x, float y)
    {
        return new Vector2((x - BodyTexels * 0.5f) / BodyTexels, (BodyTexels * 0.5f - y) / BodyTexels);
    }

    private bool AcrossOf(SurgerySiteComponent site)
    {
        return AcrossOf(site.Part, site.Queued);
    }

    private bool AcrossOf(string part, string queued)
    {
        var id = queued;
        if (part.Length > 0 && _proto.TryIndex<SurgeryPartPrototype>(part, out var proto) && proto.Operation.Length > 0)
            id = proto.Operation;

        return id.Length > 0 && _proto.TryIndex<SurgeryOperationPrototype>(id, out var operation) && operation.Across;
    }

    private static SurgeryMark MarkFor(SurgeryVerbKind verb, SurgeryGameKind game)
    {
        if (game is SurgeryGameKind.Sear or SurgeryGameKind.Inject or SurgeryGameKind.Dots)
            return SurgeryMark.Sear;
        if (game == SurgeryGameKind.Steady)
            return SurgeryMark.Dash;
        if (game == SurgeryGameKind.Glue)
            return SurgeryMark.Glue;
        if (game == SurgeryGameKind.Stanch)
            return SurgeryMark.Fine;
        if (verb == SurgeryVerbKind.Retract || game == SurgeryGameKind.Spread)
            return SurgeryMark.Spread;
        if (game == SurgeryGameKind.Hold)
            return SurgeryMark.Dash;
        if (game == SurgeryGameKind.Tap)
            return SurgeryMark.Fine;
        if (game is SurgeryGameKind.Saw or SurgeryGameKind.Chop || verb is SurgeryVerbKind.SawBone or SurgeryVerbKind.Chop)
            return SurgeryMark.None;
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
        _guide.Bubble = false;
        _guide.Mark = SurgeryMark.None;
        _map?.Orphan();
        _hint?.Orphan();
        _board = null;
        _patient = null;
        _tool = null;
        _armed = false;
    }

    private void Paint(EntityUid patient, float amount)
    {
        if (_board == null)
            return;

        var board = _board;
        var state = WoundState(amount);
        if (MarkOf((LimbZone) board.Zone, _patient != null && Prone(_patient.Value), out var mark)
            && mark.Halves
            && mark.Prefix.Length > 0
            && (state.StartsWith(mark.Prefix + "_cut") || state == mark.Prefix + "_open")
            && _game == SurgeryGameKind.Spread
            && _didPull)
        {
            var left = _left > 0.08f;
            var right = _right > 0.08f;
            if (left && !right)
                state = mark.Prefix + "_open_l";
            else if (right && !left)
                state = mark.Prefix + "_open_r";
            else if (left && right)
                state = mark.Prefix + "_open";
        }

        ShowWound(patient, (LimbZone) board.Zone, board.Part, board.Across != 0, state);
    }

    private void OnSite(Entity<SurgerySiteComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ShowWounds(ent, ent.Comp, Prone(ent));
    }

    private void OnSiteGone(Entity<SurgerySiteComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            HideWounds(ent, sprite, []);
    }

    private string WoundOf(LimbZone zone, string part, bool back, IReadOnlyList<SurgeryLayerState> layers, bool across, bool prone)
    {
        if (layers.Count > 0 && back != prone)
            return "none";

        var count = layers.Count;
        if (count == 0 || PartHidden(part))
            return "none";

        var kinds = new byte[count];
        var integrity = new float[count];
        var retracted = new byte[count];
        for (var i = 0; i < count; i++)
        {
            kinds[i] = (byte) layers[i].Kind;
            integrity[i] = layers[i].Integrity;
            retracted[i] = layers[i].Retracted ? (byte) 1 : (byte) 0;
        }

        var ribs = !across && MarkOf(zone, back, out var mark) && mark.Prefix.Length > 0 && mark.Ribs;
        return SurgeryProcedure.Wound(Prefix(zone, across, back), kinds, integrity, retracted, 1f, ribs);
    }

    private string Prefix(LimbZone zone, bool across, bool back)
    {
        return !across && MarkOf(zone, back, out var mark) && mark.Prefix.Length > 0
            ? mark.Prefix
            : LinePrefix;
    }

    private bool PartHidden(string part)
    {
        return part.Length > 0 && _proto.TryIndex<SurgeryPartPrototype>(part, out var proto) && !proto.Wound;
    }

    private bool WoundsOn(EntityUid uid)
    {
        return SpeciesOf(uid) is not { } species || species.Wounds;
    }

    private SurgeryInk InkOf(EntityUid uid)
    {
        return TryComp<HumanoidAppearanceComponent>(uid, out var look) ? SurgerySpeciesPrototype.InkOf(_proto, look.Species.Id) : SurgeryInk.Flesh;
    }

    private ResPath WoundRsi(EntityUid uid)
    {
        return SpeciesOf(uid) is { WoundSprite.Length: > 0 } species
            ? new ResPath(species.WoundSprite)
            : Rsi;
    }

    private SurgerySpeciesPrototype? SpeciesOf(EntityUid uid)
    {
        return TryComp<HumanoidAppearanceComponent>(uid, out var look)
               && _proto.TryIndex<SurgerySpeciesPrototype>(look.Species.Id, out var species)
            ? species
            : null;
    }

    private void ShowWounds(EntityUid uid, SurgerySiteComponent site, bool prone)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        SurgeryWounds.Collect(site, _openWounds);
        var shown = new HashSet<string>();
        foreach (var wound in _openWounds)
        {
            var across = AcrossOf(wound.Part, wound.Queued);
            var state = WoundOf(wound.Zone, wound.Part, wound.Back, wound.Layers, across, prone);
            ShowWound(uid, sprite, wound.Zone, wound.Part, across, state, prone, shown);
        }

        HideWounds(uid, sprite, shown);
    }

    private void ShowWound(EntityUid uid, LimbZone zone, string part, bool across, string state, bool? side = null)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        ShowWound(uid, sprite, zone, part, across, state, side ?? Prone(uid), null);
    }

    private void ShowWound(EntityUid uid, SpriteComponent sprite, LimbZone zone, string part, bool across, string state, bool prone, HashSet<string>? shown)
    {
        var visible = WoundsOn(uid) && state != "none";
        var line = visible && state.StartsWith(LinePrefix + "_");
        var art = visible && !line;
        var artKey = WoundPrefix + zone;
        var lineKey = WoundPrefix + "line-" + zone;
        SetWoundLayer(uid, sprite, artKey, WoundRsi(uid), art ? state : null);
        SetWoundLayer(uid, sprite, lineKey, LineRsi, line ? state[(LinePrefix.Length + 1)..] : null);
        if (art)
        {
            _sprites.LayerSetOffset((uid, sprite), artKey, Vector2.Zero);
            _sprites.LayerSetRotation((uid, sprite), artKey, Angle.Zero);
            _sprites.LayerSetScale((uid, sprite), artKey, new Vector2(BodyTexels / RsiSize(Rsi).X));
            TrackWound(uid, artKey, shown);
        }

        if (line && Line(zone, part, across || ZoneSevered(uid, zone), prone, out var start, out var end))
        {
            PlaceLine(uid, sprite, lineKey, start, end, state[(LinePrefix.Length + 1)..], InkOf(uid));
            TrackWound(uid, lineKey, shown);
        }
    }

    private void PlaceLine(EntityUid uid, SpriteComponent sprite, string layer, Vector2 start, Vector2 end, string state, SurgeryInk ink)
    {
        var a = new Vector2(start.X * BodyTexels + BodyTexels * 0.5f, BodyTexels * 0.5f - start.Y * BodyTexels);
        var b = new Vector2(end.X * BodyTexels + BodyTexels * 0.5f, BodyTexels * 0.5f - end.Y * BodyTexels);
        var key = $"{a.X:F1}:{a.Y:F1}:{b.X:F1}:{b.Y:F1}:{state}:{(byte) ink}";
        if (!_woundArt.TryGetValue(key, out var texture))
        {
            texture = _clyde.LoadTextureFromImage(SurgeryWoundPainter.Paint(a, b, state, ink), "surgery-wound");
            _woundArt[key] = texture;
        }

        _sprites.LayerSetTexture((uid, sprite), layer, texture);
        _sprites.LayerSetOffset((uid, sprite), layer, Vector2.Zero);
        _sprites.LayerSetRotation((uid, sprite), layer, Angle.Zero);
        _sprites.LayerSetScale((uid, sprite), layer, Vector2.One);
    }

    private void TrackWound(EntityUid uid, string key, HashSet<string>? shown)
    {
        shown?.Add(key);
        if (!_woundKeys.TryGetValue(uid, out var keys))
        {
            keys = [];
            _woundKeys[uid] = keys;
        }

        keys.Add(key);
    }

    private void HideWounds(EntityUid uid, SpriteComponent sprite, HashSet<string> shown)
    {
        if (!_woundKeys.TryGetValue(uid, out var keys))
            return;

        List<string>? gone = null;
        foreach (var key in keys)
        {
            if (shown.Contains(key))
                continue;

            SetWoundLayer(uid, sprite, key, LineRsi, null);
            gone ??= [];
            gone.Add(key);
        }

        if (gone == null)
            return;

        foreach (var key in gone)
            keys.Remove(key);

        if (keys.Count == 0)
            _woundKeys.Remove(uid);
    }

    private void SetWoundLayer(EntityUid uid, SpriteComponent sprite, string key, ResPath rsi, string? state)
    {
        if (!sprite.LayerMapTryGet(key, out var layer))
        {
            if (state == null)
                return;

            layer = sprite.AddLayer(new SpriteSpecifier.Rsi(rsi, state));
            sprite.LayerMapSet(key, layer);
        }
        else if (state != null)
        {
            _sprites.LayerSetSprite((uid, sprite), key, new SpriteSpecifier.Rsi(rsi, state));
        }

        sprite.LayerSetVisible(layer, state != null);
    }

    private Vector2 RsiSize(ResPath rsi)
    {
        var size = _cache.GetResource<RSIResource>(rsi).RSI.Size;
        return size.X > 0 && size.Y > 0 ? new Vector2(size.X, size.Y) : new Vector2(BodyTexels);
    }

    private string WoundState(float amount)
    {
        if (_board == null || PartHidden(_board.Part))
            return "none";

        var zone = (LimbZone) _board.Zone;
        var back = _patient != null && Prone(_patient.Value);
        SurgeryMarkPrototype? sheet = null;
        if (_board.Across == 0 && MarkOf(zone, back, out var found) && found.Prefix.Length > 0)
            sheet = found;
        var prefix = sheet != null ? sheet.Prefix : LinePrefix;
        var ribs = sheet is { Ribs: true };
        var brain = sheet is { Brain: true };
        if (LayerOpen(SurgeryLayerKind.Cavity) && !BoneShut() || LayerOpen(SurgeryLayerKind.Globe))
        {
            if (brain)
                return BrainThere() ? prefix + "_brain" : prefix + "_organs";
            if (prefix == LinePrefix)
                return LinePrefix + "_deep";
            return ribs ? prefix + "_organs" : prefix + "_open";
        }

        if (LayerOpen(SurgeryLayerKind.Bone))
            return ribs ? prefix + "_ribs" : prefix + "_open";

        var layer = _board.StepLayer;
        if (layer != 255 && layer < _board.LayerKinds.Length && layer < _board.Retracted.Length)
        {
            var kind = (SurgeryLayerKind) _board.LayerKinds[layer];
            var held = _board.Retracted[layer] != 0;
            if (kind == SurgeryLayerKind.Skin && !held)
                return CutLook(prefix, amount);
            if (kind == SurgeryLayerKind.Skin && held)
                return prefix + "_open";
        }

        return SurgeryProcedure.Wound(prefix, _board.LayerKinds, _board.Integrity, _board.Retracted, amount, ribs);
    }

    private bool BrainThere()
    {
        if (_board == null)
            return false;

        foreach (var organ in _board.Organs)
        {
            if ((LimbOrgan) organ == LimbOrgan.Brain)
                return true;
        }

        return false;
    }

    private bool FewOrgans()
    {
        return OrgansReady()
               && _board!.Organs.Length > 0
               && _board.Organs.Length + _board.Empty.Length <= ConeFrom
               && _patient is { } body && !MachineBody(body);
    }

    private bool OrgansReady()
    {
        return _board != null
               && _board.Part.Length == 0
               && _board.Organs.Length > 0
               && _board.CloseVerb != 255
               && LayerOpen(SurgeryLayerKind.Cavity)
               && !BoneShut()
               && (_patient == null || !Prone(_patient.Value));
    }

    private bool BoneShut()
    {
        return _board != null
               && Array.IndexOf(_board.LayerKinds, (byte) SurgeryLayerKind.Bone) >= 0
               && !LayerOpen(SurgeryLayerKind.Bone);
    }

    private bool LayerOpen(SurgeryLayerKind kind)
    {
        if (_board == null)
            return false;

        var count = Math.Min(_board.LayerKinds.Length, Math.Min(_board.Integrity.Length, _board.Retracted.Length));
        for (var i = 0; i < count; i++)
        {
            if ((SurgeryLayerKind) _board.LayerKinds[i] != kind)
                continue;

            return _board.Retracted[i] != 0 || _board.Integrity[i] < 0.9f;
        }

        return false;
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
