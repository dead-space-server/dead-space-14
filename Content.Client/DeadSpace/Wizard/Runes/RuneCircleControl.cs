// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneCircleControl : LayoutContainer
{
    public event Action<List<Vector2>>? StrokeFinished;
    public event Action? CastRequested;
    public event Action? InscribeRequested;
    public event Action? ClearRequested;
    public event Action? CloseRequested;

    public List<RunePrototype> Known = new();
    public readonly List<RunePrototype> Chain = new();
    public string? Preview;
    public RuneKind? Expected = RuneKind.Form;
    public bool SecondElement;
    public Func<float>? Strain;

    public Func<(float Left, float Fraction)?>? Cooldown;

    public Func<(float Left, float Fraction)?>? UltimateCooldown;

    public Func<List<(string Name, float Left, float Fraction)>>? ActiveCooldowns;

    private static readonly Color Ink = Color.FromHex("#b89cff");
    private static readonly Color CooldownColor = Color.FromHex("#7fc8ff");
    private static readonly Color Ember = Color.FromHex("#ffa24a");
    private static readonly Color Danger = Color.FromHex("#ff3b4a");
    private static readonly Color Night = Color.FromHex("#0b0614");
    private static readonly Color Good = Color.FromHex("#8dffb0");
    private static readonly Color Bad = Color.FromHex("#ff5a66");
    private static readonly Color FormColor = Color.FromHex("#ffd88a");
    private static readonly Color ElementColor = Color.FromHex("#9fdcff");
    private static readonly Color ModifierColor = Color.FromHex("#ff9ad8");

    private const string WhisperRune = "RuneModifierWhisper";
    private const string PowerRune = "RuneModifierPower";
    private const float FlashTime = 0.7f;
    private const float BurstTime = 0.7f;

    private readonly IClyde _clyde;
    private readonly List<Vector2> _stroke = new();
    private List<Vector2> _last = new();
    private Color _lastColor = Good;
    private float _lastFade;
    private Vector2 _burstAt;
    private Color _burstColor = Good;
    private float _burst;
    private float _castBurst;
    private bool _drawing;
    private float _time;
    private string? _status;
    private float _statusFade;
    private readonly Font _font;
    private readonly Font _small;
    private readonly Font _tiny;
    private readonly Font _label;
    private readonly Font _big;
    private readonly Font _title;
    private readonly List<Vector2> _scratch = new();
    private readonly List<Vector2> _quad = new(6);

    private const float LeftWidth = 290f;
    private const float RightWidth = 300f;
    private const float MiniRadius = 55f;
    private const int MaxCooldownRows = 4;
    private const float GuideCycle = 2.8f;
    private float PanelHeight => _compact ? 690f : 520f;
    private const float CircleRadius = 100f;
    private const float ButtonSpacing = 78f;
    private const float HeaderHeight = 34f;

    private static Vector2 _origin = new(14f, 56f);
    private static bool _compact;
    private static bool _showCircle = true;

    private readonly List<(RuneButton Button, int Slot)> _actions = new();
    private readonly List<(RuneButton Button, int Slot)> _toggles = new();
    private RuneButton? _collapse;
    private bool _dragging;
    private Vector2 _dragOffset;

    private bool CircleVisible => !_compact && _showCircle;
    private float PanelX => _origin.X;
    private float PanelY => _origin.Y;
    private float PanelWidth => CircleVisible ? LeftWidth + RightWidth : RightWidth;
    private float SheetX => CircleVisible ? PanelX + LeftWidth : PanelX;
    private float ButtonsTop => PanelY + PanelHeight - 70f;
    private Vector2 CircleCenter => new(PanelX + LeftWidth / 2f, PanelY + 44f + CircleRadius * 1.15f);
    private float ButtonsCenterX => CircleVisible ? CircleCenter.X : SheetX + RightWidth / 2f;

    private readonly record struct FormStyle(int Dashes, float Duty, float Thickness, float Spin);

    public RuneCircleControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        _clyde = IoCManager.Resolve<IClyde>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.NotoStack(variation: "Bold", size: 12);
        _small = cache.NotoStack(size: 9);
        _tiny = cache.NotoStack(size: 7);
        _label = cache.NotoStack(variation: "Bold", size: 9);
        _big = cache.NotoStack(variation: "Bold", size: 16);
        _title = cache.NotoStack(variation: "Bold", size: 14);

        AddButton(RuneButtonIcon.Scroll, "rune-circle-inscribe", -1, () => InscribeRequested?.Invoke());
        AddButton(RuneButtonIcon.Erase, "rune-circle-clear", 0, () => ClearRequested?.Invoke());
        AddButton(RuneButtonIcon.Close, "rune-circle-close", 1, () => CloseRequested?.Invoke());

        AddToggle(RuneButtonIcon.Swap, "rune-circle-toggle-mode", 0, () => _compact = !_compact);
        _collapse = AddToggle(RuneButtonIcon.Collapse, "rune-circle-toggle-circle", 1, () => _showCircle = !_showCircle);
        LayoutButtons();
    }

    private void AddButton(RuneButtonIcon icon, string loc, int slot, Action pressed)
    {
        var button = new RuneButton(icon, Loc.GetString(loc));
        button.Pressed += pressed;
        AddChild(button);
        _actions.Add((button, slot));
    }

    private RuneButton AddToggle(RuneButtonIcon icon, string loc, int slot, Action pressed)
    {
        var button = new RuneButton(icon, Loc.GetString(loc), true);
        button.Pressed += pressed;
        AddChild(button);
        _toggles.Add((button, slot));
        return button;
    }

    private void LayoutButtons()
    {
        var spacing = CircleVisible ? ButtonSpacing : 84f;
        var width = RuneButton.Diameter + 24f;
        foreach (var (button, slot) in _actions)
            SetPosition(button, new Vector2(ButtonsCenterX + slot * spacing - width / 2f, ButtonsTop));

        var size = RuneButton.SmallDiameter + 4f;
        foreach (var (button, slot) in _toggles)
            SetPosition(button, new Vector2(PanelX + PanelWidth - 14f - size - slot * (size + 4f), PanelY + (HeaderHeight - size) / 2f));

        if (_collapse != null)
            _collapse.Visible = !_compact;
    }

    public void Flash(bool success)
    {
        _lastColor = success ? Good : Bad;
        _lastFade = FlashTime;

        if (_last.Count == 0)
            return;

        var sum = Vector2.Zero;
        foreach (var point in _last)
            sum += point;

        _burstAt = sum / _last.Count;
        _burstColor = _lastColor;
        _burst = BurstTime;
    }

    public void Say(string text)
    {
        _status = text;
        _statusFade = 2.5f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
        _lastFade = MathF.Max(0f, _lastFade - args.DeltaSeconds);
        _statusFade = MathF.Max(0f, _statusFade - args.DeltaSeconds);
        _burst = MathF.Max(0f, _burst - args.DeltaSeconds);
        _castBurst = MathF.Max(0f, _castBurst - args.DeltaSeconds);
        LayoutButtons();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled)
            return;

        if (args.Function == EngineKeyFunctions.CloseModals ||
            args.Function == EngineKeyFunctions.WindowCloseAll ||
            args.Function == ContentKeyFunctions.EscapeContext)
        {
            CloseRequested?.Invoke();
            args.Handle();
            return;
        }

        if (args.Function == EngineKeyFunctions.UIClick && InHeader(args.RelativePosition))
        {
            _dragging = true;
            _dragOffset = args.RelativePosition - _origin;
            args.Handle();
            return;
        }

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _drawing = true;
            _stroke.Clear();
            _stroke.Add(Jitter(args.RelativePixelPosition));
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick || args.Function == EngineKeyFunctions.UseSecondary)
        {
            if (Chain.Count >= 2)
                _castBurst = BurstTime;

            CastRequested?.Invoke();
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick && _dragging)
        {
            _dragging = false;
            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick || !_drawing)
            return;

        _drawing = false;
        _last = new List<Vector2>(_stroke);
        _stroke.Clear();
        StrokeFinished?.Invoke(_last);
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging)
        {
            var limit = Size - new Vector2(PanelWidth, HeaderHeight);
            _origin = Vector2.Clamp(args.RelativePosition - _dragOffset, Vector2.Zero, Vector2.Max(limit, Vector2.Zero));
            return;
        }

        if (!_drawing)
            return;

        var point = Jitter(args.RelativePixelPosition);
        if (_stroke.Count > 0 && Vector2.DistanceSquared(_stroke[^1], point) < 9f)
            return;

        if (_stroke.Count < 600)
            _stroke.Add(point);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        if (!_drawing)
            return;

        _drawing = false;
        _stroke.Clear();
    }

    private Vector2 Jitter(Vector2 point)
    {
        var shake = Strain?.Invoke() ?? 0f;
        if (shake <= 0.5f)
            return point;

        var amount = (shake - 0.5f) * 14f * UIScale;
        return point + new Vector2(Random.Shared.NextSingle() - 0.5f, Random.Shared.NextSingle() - 0.5f) * amount;
    }

    private RunePrototype? Form => Chain.Count > 0 ? Chain[0] : null;

    private List<RunePrototype> Elements => Chain.FindAll(r => r.Kind == RuneKind.Element);

    private List<RunePrototype> Modifiers => Chain.FindAll(r => r.Kind == RuneKind.Modifier);

    private Color Tint(float strain)
    {
        var calm = Elements.Count > 0 ? Color.InterpolateBetween(Ink, Elements[0].Color, 0.5f) : Ink;
        if (strain < 0.5f)
            return calm;

        return strain < 0.8f
            ? Color.InterpolateBetween(calm, Ember, (strain - 0.5f) / 0.3f)
            : Color.InterpolateBetween(Ember, Danger, MathF.Min(1f, (strain - 0.8f) / 0.2f));
    }

    private Color KindColor(RuneKind? kind)
    {
        return kind switch
        {
            RuneKind.Form => FormColor,
            RuneKind.Element => Elements.Count > 0 ? Elements[0].Color : ElementColor,
            RuneKind.Modifier => ModifierColor,
            _ => Color.White,
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = UIScale;
        var strain = Math.Clamp(Strain?.Invoke() ?? 0f, 0f, 1f);
        var tint = Tint(strain);
        var radius = CircleRadius * scale;
        var tremble = strain > 0.75f ? new Vector2(MathF.Sin(_time * 41f), MathF.Cos(_time * 37f)) * (strain - 0.75f) * 4f * scale : Vector2.Zero;
        var c = CircleCenter * scale + tremble;
        var dim = Chain.Exists(r => r.ID == WhisperRune) ? 0.5f : 1f;

        DrawPanel(handle, tint, scale);
        var cooldowns = ActiveCooldowns?.Invoke() ?? new List<(string Name, float Left, float Fraction)>();
        if (cooldowns.Count > MaxCooldownRows)
            cooldowns.RemoveRange(MaxCooldownRows, cooldowns.Count - MaxCooldownRows);
        var listHeight = cooldowns.Count == 0 ? 0f : 22f + cooldowns.Count * 18f;

        var listBottom = CircleVisible ? PanelY + PanelHeight - 12f : ButtonsTop - 6f;
        DrawCooldownList(handle, cooldowns, SheetX + 16f, RightWidth - 32f, listBottom - listHeight, scale);

        var sheetBottom = listBottom - 2f - listHeight;
        if (CircleVisible)
        {
            Texture(handle, RuneCircleArt.Glow(_clyde), c, radius * 3f, 0f, tint.WithAlpha(0.2f * dim));
            handle.DrawCircle(c, radius * 1.02f, Night.WithAlpha(0.6f));
            DrawFormRing(handle, c, radius, scale, dim);
            DrawElementBand(handle, c, radius, scale, dim);
            DrawCore(handle, c, radius, tint, scale, dim);
            DrawStrain(handle, c, radius * 1.13f, strain, scale);
            DrawCaption(handle, c, radius, tint, scale);
            DrawLabels(handle, scale);
            DrawGuide(handle, c, radius, scale);
            DrawSheet(handle, scale, PanelY + 12f, sheetBottom);
        }
        else if (_compact)
        {
            DrawMiniCircle(handle, tint, strain, scale, dim);
            DrawSheet(handle, scale, PanelY + 216f, sheetBottom);
        }
        else
        {
            DrawSheet(handle, scale, PanelY + 12f, sheetBottom);
        }

        DrawTrail(handle, _stroke, KindColor(Expected), 1f, scale);
        if (_lastFade > 0f)
            DrawTrail(handle, _last, _lastColor, _lastFade / FlashTime, scale);

        if (_burst > 0f)
        {
            var t = 1f - _burst / BurstTime;
            var ease = 1f - (1f - t) * (1f - t);
            Texture(handle, RuneCircleArt.Ring(_clyde, 3f), _burstAt, RuneCircleArt.RingDiameter((14f + 60f * ease) * scale), 0f, _burstColor.WithAlpha(1f - t));
            Texture(handle, RuneCircleArt.Glow(_clyde), _burstAt, (40f + 40f * ease) * scale, 0f, _burstColor.WithAlpha(0.5f * (1f - t)));
        }

        if (_castBurst > 0f && CircleVisible)
        {
            var t = 1f - _castBurst / BurstTime;
            var ease = 1f - (1f - t) * (1f - t);
            Texture(handle, RuneCircleArt.Ring(_clyde, 4f), c, RuneCircleArt.RingDiameter(radius * (1f + 0.7f * ease)), 0f, tint.WithAlpha(1f - t));
            Texture(handle, RuneCircleArt.Glow(_clyde), c, radius * (1.2f + 2f * ease), 0f, Color.White.WithAlpha(0.35f * (1f - t)));
        }

        if (_status != null && _statusFade > 0f)
            Centered(handle, _font, _status, new Vector2(ButtonsCenterX * scale, (PanelY + PanelHeight + 8f) * scale), Bad.WithAlpha(MathF.Min(1f, _statusFade)));
    }

    private static FormStyle StyleFor(string? form)
    {
        return form switch
        {
            "RuneFormProjectile" => new FormStyle(12, 0.55f, 4f, 0.5f),
            "RuneFormWall" => new FormStyle(8, 0.86f, 8f, 0.06f),
            "RuneFormWave" => new FormStyle(36, 0.5f, 3f, 0.35f),
            "RuneFormTouch" => new FormStyle(6, 0.32f, 6f, 0.15f),
            "RuneFormSelf" => new FormStyle(0, 1f, 4f, 0f),
            "RuneFormAnimate" => new FormStyle(3, 0.72f, 4f, 0.8f),
            "RuneFormSummon" => new FormStyle(5, 0.6f, 5f, -0.3f),
            "RuneFormPolymorph" => new FormStyle(4, 0.5f, 5f, 0.45f),
            "RuneFormUltimate" => new FormStyle(7, 0.82f, 6f, 0.2f),
            _ => new FormStyle(60, 0.35f, 2f, 0.12f),
        };
    }

    private void DrawFormRing(DrawingHandleScreen handle, Vector2 c, float big, float scale, float dim)
    {
        var r = big * 0.95f;
        var form = Form;
        var diameter = RuneCircleArt.RingDiameter(r);
        var texel = RuneCircleArt.RingSize / diameter;
        var pending = form == null;
        var pulse = Expected == RuneKind.Form ? 0.5f + 0.5f * MathF.Sin(_time * 4f) : 0f;
        var color = pending ? FormColor.WithAlpha(0.18f + 0.35f * pulse) : form!.Color.WithAlpha(0.95f * dim);
        var style = StyleFor(form?.ID);

        Texture(handle, RuneCircleArt.Ring(_clyde, 1.2f * scale * texel), c, RuneCircleArt.RingDiameter(r + 9f * scale), 0f, color.WithAlpha(color.A * 0.45f));
        Texture(handle, RuneCircleArt.Ring(_clyde, 1.2f * scale * texel), c, RuneCircleArt.RingDiameter(r - 9f * scale), 0f, color.WithAlpha(color.A * 0.3f));

        var spin = style.Spin;
        if (form?.ID == "RuneFormPolymorph")
            spin *= MathF.Sin(_time * 0.8f) * 2f;

        Texture(handle, RuneCircleArt.Ring(_clyde, style.Thickness * scale * texel, style.Dashes, style.Duty), c, diameter, _time * spin, color);
        if (!pending)
            Texture(handle, RuneCircleArt.Band(_clyde, style.Thickness * 4f * scale * texel), c, diameter, 0f, color.WithAlpha(0.18f * dim));

        if (form?.ID == "RuneFormUltimate")
            Texture(handle, RuneCircleArt.Ring(_clyde, 2f * scale * texel, 14, 0.4f), c, RuneCircleArt.RingDiameter(r + 16f * scale), -_time * 0.35f, color.WithAlpha(0.7f * dim));

        if (form == null)
            return;

        var top = c + new Vector2(0f, -r);
        var medal = 17f * scale;
        handle.DrawCircle(top, medal, Night);
        Texture(handle, RuneCircleArt.Glow(_clyde), top, medal * 4f, 0f, form.Color.WithAlpha(0.35f * dim));
        Texture(handle, RuneCircleArt.Ring(_clyde, 2f * scale * RuneCircleArt.RingSize / RuneCircleArt.RingDiameter(medal)), top, RuneCircleArt.RingDiameter(medal), 0f, form.Color.WithAlpha(dim));
        DrawGlyph(handle, form.Stroke, UIBox2.FromDimensions(top - new Vector2(medal * 0.8f), new Vector2(medal * 1.6f)), form.Color.WithAlpha(dim), 1.8f * scale);
    }

    private void DrawElementBand(DrawingHandleScreen handle, Vector2 c, float big, float scale, float dim)
    {
        var r = big * 0.74f;
        var width = big * 0.13f;
        var diameter = RuneCircleArt.RingDiameter(r);
        var texel = RuneCircleArt.RingSize / diameter;
        var elements = Elements;

        Texture(handle, RuneCircleArt.Ring(_clyde, 1f * scale * texel), c, RuneCircleArt.RingDiameter(r + width / 2f), 0f, Color.White.WithAlpha(0.12f * dim));
        Texture(handle, RuneCircleArt.Ring(_clyde, 1f * scale * texel), c, RuneCircleArt.RingDiameter(r - width / 2f), 0f, Color.White.WithAlpha(0.12f * dim));

        if (elements.Count == 0)
        {
            var pulse = Expected == RuneKind.Element ? 0.5f + 0.5f * MathF.Sin(_time * 4f) : 0f;
            Texture(handle, RuneCircleArt.Band(_clyde, width * texel), c, diameter, 0f, ElementColor.WithAlpha(0.05f + 0.12f * pulse));
            Texture(handle, RuneCircleArt.Ring(_clyde, 1.5f * scale * texel, 48, 0.3f), c, diameter, _time * 0.2f, ElementColor.WithAlpha(0.15f + 0.35f * pulse));
            return;
        }

        var first = elements[0];
        var second = elements.Count > 1 ? elements[1] : null;
        var main = ElementTint(first, 0f);
        Texture(handle, RuneCircleArt.Band(_clyde, width * texel), c, diameter, 0f, main.WithAlpha(0.55f * dim));

        var sheen = SecondElement
            ? Color.White.WithAlpha(0.3f + 0.4f * (0.5f + 0.5f * MathF.Sin(_time * 4f)))
            : (second != null ? ElementTint(second, 0.3f) : Color.InterpolateBetween(main, Color.White, 0.6f)).WithAlpha(0.85f * dim);
        var speed = ElementSpeed(first.ID);
        Texture(handle, RuneCircleArt.Comet(_clyde, width * 0.9f * texel), c, diameter, _time * speed, sheen);
        Texture(handle, RuneCircleArt.Comet(_clyde, width * 0.9f * texel), c, diameter, _time * speed + MathF.PI, sheen.WithAlpha(sheen.A * 0.6f));

        DrawMotes(handle, c, r, width, first, second, scale, dim);
    }

    private Color ElementTint(RunePrototype element, float offset)
    {
        if (element.ID != "RuneElementChaos")
            return element.Color;

        return Color.FromHsv(new Vector4((_time * 0.15f + offset) % 1f, 0.75f, 1f, 1f));
    }

    private static float ElementSpeed(string element)
    {
        return element switch
        {
            "RuneElementFire" => 1.3f,
            "RuneElementIce" => 0.25f,
            "RuneElementEarth" => 0.12f,
            "RuneElementLightning" => 3f,
            "RuneElementLife" => 0.4f,
            "RuneElementVoid" => -0.7f,
            "RuneElementChaos" => 1f,
            "RuneElementWater" => 0.8f,
            "RuneElementAir" => 2f,
            "RuneElementPoison" => 0.35f,
            "RuneElementBlood" => 0.3f,
            "RuneElementDeath" => -0.2f,
            _ => 0.5f,
        };
    }

    private void DrawMotes(DrawingHandleScreen handle, Vector2 c, float r, float width, RunePrototype first, RunePrototype? second, float scale, float dim)
    {
        const int count = 22;
        var glow = RuneCircleArt.Glow(_clyde);
        for (var i = 0; i < count; i++)
        {
            var element = second != null && i % 2 == 1 ? second : first;
            var seed = i * 7 % count / (float) count;
            var baseAngle = i * MathF.Tau / count;
            var angle = baseAngle;
            var offset = 0f;
            var alpha = 0.8f;
            var mote = 7f;

            switch (element.ID)
            {
                case "RuneElementFire":
                {
                    var life = (_time * 0.7f + seed) % 1f;
                    angle += _time * 0.15f;
                    offset = -0.4f + life * 1.3f;
                    alpha = MathF.Sin(life * MathF.PI);
                    mote = 9f - life * 4f;
                    break;
                }
                case "RuneElementIce":
                    offset = (seed - 0.5f) * 0.7f;
                    alpha = MathF.Max(0f, MathF.Sin(_time * 2f + seed * 20f));
                    mote = 6f;
                    break;
                case "RuneElementEarth":
                    angle += _time * 0.05f;
                    offset = (seed - 0.5f) * 0.4f;
                    alpha = 0.55f;
                    mote = 11f;
                    break;
                case "RuneElementLightning":
                {
                    var step = MathF.Floor(_time * 9f + seed * 7f);
                    var random = new Random((int) step * 31 + i);
                    angle += random.NextSingle() * 0.4f;
                    offset = (random.NextSingle() - 0.5f) * 0.9f;
                    alpha = random.NextSingle() > 0.4f ? 1f : 0.15f;
                    mote = 6f;
                    break;
                }
                case "RuneElementLife":
                    angle += _time * 0.3f;
                    offset = MathF.Sin(_time * 1.5f + seed * 9f) * 0.35f;
                    alpha = 0.75f;
                    break;
                case "RuneElementVoid":
                {
                    var fall = (_time * 0.4f + seed) % 1f;
                    angle -= _time * 0.5f;
                    offset = 0.5f - fall;
                    alpha = MathF.Sin(fall * MathF.PI);
                    break;
                }
                case "RuneElementChaos":
                    angle += _time * (0.4f + seed);
                    offset = MathF.Sin(_time * 3f + seed * 13f) * 0.45f;
                    break;
                case "RuneElementWater":
                    angle += _time * 0.6f;
                    offset = MathF.Sin(angle * 4f + _time * 2f) * 0.3f;
                    alpha = 0.7f;
                    break;
                case "RuneElementAir":
                    angle += _time * (1.4f + seed);
                    offset = (seed - 0.5f) * 0.8f;
                    alpha = 0.5f;
                    mote = 5f;
                    break;
                case "RuneElementBlood":
                {
                    var drip = (_time * 0.35f + seed) % 1f;
                    offset = 0.3f - drip * 1.4f;
                    alpha = 1f - drip;
                    mote = 8f - drip * 3f;
                    break;
                }
                case "RuneElementDeath":
                {
                    var drift = (_time * 0.18f + seed) % 1f;
                    angle -= _time * 0.1f;
                    offset = -0.3f + drift * 1.1f;
                    alpha = MathF.Sin(drift * MathF.PI) * 0.6f;
                    mote = 10f;
                    break;
                }
                case "RuneElementPoison":
                {
                    var bubble = (_time * 0.5f + seed) % 1f;
                    angle += MathF.Sin(_time * 0.7f + seed * 11f) * 0.05f;
                    offset = -0.5f + bubble * 1.2f;
                    alpha = bubble < 0.85f ? 0.8f : (1f - bubble) * 5f;
                    mote = 5f + bubble * 6f;
                    break;
                }
            }

            var color = ElementTint(element, seed);
            var at = c + Dir(angle) * (r + offset * width);
            Texture(handle, glow, at, mote * 2.2f * scale, 0f, color.WithAlpha(alpha * 0.6f * dim));
            Texture(handle, glow, at, mote * 0.9f * scale, 0f, Color.InterpolateBetween(color, Color.White, 0.6f).WithAlpha(alpha * dim));
        }
    }

    private void DrawCore(DrawingHandleScreen handle, Vector2 c, float big, Color tint, float scale, float dim)
    {
        var elements = Elements;
        var modifiers = Modifiers;
        var color = elements.Count > 0 ? ElementTint(elements[0], 0f) : tint;
        var power = modifiers.FindAll(m => m.ID == PowerRune).Count;
        var breath = 1f + 0.06f * MathF.Sin(_time * 2.4f);
        var core = big * (0.13f + 0.06f * power) * breath;
        var glow = RuneCircleArt.Glow(_clyde);
        var orbit = big * 0.44f;
        var diameter = RuneCircleArt.RingDiameter(orbit);
        var texel = RuneCircleArt.RingSize / diameter;

        var pulse = Expected == RuneKind.Modifier && modifiers.Count == 0 ? 0.5f + 0.5f * MathF.Sin(_time * 4f) : 0f;
        Texture(handle, RuneCircleArt.Ring(_clyde, 1.2f * scale * texel, 72, 0.5f), c, diameter, -_time * 0.1f, ModifierColor.WithAlpha((0.18f + 0.4f * pulse) * dim));

        Texture(handle, glow, c, core * 6f, 0f, color.WithAlpha(0.45f * dim));
        Texture(handle, glow, c, core * 2.6f, 0f, color.WithAlpha(0.9f * dim));
        Texture(handle, glow, c, core * 1.1f, 0f, Color.White.WithAlpha(0.9f * dim));

        if (modifiers.Count == 0)
            return;

        var medal = 15f * scale;
        var medalDiameter = RuneCircleArt.RingDiameter(medal);
        var medalRing = RuneCircleArt.Ring(_clyde, 1.8f * scale * RuneCircleArt.RingSize / medalDiameter);
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            var at = c + Dir(_time * 0.35f + i * MathF.Tau / modifiers.Count - MathF.PI / 2f) * orbit;
            Texture(handle, glow, at, medal * 4f, 0f, modifier.Color.WithAlpha(0.35f * dim));
            handle.DrawCircle(at, medal, Night);
            Texture(handle, medalRing, at, medalDiameter, 0f, modifier.Color.WithAlpha(dim));
            DrawGlyph(handle, modifier.Stroke, UIBox2.FromDimensions(at - new Vector2(medal * 0.8f), new Vector2(medal * 1.6f)), modifier.Color.WithAlpha(dim), 1.6f * scale);
        }
    }

    private void DrawPanel(DrawingHandleScreen handle, Color tint, float scale)
    {
        var panel = UIBox2.FromDimensions(new Vector2(PanelX, PanelY) * scale, new Vector2(PanelWidth, PanelHeight) * scale);
        var glass = Color.FromHex("#0d0818");

        handle.DrawRect(new UIBox2(panel.TopLeft + new Vector2(4f, 6f) * scale, panel.BottomRight + new Vector2(4f, 6f) * scale), Color.Black.WithAlpha(0.35f));
        handle.DrawRect(panel, glass.WithAlpha(0.9f));
        for (var i = 0; i < 6; i++)
        {
            var band = UIBox2.FromDimensions(panel.TopLeft + new Vector2(0f, i * 8f * scale), new Vector2(panel.Width, 8f * scale));
            handle.DrawRect(band, tint.WithAlpha(0.03f * (6 - i) / 6f));
        }

        var gold = Color.FromHex("#e8c77a");
        Frame(handle, panel, gold.WithAlpha(0.55f), 1.2f * scale);
        Frame(handle, new UIBox2(panel.TopLeft + new Vector2(5f) * scale, panel.BottomRight - new Vector2(5f) * scale), gold.WithAlpha(0.18f), 1f * scale);

        foreach (var corner in new[] { panel.TopLeft, panel.TopRight, panel.BottomLeft, panel.BottomRight })
            Diamond(handle, corner, 6f * scale, gold);

        var grip = new Vector2(PanelX + 16f, PanelY + HeaderHeight / 2f) * scale;
        for (var i = 0; i < 3; i++)
        {
            Diamond(handle, grip + new Vector2(0f, (i - 1) * 6f * scale), 1.8f * scale, gold.WithAlpha(_dragging ? 0.9f : 0.4f));
            Diamond(handle, grip + new Vector2(5f * scale, (i - 1) * 6f * scale), 1.8f * scale, gold.WithAlpha(_dragging ? 0.9f : 0.4f));
        }

        if (_compact)
            Header(handle, new Vector2((SheetX + RightWidth / 2f) * scale, (PanelY + 12f) * scale), Loc.GetString("rune-circle-title"), gold, scale);

        if (!CircleVisible)
            return;

        var divider = (PanelX + LeftWidth) * scale;
        Line(handle, new Vector2(divider, panel.Top + 18f * scale), new Vector2(divider, panel.Bottom - 18f * scale), 1f * scale, gold.WithAlpha(0.22f));
        Diamond(handle, new Vector2(divider, panel.Top + 18f * scale), 3.5f * scale, gold.WithAlpha(0.6f));
        Diamond(handle, new Vector2(divider, panel.Bottom - 18f * scale), 3.5f * scale, gold.WithAlpha(0.6f));

        Header(handle, new Vector2((PanelX + LeftWidth / 2f) * scale, (PanelY + 12f) * scale), Loc.GetString("rune-circle-title"), gold, scale);
    }

    private bool InHeader(Vector2 point)
    {
        return point.X >= PanelX && point.X <= PanelX + PanelWidth && point.Y >= PanelY && point.Y <= PanelY + HeaderHeight;
    }

    private void DrawMiniCircle(DrawingHandleScreen handle, Color tint, float strain, float scale, float dim)
    {
        var detail = scale * MiniRadius / CircleRadius;
        var radius = MiniRadius * scale;
        var c = new Vector2(SheetX + RightWidth / 2f, PanelY + 44f + MiniRadius * 1.15f) * scale;

        Texture(handle, RuneCircleArt.Glow(_clyde), c, radius * 3f, 0f, tint.WithAlpha(0.2f * dim));
        handle.DrawCircle(c, radius * 1.02f, Night.WithAlpha(0.6f));
        DrawFormRing(handle, c, radius, detail, dim);
        DrawElementBand(handle, c, radius, detail, dim);
        DrawCore(handle, c, radius, tint, detail, dim);
        DrawStrain(handle, c, radius * 1.13f, strain, detail);
        DrawGuide(handle, c, radius, detail);

        var top = c.Y + radius * 1.22f;
        var left = (SheetX + 16f) * scale;
        var right = (SheetX + RightWidth - 16f) * scale;
        var caption = Preview ?? Expected switch
        {
            RuneKind.Form => Loc.GetString("rune-circle-hint-form"),
            RuneKind.Element when SecondElement => Loc.GetString("rune-circle-hint-second"),
            RuneKind.Element => Loc.GetString("rune-circle-hint-element"),
            RuneKind.Modifier => Loc.GetString("rune-circle-hint-modifier"),
            _ => Loc.GetString("rune-circle-hint-full"),
        };
        var captionColor = Preview != null ? Color.InterpolateBetween(Color.White, tint, 0.3f) : Color.White.WithAlpha(0.5f);
        Centered(handle, Preview != null ? _font : _small, caption, new Vector2((left + right) / 2f, top), captionColor);
        if (Preview != null && Cooldown?.Invoke() is { } cooldown)
            DrawCooldownBar(handle, new Vector2((left + right) / 2f, top + 20f * scale), right - left - 40f * scale, cooldown.Left, cooldown.Fraction, scale);
    }

    private void DrawCooldownBar(DrawingHandleScreen handle, Vector2 top, float width, float left, float fraction, float scale)
    {
        var text = Loc.GetString("rune-circle-cooldown", ("time", FormatSeconds(left)));
        var textWidth = TextWidth(_small, text);
        var gap = 8f * scale;
        var barWidth = MathF.Max(20f * scale, width - textWidth - gap);
        var start = top.X - (barWidth + gap + textWidth) / 2f;
        var mid = top.Y + 7f * scale;

        Quad(handle, new Vector2(start, mid), new Vector2(start + barWidth, mid), 4f * scale, Night.WithAlpha(0.9f));
        Quad(handle, new Vector2(start, mid), new Vector2(start + barWidth * fraction, mid), 3f * scale, CooldownColor);
        Texture(handle, RuneCircleArt.Glow(_clyde), new Vector2(start + barWidth * fraction, mid), 12f * scale, 0f, CooldownColor);
        handle.DrawString(_small, new Vector2(start + barWidth + gap, top.Y), text, CooldownColor);
    }

    private void DrawCooldownDial(DrawingHandleScreen handle, Vector2 center, float radius, float left, float fraction, float scale)
    {
        Arc(handle, center, radius / 2f, radius, -MathF.PI / 2f, MathF.Tau * fraction, Night.WithAlpha(0.78f));
        Arc(handle, center, radius + 1.5f * scale, 2f * scale, -MathF.PI / 2f, MathF.Tau * fraction, CooldownColor);
        Centered(handle, _small, FormatSeconds(left), center - new Vector2(0f, 7f * scale), Color.White);
    }

    private void DrawGuide(DrawingHandleScreen handle, Vector2 c, float big, float scale)
    {
        if (Expected is not { } kind || _stroke.Count > 0)
            return;

        var fusionAllowed = Chain.Count == 2;
        var candidates = Known.FindAll(r => r.Kind == kind && (fusionAllowed || !r.Fusion));
        if (candidates.Count == 0)
            return;

        var cycle = _time / GuideCycle;
        var rune = candidates[(int) cycle % candidates.Count];
        var progress = Math.Clamp((cycle - MathF.Floor(cycle)) / 0.7f, 0f, 1f);
        var size = big * 0.52f;
        var rect = UIBox2.FromDimensions(c - new Vector2(size / 2f), new Vector2(size));
        var inner = new UIBox2(rect.TopLeft + rect.Size * 0.15f, rect.BottomRight - rect.Size * 0.15f);
        Vector2 Map(Vector2 p) => inner.TopLeft + new Vector2(p.X * inner.Width, p.Y * inner.Height);

        handle.DrawCircle(c, big * 0.32f, Night.WithAlpha(0.6f));
        DrawGlyph(handle, rune.Stroke, rect, rune.Color.WithAlpha(0.18f), 2f * scale);

        var total = 0f;
        for (var i = 1; i < rune.Stroke.Count; i++)
            total += (Map(rune.Stroke[i]) - Map(rune.Stroke[i - 1])).Length();

        var remaining = total * progress;
        var pen = Map(rune.Stroke[0]);
        for (var i = 1; i < rune.Stroke.Count && remaining > 0f; i++)
        {
            var from = Map(rune.Stroke[i - 1]);
            var to = Map(rune.Stroke[i]);
            var length = (to - from).Length();
            pen = length <= remaining ? to : from + (to - from) * (remaining / length);
            Line(handle, from, pen, 2.4f * scale, rune.Color.WithAlpha(0.9f));
            remaining -= length;
        }

        var glow = RuneCircleArt.Glow(_clyde);
        Texture(handle, glow, Map(rune.Stroke[0]), 14f * scale, 0f, Color.White.WithAlpha(0.5f));
        if (progress < 1f)
            Texture(handle, glow, pen, 22f * scale, 0f, Color.InterpolateBetween(rune.Color, Color.White, 0.5f));

        Centered(handle, _small, Loc.GetString("rune-circle-guide", ("rune", Loc.GetString(rune.Name))), new Vector2(c.X, rect.Bottom - 2f * scale), rune.Color.WithAlpha(0.85f));
    }

    private void DrawCooldownList(DrawingHandleScreen handle, List<(string Name, float Left, float Fraction)> cooldowns, float x, float width, float top, float scale)
    {
        if (cooldowns.Count == 0)
            return;

        handle.DrawString(_label, new Vector2(x, top) * scale, Loc.GetString("rune-circle-cooldowns").ToUpperInvariant(), CooldownColor.WithAlpha(0.7f));
        Line(handle, new Vector2(x, top + 15f) * scale, new Vector2(x + width, top + 15f) * scale, 1f * scale, CooldownColor.WithAlpha(0.2f));

        var y = top + 22f;
        foreach (var (name, left, fraction) in cooldowns)
        {
            var time = FormatSeconds(left);
            var timeWidth = TextWidth(_small, time);
            var barStart = (x + width * 0.55f) * scale;
            var barEnd = (x + width) * scale - timeWidth - 6f * scale;
            var mid = (y + 7f) * scale;

            handle.DrawString(_small, new Vector2(x, y) * scale, Fit(_small, name, width * 0.55f * scale - 6f * scale), Color.White.WithAlpha(0.8f));
            Quad(handle, new Vector2(barStart, mid), new Vector2(barEnd, mid), 4f * scale, Night.WithAlpha(0.9f));
            Quad(handle, new Vector2(barStart, mid), new Vector2(barStart + (barEnd - barStart) * fraction, mid), 3f * scale, CooldownColor);
            handle.DrawString(_small, new Vector2((x + width) * scale - timeWidth, y * scale), time, CooldownColor);
            y += 18f;
        }
    }

    private static string Fit(Font font, string text, float width)
    {
        if (TextWidth(font, text) <= width)
            return text;

        while (text.Length > 1 && TextWidth(font, text + "…") > width)
            text = text[..^1];

        return text + "…";
    }

    private static string FormatSeconds(float seconds)
    {
        var total = (int) MathF.Ceiling(seconds);
        return total >= 60 ? $"{total / 60}:{total % 60:00}" : Loc.GetString("rune-circle-seconds", ("seconds", total));
    }

    private void Header(DrawingHandleScreen handle, Vector2 at, string text, Color gold, float scale)
    {
        var width = TextWidth(_title, text);
        Centered(handle, _title, text, at, gold);
        var y = at.Y + 9f * scale;
        Line(handle, new Vector2(at.X - width / 2f - 46f * scale, y), new Vector2(at.X - width / 2f - 10f * scale, y), 1f * scale, gold.WithAlpha(0.5f));
        Line(handle, new Vector2(at.X + width / 2f + 10f * scale, y), new Vector2(at.X + width / 2f + 46f * scale, y), 1f * scale, gold.WithAlpha(0.5f));
        Diamond(handle, new Vector2(at.X - width / 2f - 50f * scale, y), 2.5f * scale, gold);
        Diamond(handle, new Vector2(at.X + width / 2f + 50f * scale, y), 2.5f * scale, gold);
    }

    private void Frame(DrawingHandleScreen handle, UIBox2 box, Color color, float width)
    {
        Quad(handle, box.TopLeft, box.TopRight, width, color);
        Quad(handle, box.BottomLeft, box.BottomRight, width, color);
        Quad(handle, box.TopLeft, box.BottomLeft, width, color);
        Quad(handle, box.TopRight, box.BottomRight, width, color);
    }

    private void Diamond(DrawingHandleScreen handle, Vector2 at, float size, Color color)
    {
        _quad.Clear();
        _quad.Add(at + new Vector2(0f, -size));
        _quad.Add(at + new Vector2(size, 0f));
        _quad.Add(at + new Vector2(0f, size));
        _quad.Add(at + new Vector2(0f, size));
        _quad.Add(at + new Vector2(-size, 0f));
        _quad.Add(at + new Vector2(0f, -size));
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _quad, color);
    }

    private void DrawLabels(DrawingHandleScreen handle, float scale)
    {
        var form = Form;
        var elements = Elements;
        var modifiers = Modifiers;
        var x = (PanelX + 26f) * scale;
        var y = (PanelY + 352f) * scale;
        var row = 28f * scale;

        Label(handle, new Vector2(x, y), Loc.GetString("rune-circle-part-form"),
            form != null ? Loc.GetString(form.Name) : null, form?.Color ?? FormColor, Expected == RuneKind.Form, scale);

        var elementValue = elements.Count switch
        {
            0 => null,
            1 when SecondElement => Loc.GetString(elements[0].Name) + " + ?",
            1 => Loc.GetString(elements[0].Name),
            _ => Loc.GetString(elements[0].Name) + " + " + Loc.GetString(elements[1].Name),
        };
        Label(handle, new Vector2(x, y + row), Loc.GetString(SecondElement ? "rune-circle-part-second" : "rune-circle-part-element"),
            elementValue, elements.Count > 0 ? elements[0].Color : ElementColor, Expected == RuneKind.Element, scale);

        string? modifierValue = null;
        foreach (var modifier in modifiers)
            modifierValue = modifierValue == null ? Loc.GetString(modifier.Name) : modifierValue + ", " + Loc.GetString(modifier.Name);

        Label(handle, new Vector2(x, y + row * 2f), Loc.GetString("rune-circle-part-modifier"),
            modifierValue, ModifierColor, Expected == RuneKind.Modifier, scale);
    }

    private void Label(DrawingHandleScreen handle, Vector2 at, string title, string? value, Color color, bool active, float scale)
    {
        var pulse = active ? 0.5f + 0.5f * MathF.Sin(_time * 4f) : 0f;
        var titleColor = value != null ? color.WithAlpha(0.8f) : active ? color.WithAlpha(0.5f + 0.5f * pulse) : Color.White.WithAlpha(0.22f);
        Diamond(handle, at + new Vector2(-10f * scale, 6f * scale), (active ? 3.5f : 2.5f) * scale, titleColor);
        handle.DrawString(_label, at, title, titleColor);

        if (value != null)
            handle.DrawString(_font, new Vector2(at.X + 112f * scale, at.Y - 2f * scale), value, Color.InterpolateBetween(color, Color.White, 0.45f));
        Line(handle, new Vector2(at.X, at.Y + 17f * scale), new Vector2((PanelX + LeftWidth - 22f) * scale, at.Y + 17f * scale), 1f * scale, Color.White.WithAlpha(0.06f));
    }

    private void DrawStrain(DrawingHandleScreen handle, Vector2 c, float radius, float strain, float scale)
    {
        const float sweep = MathF.PI * 0.8f;
        var start = -MathF.PI / 2f - sweep / 2f;
        Arc(handle, c, radius, 3f * scale, start, sweep, Color.White.WithAlpha(0.08f));
        for (var i = 0; i <= 4; i++)
            Diamond(handle, c + Dir(start + sweep * i / 4f) * radius, 2f * scale, Color.White.WithAlpha(0.25f));

        if (strain <= 0f)
            return;

        var color = strain < 0.5f ? Good : strain < 0.75f ? Color.FromHex("#ffd94a") : Danger;
        var flicker = strain > 0.75f ? 0.65f + 0.35f * MathF.Sin(_time * 12f) : 1f;
        Arc(handle, c, radius, 8f * scale, start, sweep * strain, color.WithAlpha(0.12f * flicker));
        Arc(handle, c, radius, 3f * scale, start, sweep * strain, color.WithAlpha(0.95f * flicker));
        Texture(handle, RuneCircleArt.Glow(_clyde), c + Dir(start + sweep * strain) * radius, 18f * scale, 0f, color.WithAlpha(0.9f * flicker));
    }

    private void DrawCaption(DrawingHandleScreen handle, Vector2 c, float big, Color tint, float scale)
    {
        var y = c.Y + big * 1.16f;
        var strain = Math.Clamp(Strain?.Invoke() ?? 0f, 0f, 1f);
        if (Preview != null)
        {
            var glow = Elements.Count > 0 ? Elements[0].Color : tint;
            Texture(handle, RuneCircleArt.Glow(_clyde), new Vector2(c.X, y + 11f * scale), 200f * scale, 0f, glow.WithAlpha(0.14f));
            Centered(handle, _big, Preview, new Vector2(c.X + 1f * scale, y + 1f * scale), Night);
            Centered(handle, _big, Preview, new Vector2(c.X, y), Color.InterpolateBetween(Color.White, glow, 0.3f));
            if (Cooldown?.Invoke() is { } cooldown)
                DrawCooldownBar(handle, new Vector2(c.X, y + 30f * scale), 150f * scale, cooldown.Left, cooldown.Fraction, scale);
            return;
        }

        var hint = Expected switch
        {
            RuneKind.Form => Loc.GetString("rune-circle-hint-form"),
            RuneKind.Element when SecondElement => Loc.GetString("rune-circle-hint-second"),
            RuneKind.Element => Loc.GetString("rune-circle-hint-element"),
            RuneKind.Modifier => Loc.GetString("rune-circle-hint-modifier"),
            _ => Loc.GetString("rune-circle-hint-full"),
        };
        Centered(handle, _font, hint, new Vector2(c.X, y + 4f * scale), Color.White.WithAlpha(0.55f));
        Centered(handle, _small, Loc.GetString("rune-circle-strain", ("percent", (int) (strain * 100f))), new Vector2(c.X, y + 22f * scale), Color.White.WithAlpha(0.35f));
    }

    private void DrawSheet(DrawingHandleScreen handle, float scale, float sheetTop, float sheetBottom)
    {
        const int columns = 4;
        var box = 28f * scale;
        var cell = 68f * scale;
        var row = box + 20f * scale;
        var x = (SheetX + 16f) * scale;
        var bottom = sheetBottom * scale;
        var y = sheetTop * scale;
        var gold = Color.FromHex("#e8c77a");

        Header(handle, new Vector2((SheetX + RightWidth / 2f) * scale, y), Loc.GetString("rune-circle-sheet-title"), gold.WithAlpha(0.85f), scale * 0.75f);
        y += 32f * scale;

        foreach (var kind in new[] { RuneKind.Form, RuneKind.Element, RuneKind.Modifier })
        {
            var runes = Known.FindAll(r => r.Kind == kind);
            if (runes.Count == 0)
                continue;

            var active = Expected == kind;
            var color = KindHeader(kind);
            var pulse = active ? 0.5f + 0.5f * MathF.Sin(_time * 4f) : 0f;
            handle.DrawString(_label, new Vector2(x, y), Loc.GetString($"rune-kind-{kind.ToString().ToLowerInvariant()}").ToUpperInvariant(), active ? color.WithAlpha(0.7f + 0.3f * pulse) : color.WithAlpha(0.35f));
            Line(handle, new Vector2(x, y + 15f * scale), new Vector2(x + columns * cell - 8f * scale, y + 15f * scale), 1f * scale, color.WithAlpha(active ? 0.3f : 0.1f));
            y += 22f * scale;

            for (var i = 0; i < runes.Count; i++)
            {
                var rune = runes[i];
                var column = i % columns;
                if (i > 0 && column == 0)
                    y += row;

                if (y + box > bottom)
                    return;

                var center = new Vector2(x + column * cell + cell / 2f - 4f * scale, y + box / 2f);
                var alpha = active ? 1f : 0.35f;
                var diameter = RuneCircleArt.RingDiameter(box / 2f);
                handle.DrawCircle(center, box / 2f, Night);
                Texture(handle, RuneCircleArt.Ring(_clyde, 1.3f * scale * RuneCircleArt.RingSize / diameter), center, diameter, 0f, rune.Color.WithAlpha(0.7f * alpha));
                if (active)
                    Texture(handle, RuneCircleArt.Glow(_clyde), center, box * 1.8f, 0f, rune.Color.WithAlpha(0.12f));
                DrawGlyph(handle, rune.Stroke, UIBox2.FromDimensions(center - new Vector2(box * 0.42f), new Vector2(box * 0.84f)), rune.Color.WithAlpha(alpha), 1.4f * scale);
                var name = Loc.GetString(rune.Name);
                var room = cell - 4f * scale;
                var nameFont = TextWidth(_small, name) <= room ? _small : _tiny;
                Centered(handle, nameFont, Fit(nameFont, name, room), new Vector2(center.X, y + box + 1f * scale), Color.White.WithAlpha(0.25f + 0.6f * alpha));
                if (rune.Ultimate && UltimateCooldown?.Invoke() is { } ultimate)
                    DrawCooldownDial(handle, center, box / 2f, ultimate.Left, ultimate.Fraction, scale);
            }

            y += row + 4f * scale;
        }
    }

    private static float TextWidth(Font font, string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }

    private static Color KindHeader(RuneKind kind)
    {
        return kind switch
        {
            RuneKind.Form => FormColor,
            RuneKind.Element => ElementColor,
            _ => ModifierColor,
        };
    }

    private void DrawTrail(DrawingHandleScreen handle, IReadOnlyList<Vector2> points, Color color, float alpha, float scale)
    {
        if (points.Count == 0)
            return;

        var glow = RuneCircleArt.Glow(_clyde);
        for (var i = 0; i < points.Count; i += 2)
        {
            var age = (float) (i + 1) / points.Count;
            Texture(handle, glow, points[i], 26f * scale, 0f, color.WithAlpha(0.18f * alpha * age));
        }

        var core = Color.InterpolateBetween(color, Color.White, 0.55f);
        for (var i = 1; i < points.Count; i++)
            Line(handle, points[i - 1], points[i], 2.6f * scale, core.WithAlpha(alpha));

        Texture(handle, glow, points[^1], 24f * scale, 0f, Color.White.WithAlpha(alpha));
    }

    private void DrawGlyph(DrawingHandleScreen handle, IReadOnlyList<Vector2> points, UIBox2 rect, Color color, float width)
    {
        if (points.Count < 2)
            return;

        var inner = new UIBox2(rect.TopLeft + rect.Size * 0.15f, rect.BottomRight - rect.Size * 0.15f);
        Vector2 Map(Vector2 p) => inner.TopLeft + new Vector2(p.X * inner.Width, p.Y * inner.Height);

        for (var i = 1; i < points.Count; i++)
            Line(handle, Map(points[i - 1]), Map(points[i]), width, color);
    }

    private void Texture(DrawingHandleScreen handle, Texture texture, Vector2 center, float diameter, float rotation, Color color)
    {
        if (color.A <= 0.001f || diameter <= 0.5f)
            return;

        var baseTransform = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(center) * baseTransform);
        handle.DrawTextureRect(texture, UIBox2.FromDimensions(new Vector2(-diameter / 2f), new Vector2(diameter)), color);
        handle.SetTransform(baseTransform);
    }

    private static Vector2 Dir(float angle)
    {
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private void Line(DrawingHandleScreen handle, Vector2 a, Vector2 b, float width, Color color)
    {
        Quad(handle, a, b, width + 1.4f, color.WithAlpha(color.A * 0.3f));
        Quad(handle, a, b, width, color);
        if (width > 2f)
            handle.DrawCircle(b, width / 2f, color);
    }

    private void Quad(DrawingHandleScreen handle, Vector2 a, Vector2 b, float width, Color color)
    {
        var delta = b - a;
        var length = delta.Length();
        if (length < 0.01f)
            return;

        var normal = new Vector2(-delta.Y, delta.X) / length * (width / 2f);
        _quad.Clear();
        _quad.Add(a + normal);
        _quad.Add(a - normal);
        _quad.Add(b + normal);
        _quad.Add(b + normal);
        _quad.Add(a - normal);
        _quad.Add(b - normal);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _quad, color);
    }

    private void Arc(DrawingHandleScreen handle, Vector2 center, float radius, float width, float start, float sweep, Color color)
    {
        var segments = Math.Clamp((int) (MathF.Abs(sweep) * radius / 4f), 8, 200);
        _scratch.Clear();
        for (var i = 0; i <= segments; i++)
        {
            var angle = start + sweep * i / segments;
            var dir = Dir(angle);
            _scratch.Add(center + dir * (radius + width / 2f));
            _scratch.Add(center + dir * (radius - width / 2f));
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _scratch, color);
    }

    private static void Centered(DrawingHandleScreen handle, Font font, string text, Vector2 top, Color color)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, 1f, out var metrics))
                width += metrics.Advance;
        }

        handle.DrawString(font, new Vector2(top.X - width / 2f, top.Y), text, color);
    }
}
