// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Magic.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneCasterSystem : SharedRuneCasterSystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly RuneVisualsSystem _visuals = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private const float MinStrokePixels = 16f;
    private const float RangeSlack = 0.5f;
    private const float TargetRadius = 0.9f;
    private const float SelfRadius = 0.6f;
    private static readonly ProtoId<ShaderPrototype> OutlineInRange = "SelectionOutlineInrange";
    private static readonly ProtoId<ShaderPrototype> OutlineOutOfRange = "SelectionOutline";

    private ShaderInstance? _outlineIn;
    private ShaderInstance? _outlineOut;
    private readonly List<EntityUid> _outlined = new();

    private RuneCircleControl? _circle;
    private EntityUid? _caster;
    private bool _inBlood;
    private readonly List<string> _chain = new();
    private readonly List<string> _scrollChain = new();
    private readonly Dictionary<RuneKind, List<(string Id, Vector2[] Template)>> _templates = new();
    private EntityUid? _armed;
    private EntityUid? _quillPaper;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RuneCasterComponent, RuneCircleActionEvent>(OnToggle);
        SubscribeLocalEvent<RuneCasterComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ => Close(false));
        SubscribeNetworkEvent<RuneQuillOpenEvent>(OnQuillOpen);
        SubscribeNetworkEvent<RuneCastResultEvent>(OnCastResult);
        _outlineIn = Proto.Index(OutlineInRange).InstanceUnique();
        _outlineOut = Proto.Index(OutlineOutOfRange).InstanceUnique();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateScrolls();
        var aim = ComputeAim();
        _visuals.Aim = aim;
        SetOutline(aim?.Targets, aim is { InRange: true });
    }

    private void OnQuillOpen(RuneQuillOpenEvent ev)
    {
        if (_player.LocalEntity is not { } local || !TryComp<RuneCasterComponent>(local, out var comp) || !TryGetEntity(ev.Paper, out var paper))
            return;

        if (_circle == null)
            Open((local, comp));

        if (_circle == null)
            return;

        _quillPaper = paper;
        _armed = null;
        _chain.Clear();
        _circle.Quill = true;
        _circle.Armed = null;
        _circle.Say(Loc.GetString("rune-circle-quill-open"));
        Refresh();
    }

    private void OnCastResult(RuneCastResultEvent ev)
    {
        if (_circle == null)
            return;

        if (!ev.Success)
        {
            if (ev.Reason != null)
                _circle.Warn(ev.Reason);
            return;
        }

        if (_armed != null)
            return;

        _chain.Clear();
        Refresh();
    }

    private List<string> ActiveChain()
    {
        if (_armed is not { } scroll || !TryComp<RuneScrollComponent>(scroll, out var comp))
            return _chain;

        _scrollChain.Clear();
        foreach (var rune in comp.Runes)
            _scrollChain.Add(rune.Id);

        return _scrollChain;
    }

    private void UpdateScrolls()
    {
        if (_circle == null || _caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp))
            return;

        _circle.Scrolls.Clear();
        foreach (var scroll in comp.Scrolls)
        {
            if (!TryComp<RuneScrollComponent>(scroll, out var data) || data.Runes.Count < 2 ||
                !Proto.TryIndex(data.Runes[0], out var form) || !Proto.TryIndex(data.Runes[1], out var element))
                continue;

            var left = 0f;
            var fraction = 0f;
            var chain = data.Runes.Select(r => r.Id).ToList();
            if (TryCompose(chain, out var composition))
                TryGetCooldown(comp, composition.Key, out left, out fraction);

            _circle.Scrolls.Add(new RuneScrollChip(scroll, form, element, left, fraction));
        }

        if (_armed is { } armed && !comp.Scrolls.Contains(armed))
        {
            _armed = null;
            _circle.Armed = null;
            Refresh();
        }
    }

    private void ToggleScroll(EntityUid scroll)
    {
        if (_circle == null)
            return;

        _armed = _armed == scroll ? null : scroll;
        _quillPaper = null;
        _circle.Quill = false;
        _circle.Armed = _armed;
        Refresh();
    }

    private void SendScroll()
    {
        if (_circle == null || _armed is not { } scroll || !CheckAim())
            return;

        var map = _eye.PixelToMap(_input.MouseScreenPosition.Position);
        if (map.MapId == MapId.Nullspace)
            return;

        RaiseNetworkEvent(new RuneScrollCastMessage(GetNetEntity(scroll), GetNetCoordinates(_transform.ToCoordinates(map))));
    }

    private RuneAim? ComputeAim()
    {
        if (_circle == null || _quillPaper != null || _caster is not { } caster || TerminatingOrDeleted(caster) ||
            !TryCompose(ActiveChain(), out var composition) || composition.Spell is not { } spell)
            return null;

        var from = _transform.GetMapCoordinates(caster);
        var to = _eye.PixelToMap(_input.MouseScreenPosition.Position);
        var aim = new RuneAim
        {
            Caster = caster,
            Range = spell.Range * composition.RangeScale,
            Targeting = spell.Targeting,
            Color = composition.Element.Color,
            Point = to,
        };

        if (spell.Targeting == RuneTargeting.Self)
        {
            aim.InRange = true;
            aim.Valid = true;
            return aim;
        }

        aim.InRange = to.MapId == from.MapId && (to.Position - from.Position).Length() <= aim.Range + RangeSlack;
        if (spell.Effects.Find(e => e is RuneAnimateEffect) is RuneAnimateEffect animate)
        {
            FindAnimated(to, animate, composition, aim.Targets);
            aim.Valid = aim.Targets.Count > 0;
            aim.Problem = "rune-animate-nothing";
            return aim;
        }

        if (WantsTarget(spell) && FindAimTarget(caster, from, to, spell) is { } target)
            aim.Targets.Add(target);

        aim.Valid = spell.Targeting is not (RuneTargeting.Entity or RuneTargeting.EntityOrSelf) || aim.Targets.Count > 0;
        return aim;
    }

    private static bool WantsTarget(RuneSpellPrototype spell)
    {
        return spell.Targeting is RuneTargeting.Entity or RuneTargeting.EntityOrSelf ||
               spell.Effects.Exists(e => e is RuneTelekinesisEffect or RuneTeleportEffect { Mode: RuneTeleportMode.SwapWithTarget });
    }

    private void FindAnimated(MapCoordinates to, RuneAnimateEffect effect, RuneComposition composition, List<EntityUid> found)
    {
        var candidates = new List<(EntityUid Uid, float Distance)>();
        foreach (var item in _lookup.GetEntitiesInRange<AnimateableComponent>(to, effect.Radius * composition.RangeScale))
        {
            if (_container.IsEntityInContainer(item) ||
                HasComp<MindContainerComponent>(item) ||
                HasComp<AnimateComponent>(item) ||
                HasComp<MovementSpeedModifierComponent>(item))
                continue;

            candidates.Add((item, (_transform.GetWorldPosition(item) - to.Position).LengthSquared()));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var count = Math.Min(candidates.Count, effect.Count + composition.Extra);
        for (var i = 0; i < count; i++)
            found.Add(candidates[i].Uid);
    }

    private EntityUid? FindAimTarget(EntityUid caster, MapCoordinates from, MapCoordinates to, RuneSpellPrototype spell)
    {
        if (to.MapId != from.MapId)
            return null;

        if (spell.Targeting == RuneTargeting.EntityOrSelf && (from.Position - to.Position).Length() < SelfRadius)
            return caster;

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(to, TargetRadius))
        {
            if (mob.Owner == caster)
                continue;

            var distance = (_transform.GetWorldPosition(mob) - to.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = mob;
            bestDistance = distance;
        }

        if (best != null || spell.Effects.Find(e => e is RuneTelekinesisEffect) is not RuneTelekinesisEffect grasp)
            return best;

        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(to, grasp.GrabRadius))
        {
            if (_container.IsEntityInContainer(item) || Transform(item).Anchored)
                continue;

            var distance = (_transform.GetWorldPosition(item) - to.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = item;
            bestDistance = distance;
        }

        return best;
    }

    private void SetOutline(List<EntityUid>? targets, bool inRange)
    {
        foreach (var old in _outlined)
        {
            if ((targets == null || !targets.Contains(old)) && TryComp<SpriteComponent>(old, out var oldSprite) &&
                (oldSprite.PostShader == _outlineIn || oldSprite.PostShader == _outlineOut))
                oldSprite.PostShader = null;
        }

        _outlined.Clear();
        if (targets == null)
            return;

        var shader = inRange ? _outlineIn : _outlineOut;
        shader?.SetParameter("outline_width", _eye.MainViewport.GetRenderScale());
        foreach (var uid in targets)
        {
            _outlined.Add(uid);
            if (TryComp<SpriteComponent>(uid, out var sprite) && sprite.PostShader != shader)
                sprite.PostShader = shader;
        }
    }

    private bool CheckAim()
    {
        if (_circle == null || _caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp) ||
            !TryCompose(ActiveChain(), out var composition) || composition.Spell == null)
            return true;

        if (TryGetCooldown(comp, composition.Key, out var left, out _) && left > 0f)
        {
            _circle.Warn(Loc.GetString("rune-cast-cooldown", ("seconds", (int) MathF.Ceiling(left))));
            return false;
        }

        if (composition.Form.Ultimate && TryGetUltimateCooldown(comp, out var ultimate, out _) && ultimate > 0f)
        {
            _circle.Warn(Loc.GetString("rune-ultimate-cooldown", ("seconds", (int) MathF.Ceiling(ultimate))));
            return false;
        }

        if (ComputeAim() is not { } aim)
            return true;

        if (!aim.InRange)
        {
            _circle.Warn(Loc.GetString("rune-cast-range"));
            return false;
        }

        if (!aim.Valid)
        {
            _circle.Warn(Loc.GetString(aim.Problem));
            return false;
        }

        return true;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Close(false);
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
        _circle.CastRequested += () =>
        {
            if (_quillPaper != null)
                Send(true);
            else if (_armed != null)
                SendScroll();
            else
                Send(false);
        };
        _circle.InscribeRequested += () => Send(true);
        _circle.ScrollToggled += ToggleScroll;
        _circle.ClearRequested += () =>
        {
            if (_chain.Count > 0)
                RaiseNetworkEvent(new RuneSigilClearMessage());

            _chain.Clear();
            Refresh();
        };
        _circle.CloseRequested += () => Close();

        _ui.PopupRoot.AddChild(_circle);
        LayoutContainer.SetAnchorAndMarginPreset(_circle, LayoutContainer.LayoutPreset.Wide);
        Refresh();
    }

    private void Close(bool notify = true)
    {
        if (notify && _circle != null && _chain.Count > 0)
            RaiseNetworkEvent(new RuneSigilClearMessage());

        _circle?.Orphan();
        _circle = null;
        _caster = null;
        _armed = null;
        _quillPaper = null;
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

        if (_armed != null)
        {
            _armed = null;
            _circle.Armed = null;
        }

        var expected = ExpectedNext(comp);
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
            _circle.Warn(Loc.GetString("rune-circle-need-two"));
            return;
        }

        if (inscribe && _quillPaper == null && !HoldsQuill(caster))
        {
            _circle.Warn(Loc.GetString("rune-inscribe-need-quill"));
            return;
        }

        if (inscribe && _chain.Count > 2)
        {
            _circle.Warn(Loc.GetString("rune-inscribe-too-complex"));
            return;
        }

        if (!inscribe && !CheckAim())
            return;

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

        NetEntity? paper = inscribe && _quillPaper is { } chosen ? GetNetEntity(chosen) : null;
        RaiseNetworkEvent(new RuneCastMessage(new List<string>(_chain), GetNetCoordinates(coords), inscribe, paper));
        if (inscribe)
            _chain.Clear();
        if (inscribe && _quillPaper != null)
        {
            _quillPaper = null;
            _circle.Quill = false;
        }

        Refresh();
    }

    private RuneKind? ExpectedNext(RuneCasterComponent comp)
    {
        if (_armed != null || _quillPaper != null && _chain.Count >= 2)
            return null;

        return NextKind(_chain, comp.MaxRunes);
    }

    private bool HoldsQuill(EntityUid uid)
    {
        if (!TryComp<HandsComponent>(uid, out var hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((uid, hands)))
        {
            if (HasComp<RuneQuillComponent>(held))
                return true;
        }

        return false;
    }

    private (float Left, float Fraction)? CurrentCooldown()
    {
        if (_caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp) || !TryCompose(ActiveChain(), out var composition))
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

        var chain = ActiveChain();
        _circle.Chain.Clear();
        _circle.Chain.AddRange(ResolveRunes(chain));
        _circle.Expected = ExpectedNext(comp);
        _circle.SecondElement = chain.Count == 3 && IsFusion(chain[2]);

        if (chain.Count < 2)
        {
            _circle.Preview = null;
            return;
        }

        var preview = _armed != null ? "rune-circle-preview-scroll" : _quillPaper != null ? "rune-circle-preview-quill" : "rune-circle-preview";
        _circle.Preview = TryCompose(chain, out var composition) && composition.Spell != null
            ? Loc.GetString(preview, ("spell", Loc.GetString(composition.Spell.Name)))
            : Loc.GetString("rune-circle-preview-mismatch");
    }
}
