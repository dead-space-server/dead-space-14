// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.DeadSpace.Wizard.Runes;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneCasterSystem : SharedRuneCasterSystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float MinStrokePixels = 16f;

    private RuneCircleControl? _circle;
    private EntityUid? _caster;
    private bool _inBlood;
    private readonly List<string> _chain = new();
    private readonly Dictionary<RuneKind, List<(string Id, Vector2[] Template)>> _templates = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RuneCasterComponent, RuneCircleActionEvent>(OnToggle);
        SubscribeLocalEvent<RuneCasterComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ => Close());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Close();
    }

    private void OnToggle(Entity<RuneCasterComponent> ent, ref RuneCircleActionEvent args)
    {
        if (args.Handled || _player.LocalEntity != ent.Owner)
            return;

        args.Handled = true;
        if (_circle != null)
            Close();
        else
            Open(ent);
    }

    private void OnShutdown(Entity<RuneCasterComponent> ent, ref ComponentShutdown args)
    {
        if (_caster == ent.Owner)
            Close();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_circle == null || _caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp) || comp.InBlood == _inBlood)
            return;

        RebuildKnown(comp);
        _circle.Say(Loc.GetString(comp.InBlood ? "rune-blood-awaken" : "rune-blood-fade"));

        if (!comp.InBlood && _chain.Contains(comp.BloodRune.Id))
            _chain.Clear();

        Refresh();
    }

    private void RebuildKnown(RuneCasterComponent comp)
    {
        _inBlood = comp.InBlood;
        _templates.Clear();

        var ids = new List<string>();
        foreach (var id in comp.Known)
            ids.Add(id);

        if (comp.InBlood && !ids.Contains(comp.BloodRune.Id))
            ids.Add(comp.BloodRune.Id);

        var known = new List<RunePrototype>();
        foreach (var id in ids)
        {
            if (!Proto.TryIndex<RunePrototype>(id, out var rune))
                continue;

            known.Add(rune);
            var template = RuneRecognizer.Normalize(rune.Stroke);
            if (template == null)
                continue;

            if (!_templates.TryGetValue(rune.Kind, out var list))
                _templates[rune.Kind] = list = new List<(string, Vector2[])>();

            list.Add((rune.ID, template));
        }

        if (_circle != null)
            _circle.Known = known.OrderBy(r => r.Kind).ThenBy(r => r.Order).ToList();
    }

    private void Open(Entity<RuneCasterComponent> ent)
    {
        _caster = ent;
        _chain.Clear();

        _circle = new RuneCircleControl
        {
            Strain = () => _caster is { } caster && TryComp<RuneCasterComponent>(caster, out var comp) ? GetStrainFraction(comp) : 0f,
            Cooldown = CurrentCooldown,
            ActiveCooldowns = () => _caster is { } caster && TryComp<RuneCasterComponent>(caster, out var comp)
                ? ActiveCooldowns(comp)
                : new List<(string Name, float Left, float Fraction)>(),
            UltimateCooldown = () => _caster is { } caster && TryComp<RuneCasterComponent>(caster, out var comp) && TryGetUltimateCooldown(comp, out var left, out var fraction)
                ? (left, fraction)
                : null,
        };
        RebuildKnown(ent.Comp);

        _circle.StrokeFinished += OnStroke;
        _circle.CastRequested += () => Send(false);
        _circle.InscribeRequested += () => Send(true);
        _circle.ClearRequested += () =>
        {
            _chain.Clear();
            Refresh();
        };
        _circle.CloseRequested += Close;

        _ui.PopupRoot.AddChild(_circle);
        LayoutContainer.SetAnchorAndMarginPreset(_circle, LayoutContainer.LayoutPreset.Wide);
        Refresh();
    }

    private void Close()
    {
        _circle?.Orphan();
        _circle = null;
        _caster = null;
        _chain.Clear();
    }

    private void OnStroke(List<Vector2> points)
    {
        if (_circle == null || _caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp) || points.Count < 2)
            return;

        var min = points[0];
        var max = points[0];
        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        if (MathF.Max(max.X - min.X, max.Y - min.Y) < MinStrokePixels * _circle.UIScale)
            return;

        var expected = NextKind(_chain, comp.MaxRunes);
        var threshold = GetStrainFraction(comp) > 0.5f ? comp.ShakenRecognition : comp.Recognition;

        if (expected is { } kind &&
            _templates.TryGetValue(kind, out var templates) &&
            RuneRecognizer.TryMatch(points, templates.Where(t => _chain.Count == 2 || !IsFusion(t.Id)), threshold, out var id, out _) &&
            id != null)
        {
            _chain.Add(id);
            RaiseNetworkEvent(new RuneDrawnMessage(id, _chain.Count - 1, new List<string>(_chain)));
            _circle.Flash(true);
            Refresh();
            return;
        }

        _circle.Flash(false);
        _circle.Say(Loc.GetString("rune-circle-unrecognized"));
        RaiseNetworkEvent(new RuneFizzleMessage());
    }

    private void Send(bool inscribe)
    {
        if (_circle == null || _caster is not { } caster)
            return;

        if (_chain.Count < 2)
        {
            _circle.Flash(false);
            _circle.Say(Loc.GetString("rune-circle-need-two"));
            return;
        }

        if (inscribe && _chain.Count > 2)
        {
            _circle.Flash(false);
            _circle.Say(Loc.GetString("rune-inscribe-too-complex"));
            return;
        }

        EntityCoordinates coords;
        if (inscribe)
        {
            coords = Transform(caster).Coordinates;
        }
        else
        {
            var map = _eye.PixelToMap(_input.MouseScreenPosition.Position);
            if (map.MapId == MapId.Nullspace)
                return;

            coords = _transform.ToCoordinates(map);
        }

        RaiseNetworkEvent(new RuneCastMessage(new List<string>(_chain), GetNetCoordinates(coords), inscribe));
        _chain.Clear();
        Refresh();
    }

    private (float Left, float Fraction)? CurrentCooldown()
    {
        if (_caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp) || !TryCompose(_chain, out var composition))
            return null;

        (float Left, float Fraction)? result = null;
        if (TryGetCooldown(comp, composition.Key, out var left, out var fraction))
            result = (left, fraction);

        if (composition.Form.Ultimate && TryGetUltimateCooldown(comp, out var ultimateLeft, out var ultimateFraction) && ultimateLeft > (result?.Left ?? 0f))
            result = (ultimateLeft, ultimateFraction);

        return result;
    }

    private void Refresh()
    {
        if (_circle == null || _caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp))
            return;

        _circle.Chain.Clear();
        _circle.Chain.AddRange(ResolveRunes(_chain));
        _circle.Expected = NextKind(_chain, comp.MaxRunes);
        _circle.SecondElement = _chain.Count == 3 && IsFusion(_chain[2]);

        if (_chain.Count < 2)
        {
            _circle.Preview = null;
            return;
        }

        _circle.Preview = TryCompose(_chain, out var composition) && composition.Spell != null
            ? Loc.GetString("rune-circle-preview", ("spell", Loc.GetString(composition.Spell.Name)))
            : Loc.GetString("rune-circle-preview-mismatch");
    }
}
