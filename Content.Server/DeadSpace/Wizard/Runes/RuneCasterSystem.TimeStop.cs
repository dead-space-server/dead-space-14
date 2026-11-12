// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneTimeStopFieldComponent : Component
{
    public EntityUid Caster;
    public float Radius;
    public TimeSpan End;
    public readonly HashSet<EntityUid> Frozen = new();
}

[RegisterComponent]
public sealed partial class RuneFrozenComponent : Component
{
    public EntityUid Field;
    public bool Mob;
    public Vector2 Velocity;
    public float Angular;
    public DamageSpecifier Stored = new();
}

public sealed partial class RuneCasterSystem
{
    private static readonly EntProtoId TimeStopField = "RuneTimeStopField";
    private static readonly EntProtoId TimeStopTile = "RuneTimeStopTile";
    private static readonly EntProtoId TimeStopGear = "RuneTimeStopGear";
    private static readonly TimeSpan FreezeRefresh = TimeSpan.FromSeconds(1.2);

    private void InitializeTimeStop()
    {
        SubscribeLocalEvent<RuneFrozenComponent, BeforeDamageChangedEvent>(OnFrozenDamage);
        SubscribeLocalEvent<RuneTimeStopFieldComponent, ComponentShutdown>(OnFieldShutdown);
    }

    private void StartTimeStop(RuneCastContext ctx, RuneTimeStopEffect effect)
    {
        var center = _transform.ToMapCoordinates(ctx.Point);
        var radius = effect.Radius * ctx.RangeScale;
        var duration = effect.Duration * ctx.DurationScale;

        var field = Spawn(TimeStopField, center);
        var comp = EnsureComp<RuneTimeStopFieldComponent>(field);
        comp.Caster = ctx.Caster;
        comp.Radius = radius;
        comp.End = Timing.CurTime + TimeSpan.FromSeconds(duration);

        foreach (var (coords, _) in Positions(ctx, RuneSpawnPattern.AreaAtPoint, (int) MathF.Ceiling(radius) + 1))
        {
            if ((_transform.ToMapCoordinates(coords).Position - center.Position).Length() > radius)
                continue;

            EnsureComp<TimedDespawnComponent>(Spawn(TimeStopTile, coords)).Lifetime = duration;
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = i * MathF.Tau / 12f;
            var at = new MapCoordinates(center.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, center.MapId);
            EnsureComp<TimedDespawnComponent>(Spawn(TimeStopGear, at)).Lifetime = duration;
        }

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, radius + 4f))
            _popup.PopupEntity(Loc.GetString("rune-time-stop-start"), mob, mob, PopupType.LargeCaution);
    }

    private void UpdateTimeStop(float frameTime)
    {
        var query = EntityQueryEnumerator<RuneTimeStopFieldComponent>();
        while (query.MoveNext(out var uid, out var field))
        {
            if (Timing.CurTime >= field.End)
            {
                QueueDel(uid);
                continue;
            }

            var center = _transform.GetMapCoordinates(uid);
            foreach (var ent in _lookup.GetEntitiesInRange(center, field.Radius))
            {
                if (ent == field.Caster || ent == uid || field.Frozen.Contains(ent))
                    continue;

                if (HasComp<MobStateComponent>(ent))
                    Freeze(ent, uid, field, true);
                else if (HasComp<ProjectileComponent>(ent) || HasComp<ThrownItemComponent>(ent))
                    Freeze(ent, uid, field, false);
            }

            foreach (var frozen in field.Frozen)
            {
                if (TerminatingOrDeleted(frozen) || !TryComp<RuneFrozenComponent>(frozen, out var state))
                    continue;

                if (state.Mob)
                {
                    _stun.TryUpdateParalyzeDuration(frozen, FreezeRefresh);
                    continue;
                }

                if (HasComp<PhysicsComponent>(frozen))
                {
                    _physics.SetLinearVelocity(frozen, Vector2.Zero);
                    _physics.SetAngularVelocity(frozen, 0f);
                }

                if (TryComp<TimedDespawnComponent>(frozen, out var despawn))
                    despawn.Lifetime += frameTime;
            }
        }
    }

    private void Freeze(EntityUid ent, EntityUid fieldUid, RuneTimeStopFieldComponent field, bool mob)
    {
        if (HasComp<RuneFrozenComponent>(ent))
            return;

        var state = EnsureComp<RuneFrozenComponent>(ent);
        state.Field = fieldUid;
        state.Mob = mob;
        if (!mob && TryComp<PhysicsComponent>(ent, out var physics))
        {
            state.Velocity = physics.LinearVelocity;
            state.Angular = physics.AngularVelocity;
        }

        field.Frozen.Add(ent);
    }

    private void OnFrozenDamage(Entity<RuneFrozenComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!ent.Comp.Mob)
            return;

        ent.Comp.Stored += args.Damage;
        args.Cancelled = true;
    }

    private void OnFieldShutdown(Entity<RuneTimeStopFieldComponent> ent, ref ComponentShutdown args)
    {
        foreach (var frozen in ent.Comp.Frozen)
        {
            if (TerminatingOrDeleted(frozen) || !TryComp<RuneFrozenComponent>(frozen, out var state))
                continue;

            var stored = state.Stored;
            var mob = state.Mob;
            var velocity = state.Velocity;
            var angular = state.Angular;
            RemComp<RuneFrozenComponent>(frozen);

            if (!mob)
            {
                _physics.SetLinearVelocity(frozen, velocity);
                _physics.SetAngularVelocity(frozen, angular);
                continue;
            }

            if (!stored.Empty)
                _damageable.TryChangeDamage(frozen, stored, origin: ent.Comp.Caster);

            _popup.PopupEntity(Loc.GetString("rune-time-stop-end"), frozen, frozen, PopupType.LargeCaution);
        }

        ent.Comp.Frozen.Clear();
    }
}
