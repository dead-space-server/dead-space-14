// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Lightning;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneSpellSourceComponent : Component
{
    public string Element = string.Empty;

    public EntityUid? Caster;

    public RuneComposition? Composition;

    public float PowerScale = 1f;

    public List<RuneSpellEffect> OnHit = new();
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly StandingStateSystem _standing = default!;

    private RuneCastContext? _scope;

    private void InitializeMarks()
    {
        SubscribeLocalEvent<RuneMarkComponent, DamageModifyEvent>(OnMarkedDamage);
        SubscribeLocalEvent<RuneSpellSourceComponent, BeforeProjectileHitEvent>(OnSourceHit);
        SubscribeLocalEvent<MobStateComponent, HitByLightningEvent>(OnLightningHit);
    }

    private void UpdateMarks()
    {
        var query = EntityQueryEnumerator<RuneMarkComponent>();
        while (query.MoveNext(out var uid, out var mark))
        {
            if (mark.End <= Timing.CurTime)
                RemCompDeferred<RuneMarkComponent>(uid);
        }
    }

    private float Touch(RuneCastContext ctx, EntityUid target)
    {
        if (ctx.Element is not { } element || target == ctx.Caster || !HasComp<MobStateComponent>(target))
            return 1f;

        if (ctx.Touched.TryGetValue(target, out var cached))
            return cached;

        var before = CurrentMark(target);
        var scale = ApplyMark(target, element);
        ctx.Touched[target] = scale;
        if (before != RuneMark.Displaced && CurrentMark(target) == RuneMark.Displaced)
            ctx.FreshlyDisplaced.Add(target);

        return scale;
    }

    private float ApplyMark(EntityUid target, string element)
    {
        var current = CurrentMark(target);
        var result = RuneMarkRules.Resolve(element, current, _standing.IsDown(target), _random.Next(RuneMarkRules.ChaosMarks.Length));
        if (result.Extinguish)
            _flammable.Extinguish(target);

        SetMark(target, result.Next);
        return result.Scale;
    }

    public RuneMark CurrentMark(EntityUid target)
    {
        if (TryComp<RuneMarkComponent>(target, out var mark) && mark.Mark != RuneMark.None && mark.End > Timing.CurTime)
            return mark.Mark;

        if (TryComp<FlammableComponent>(target, out var flammable) && flammable.OnFire)
            return RuneMark.Burning;

        return RuneMark.None;
    }

    private void SetMark(EntityUid target, RuneMark next)
    {
        if (next == RuneMark.None)
        {
            RemCompDeferred<RuneMarkComponent>(target);
            return;
        }

        var mark = EnsureComp<RuneMarkComponent>(target);
        mark.Mark = next;
        mark.End = Timing.CurTime + TimeSpan.FromSeconds(RuneMarkRules.Duration);
        Dirty(target, mark);
    }

    private void OnMarkedDamage(Entity<RuneMarkComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        float scale;
        if (_scope is { Element: not null } ctx)
            scale = Touch(ctx, ent);
        else if (args.Origin is { } origin && !HasComp<ProjectileComponent>(origin) && TryComp<RuneSpellSourceComponent>(origin, out var source) && source.Caster != ent.Owner)
            scale = ApplyMark(ent, source.Element);
        else
            return;

        if (MathF.Abs(scale - 1f) > 0.01f)
            args.Damage *= scale;
    }

    private void OnSourceHit(Entity<RuneSpellSourceComponent> ent, ref BeforeProjectileHitEvent args)
    {
        if (args.Target == ent.Comp.Caster || ent.Comp.Caster is not { } caster || TerminatingOrDeleted(caster))
            return;

        var ctx = new RuneCastContext(caster, Transform(args.Target).Coordinates, args.Target, ent.Comp.Composition, ent.Comp.PowerScale);
        if (HasComp<MobStateComponent>(args.Target))
        {
            var scale = ctx.Element != null ? Touch(ctx, args.Target) : ApplyMark(args.Target, ent.Comp.Element);
            if (MathF.Abs(scale - 1f) > 0.01f)
                args.Damage *= scale;
        }

        foreach (var effect in ent.Comp.OnHit)
            Apply(ctx, effect);
    }

    private void OnLightningHit(Entity<MobStateComponent> ent, ref HitByLightningEvent args)
    {
        if (_scope != null)
            Touch(_scope, ent);
    }

    private void TagSource(EntityUid uid, RuneCastContext ctx, List<RuneSpellEffect>? onHit = null, float powerScale = 1f)
    {
        if (ctx.Element is not { } element)
            return;

        var source = EnsureComp<RuneSpellSourceComponent>(uid);
        source.Element = element;
        source.Caster = ctx.Caster;
        source.Composition = ctx.Composition;
        source.PowerScale = powerScale;
        if (onHit != null)
            source.OnHit = onHit;
    }

    private void PushDisplaced(RuneCastContext ctx, MapCoordinates center, float speed, float radius)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<RuneMarkComponent>(center, radius))
        {
            if (mob.Owner == ctx.Caster || mob.Comp.Mark != RuneMark.Displaced || mob.Comp.End <= Timing.CurTime || ctx.FreshlyDisplaced.Contains(mob))
                continue;

            var direction = _transform.GetWorldPosition(mob) - center.Position;
            if (direction == Vector2.Zero)
                continue;

            var throwDirection = speed < 0 ? -direction : direction.Normalized() * (radius - direction.Length());
            _throwing.TryThrow(mob, throwDirection, MathF.Abs(speed) * RuneMarkRules.Strong, ctx.Caster, recoil: false, compensateFriction: true);
        }
    }

    private float DisplacedScale(EntityUid target, RuneCastContext ctx)
    {
        return CurrentMark(target) == RuneMark.Displaced && !ctx.FreshlyDisplaced.Contains(target) ? RuneMarkRules.Strong : 1f;
    }
}
