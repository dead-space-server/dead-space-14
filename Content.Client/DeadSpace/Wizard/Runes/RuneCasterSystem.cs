// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Physics;
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
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private const float MinStrokePixels = 16f;
    private const float RangeSlack = 0.5f;
    private const float TargetRadius = 0.9f;
    private const float SelfRadius = 0.6f;
    private const float QuickCastDelay = 0.4f;
    private static readonly ProtoId<ShaderPrototype> OutlineInRange = "SelectionOutlineInrange";
    private static readonly ProtoId<ShaderPrototype> OutlineOutOfRange = "SelectionOutline";

    private ShaderInstance? _outlineIn;
    private ShaderInstance? _outlineOut;
    private readonly List<EntityUid> _outlined = new();

    private RuneCircleControl? _circle;
    private EntityUid? _caster;
    private EntityUid? _chainOwner;
    private bool _inBlood;
    private float? _quickLeft;
    private readonly List<string> _chain = new();
    private readonly Dictionary<RuneKind, List<(string Id, Vector2[] Template)>> _templates = new();

    private RuneQuillControl? _quill;
    private EntityUid? _quillScroll;
    private EntityUid? _quillCaster;
    private readonly List<string> _quillRunes = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RuneCasterComponent, RuneCircleActionEvent>(OnToggle);
        SubscribeLocalEvent<RuneCasterComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ =>
        {
            Close();
            CloseQuill();
            _chain.Clear();
            _chainOwner = null;
        });
        SubscribeNetworkEvent<RuneQuillOpenEvent>(OnQuillOpen);
        SubscribeNetworkEvent<RuneCastResultEvent>(OnCastResult);
        _outlineIn = Proto.Index(OutlineInRange).InstanceUnique();
        _outlineOut = Proto.Index(OutlineOutOfRange).InstanceUnique();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_quill is { Expired: true } || _quill is { Finished: false } && (_quillScroll is not { } scroll || TerminatingOrDeleted(scroll)))
            CloseQuill();

        UpdateQuickCast(frameTime);

        var aim = ComputeAim();
        _visuals.Aim = aim;
        SetOutline(aim?.Targets, aim is { InRange: true });
    }

    private void UpdateQuickCast(float frameTime)
    {
        if (_quickLeft is not { } left)
            return;

        if (_circle == null || !RuneCircleControl.QuickCast)
        {
            _quickLeft = null;
            return;
        }

        if (_circle.Drawing)
        {
            _quickLeft = QuickCastDelay;
            return;
        }

        left -= frameTime;
        if (left > 0f)
        {
            _quickLeft = left;
            return;
        }

        _quickLeft = null;
        _circle.CastFlash();
        Send();
    }

    private void ScheduleQuickCast()
    {
        _quickLeft = RuneCircleControl.QuickCast && TryCompose(_chain, out var composition) && composition.Spell != null
            ? QuickCastDelay
            : null;
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

        _chain.Clear();
        Refresh();
    }

    private RuneAim? ComputeAim()
    {
        if (_circle == null || _caster is not { } caster || TerminatingOrDeleted(caster) ||
            !TryCompose(_chain, out var composition) || composition.Spell is not { } spell)
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
        (aim.CasterArea, aim.PointArea) = EffectAreas(spell, composition);

        if (spell.Targeting == RuneTargeting.Self)
        {
            aim.InRange = true;
            aim.Valid = true;
            return aim;
        }

        aim.InRange = to.MapId == from.MapId && (to.Position - from.Position).Length() <= aim.Range + RangeSlack;
        FillWallGhost(caster, from, to, spell, composition, aim);
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

    private void FillWallGhost(EntityUid caster, MapCoordinates from, MapCoordinates to, RuneSpell spell, RuneComposition composition, RuneAim aim)
    {
        if (to.MapId != from.MapId)
            return;

        RuneSpawnEffect? wall = null;
        foreach (var effect in spell.Effects)
        {
            if (effect is RuneSpawnEffect { Pattern: RuneSpawnPattern.LineAtPoint } line &&
                (wall == null || line.Size * line.Rows > wall.Size * wall.Rows))
                wall = line;
        }

        if (wall == null)
            return;

        var direction = to.Position - from.Position;
        if (direction.LengthSquared() < 0.01f)
            direction = _transform.GetWorldRotation(caster).ToWorldVec();

        var forward = MathF.Abs(direction.X) > MathF.Abs(direction.Y)
            ? new Vector2(MathF.Sign(direction.X), 0f)
            : new Vector2(0f, MathF.Sign(direction.Y));
        var side = new Vector2(-forward.Y, forward.X);
        var half = Math.Max(0, wall.Size + composition.Extra * 2 - 1) / 2;
        var depth = Math.Max(0, wall.Rows - 1) / 2;

        for (var row = -depth; row <= depth; row++)
        for (var i = -half; i <= half; i++)
        {
            var map = new MapCoordinates(to.Position + side * i + forward * row, to.MapId);
            if (!_mapManager.TryFindGridAt(map, out var gridUid, out var grid))
            {
                aim.Ghost.Add((map.Position, Angle.Zero, false));
                continue;
            }

            var tile = _map.CoordinatesToTile(gridUid, grid, map);
            var blocked = wall.SkipBlocked && _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid);
            aim.Ghost.Add((_map.GridTileToWorldPos(gridUid, grid, tile), _transform.GetWorldRotation(gridUid), blocked));
        }
    }

    private static bool WantsTarget(RuneSpell spell)
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

    private EntityUid? FindAimTarget(EntityUid caster, MapCoordinates from, MapCoordinates to, RuneSpell spell)
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
        shader?.SetParameter("outline_width", (float) _eye.MainViewport.GetRenderScale());
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
            !TryCompose(_chain, out var composition) || composition.Spell == null)
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
        Close();
        CloseQuill();
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

        if (_quillCaster == ent.Owner)
            CloseQuill();

        if (_chainOwner == ent.Owner)
        {
            _chain.Clear();
            _chainOwner = null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_circle == null || _caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp) || comp.InBlood == _inBlood)
            return;

        RebuildKnown(comp);

        if (!comp.InBlood && _chain.Contains(comp.BloodRune.Id))
            ClearChain();

        Refresh();
    }

    private List<string> KnownIds(RuneCasterComponent comp)
    {
        var ids = new List<string>();
        foreach (var id in comp.Known)
            ids.Add(id);

        if (comp.InBlood && !ids.Contains(comp.BloodRune.Id))
            ids.Add(comp.BloodRune.Id);

        return ids;
    }

    private void RebuildKnown(RuneCasterComponent comp)
    {
        _inBlood = comp.InBlood;
        _templates.Clear();

        var known = new List<RunePrototype>();
        foreach (var id in KnownIds(comp))
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
        if (_chainOwner != ent.Owner || !_chain.TrueForAll(id => Knows(ent.Comp, id)))
            ClearChain();

        _chainOwner = ent.Owner;

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
        _circle.CastRequested += Send;
        _circle.ClearRequested += () =>
        {
            ClearChain();
            Refresh();
        };
        _circle.CloseRequested += Close;

        _ui.PopupRoot.AddChild(_circle);
        LayoutContainer.SetAnchorAndMarginPreset(_circle, LayoutContainer.LayoutPreset.Wide);
        Refresh();
    }

    private void ClearChain()
    {
        if (_chain.Count > 0)
            RaiseNetworkEvent(new RuneSigilClearMessage());

        _chain.Clear();
        _quickLeft = null;
    }

    private void Close()
    {
        _quickLeft = null;
        _circle?.Orphan();
        _circle = null;
        _caster = null;
    }

    private void OnStroke(List<Vector2> points)
    {
        if (_circle == null || _caster is not { } caster || !TryComp<RuneCasterComponent>(caster, out var comp) || points.Count == 0)
            return;

        if (points.Count < 2 || StrokeSize(points) < MinStrokePixels * _circle.UIScale)
        {
            UndoRune();
            return;
        }

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
            ScheduleQuickCast();
            return;
        }

        if (_quickLeft != null)
            _quickLeft = QuickCastDelay;

        _circle.Flash(false);
        _circle.Say(Loc.GetString("rune-circle-unrecognized"));
        RaiseNetworkEvent(new RuneFizzleMessage());
    }

    private void UndoRune()
    {
        if (_circle == null || _chain.Count == 0)
            return;

        _chain.RemoveAt(_chain.Count - 1);
        _quickLeft = null;
        if (_chain.Count == 0)
            RaiseNetworkEvent(new RuneSigilClearMessage());
        else
            RaiseNetworkEvent(new RuneSigilUndoMessage(new List<string>(_chain)));

        _circle.Say(Loc.GetString("rune-circle-undo"));
        Refresh();
    }

    private static float StrokeSize(List<Vector2> points)
    {
        var min = points[0];
        var max = points[0];
        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return MathF.Max(max.X - min.X, max.Y - min.Y);
    }

    private void Send()
    {
        _quickLeft = null;
        if (_circle == null || _caster == null)
            return;

        if (_chain.Count < 2)
        {
            _circle.Warn(Loc.GetString("rune-circle-need-two"));
            return;
        }

        if (!CheckAim())
            return;

        var map = _eye.PixelToMap(_input.MouseScreenPosition.Position);
        if (map.MapId == MapId.Nullspace)
            return;

        RaiseNetworkEvent(new RuneCastMessage(new List<string>(_chain), GetNetCoordinates(_transform.ToCoordinates(map))));
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

    private void OnQuillOpen(RuneQuillOpenEvent ev)
    {
        if (_player.LocalEntity is not { } local || !TryComp<RuneCasterComponent>(local, out var comp) || !TryGetEntity(ev.Scroll, out var scroll))
            return;

        CloseQuill();
        if (_circle != null)
            Close();

        RebuildKnown(comp);
        _quillScroll = scroll;
        _quillCaster = local;
        _quillRunes.Clear();
        _quill = new RuneQuillControl();
        _quill.StrokeFinished += OnQuillStroke;
        _quill.CloseRequested += CloseQuill;
        _ui.PopupRoot.AddChild(_quill);
        LayoutContainer.SetAnchorAndMarginPreset(_quill, LayoutContainer.LayoutPreset.Wide);
        RefreshQuill(comp);
    }

    private void CloseQuill()
    {
        _quill?.Orphan();
        _quill = null;
        _quillScroll = null;
        _quillCaster = null;
        _quillRunes.Clear();
    }

    private void RefreshQuill(RuneCasterComponent comp)
    {
        if (_quill == null)
            return;

        _quill.Expected = _quillRunes.Count switch
        {
            0 => RuneKind.Form,
            1 => RuneKind.Element,
            _ => null,
        };

        _quill.Hints.Clear();
        if (_quill.Expected is not { } kind)
            return;

        foreach (var id in KnownIds(comp))
        {
            if (Proto.TryIndex<RunePrototype>(id, out var rune) && rune.Kind == kind && !rune.Ultimate)
                _quill.Hints.Add(rune);
        }

        _quill.Hints.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    private void OnQuillStroke(List<Vector2> points)
    {
        if (_quill == null || _quill.Finished || _quillScroll is not { } scroll || _quillCaster is not { } caster ||
            !TryComp<RuneCasterComponent>(caster, out var comp) || points.Count < 2 || _quill.Expected is not { } kind)
            return;

        if (StrokeSize(points) < MinStrokePixels * _quill.UIScale)
            return;

        if (_templates.TryGetValue(kind, out var templates) &&
            RuneRecognizer.TryMatch(points, templates.Where(t => !IsFusion(t.Id)), comp.Recognition, out var id, out _) &&
            id != null && Proto.TryIndex<RunePrototype>(id, out var rune))
        {
            if (rune.Ultimate)
            {
                _quill.Warn(Loc.GetString("rune-inscribe-no-ultimate"));
                return;
            }

            var chain = new List<string>(_quillRunes) { id };
            if (chain.Count == 2 && (!TryCompose(chain, out var composition) || composition.Spell == null))
            {
                _quill.Warn(Loc.GetString("rune-circle-preview-mismatch"));
                return;
            }

            _quillRunes.Add(id);
            _quill.Accept(rune);
            RefreshQuill(comp);
            if (_quillRunes.Count < 2)
                return;

            RaiseNetworkEvent(new RuneQuillInscribeMessage(GetNetEntity(scroll), new List<string>(_quillRunes)));
            _quill.Done();
            return;
        }

        RaiseNetworkEvent(new RuneQuillBurnMessage(GetNetEntity(scroll)));
        _quill.Burn();
    }
}
