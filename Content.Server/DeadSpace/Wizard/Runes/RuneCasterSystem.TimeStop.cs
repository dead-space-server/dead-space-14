// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneTimeStopFieldComponent : Component
{
    public EntityUid Caster;
    public float Radius;
    public Vector2 Origin;
    public MapId MapId;
    public TimeSpan End;
    public readonly HashSet<EntityUid> Frozen = new();
}

[RegisterComponent]
public sealed partial class RuneFrozenComponent : Component
{
    public EntityUid Field;
    public bool Mob;
    public bool StoppedNpc;
    public double? RotationSpeed;
    public bool Hold;
    public Vector2 HoldPosition;
    public BodyType? Body;
    public Vector2 Velocity;
    public float Angular;
    public DamageSpecifier Stored = new();
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;

    private static readonly EntProtoId TimeStopField = "RuneTimeStopField";
    private static readonly EntProtoId TimeStopTile = "RuneTimeStopTile";
    private static readonly EntProtoId TimeStopGear = "RuneTimeStopGear";
    private void InitializeTimeStop()
    {
        SubscribeLocalEvent<RuneFrozenComponent, BeforeDamageChangedEvent>(OnFrozenDamage);
        SubscribeLocalEvent<RuneFrozenComponent, UpdateCanMoveEvent>(OnFrozenMove);
        SubscribeLocalEvent<RuneFrozenComponent, InteractionAttemptEvent>(OnFrozenInteract);
        SubscribeLocalEvent<RuneFrozenComponent, UseAttemptEvent>(OnFrozenUse);
        SubscribeLocalEvent<RuneFrozenComponent, AttackAttemptEvent>(OnFrozenAttack);
        SubscribeLocalEvent<RuneFrozenComponent, ThrowAttemptEvent>(OnFrozenThrow);
        SubscribeLocalEvent<RuneFrozenComponent, ChangeDirectionAttemptEvent>(OnFrozenDirection);
        SubscribeLocalEvent<RuneTimeStopFieldComponent, ComponentShutdown>(OnFieldShutdown);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnPhysicsAfter);
        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnPhysicsBefore);
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
        comp.Origin = center.Position;
        comp.MapId = center.MapId;
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

            var center = new MapCoordinates(field.Origin, field.MapId);
            foreach (var ent in _lookup.GetEntitiesInRange(center, field.Radius))
            {
                if (ent == field.Caster || ent == uid || field.Frozen.Contains(ent))
                    continue;

                if (HasComp<MobStateComponent>(ent))
                    Freeze(ent, uid, field, true);
                else if (HasComp<HTNComponent>(ent) || HasComp<MouseRotatorComponent>(ent))
                    Freeze(ent, uid, field, false);
            }

            FreezeTracked<HTNComponent>(center, field, uid);
            CatchProjectiles(uid, field, frameTime);

            foreach (var frozen in field.Frozen)
            {
                if (TerminatingOrDeleted(frozen) || !TryComp<RuneFrozenComponent>(frozen, out var state))
                    continue;

                if (state.Hold)
                    _transform.SetWorldPosition(frozen, state.HoldPosition);

                if (!state.Mob && TryComp<TimedDespawnComponent>(frozen, out var despawn))
                    despawn.Lifetime += frameTime;
            }
        }
    }

    private void OnPhysicsBefore(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        if (ev.Prediction)
            return;

        CatchFields(ev.DeltaTime);
    }

    private void OnPhysicsAfter(ref PhysicsUpdateAfterSolveEvent ev)
    {
        if (ev.Prediction)
            return;

        CatchFields(ev.DeltaTime);
        var query = EntityQueryEnumerator<RuneFrozenComponent>();
        while (query.MoveNext(out var uid, out var state))
        {
            if (!state.Hold || TerminatingOrDeleted(uid))
                continue;

            _transform.SetWorldPosition(uid, state.HoldPosition);
        }
    }

    private void CatchFields(float dt)
    {
        var query = EntityQueryEnumerator<RuneTimeStopFieldComponent>();
        while (query.MoveNext(out var uid, out var field))
        {
            if (Timing.CurTime < field.End)
                CatchProjectiles(uid, field, dt);
        }
    }

    private void CatchProjectiles(EntityUid fieldUid, RuneTimeStopFieldComponent field, float dt)
    {
        var center = field.Origin;
        var radius = field.Radius;
        var query = EntityQueryEnumerator<ProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var physics, out var xform))
        {
            if (xform.MapID != field.MapId || uid == fieldUid || HasComp<RuneFrozenComponent>(uid))
                continue;

            var pos = _transform.GetWorldPosition(xform);
            var velocity = _physics.GetMapLinearVelocity(uid, physics, xform);
            var speed = velocity.Length();
            var offset = pos - center;
            var dist = offset.Length();
            var lookback = MathF.Max(dt, 1f / 20f);
            Vector2 park;

            if (dist <= radius)
            {
                if (speed < 0.05f)
                    park = pos;
                else
                {
                    var dir = velocity / speed;
                    var prev = pos - dir * speed * lookback;
                    if ((prev - center).Length() > radius && TryRayCircle(prev, dir, center, radius, out var crossed))
                        park = Rim(center, prev + dir * crossed, radius);
                    else
                        park = pos;
                }
            }
            else
            {
                if (speed < 0.05f)
                    continue;

                var dir = velocity / speed;
                var prev = pos - dir * speed * lookback;
                var traveled = (pos - prev).Length();
                if (TryRayCircle(prev, dir, center, radius, out var crossed) && crossed <= traveled + 0.35f)
                    park = Rim(center, prev + dir * crossed, radius);
                else if (TryRayCircle(pos, dir, center, radius, out var ahead) && ahead <= speed * lookback + 0.35f)
                    park = Rim(center, pos + dir * ahead, radius);
                else
                    continue;
            }

            ParkProjectile(uid, fieldUid, field, park, velocity, physics);
        }
    }

    private void ParkProjectile(
        EntityUid uid,
        EntityUid fieldUid,
        RuneTimeStopFieldComponent field,
        Vector2 park,
        Vector2 velocity,
        PhysicsComponent physics)
    {
        var state = EnsureComp<RuneFrozenComponent>(uid);
        state.Field = fieldUid;
        state.Mob = false;
        state.Velocity = velocity;
        state.Angular = physics.AngularVelocity;
        state.Hold = true;
        state.HoldPosition = park;
        if (physics.BodyType != BodyType.Static)
            state.Body = physics.BodyType;

        field.Frozen.Add(uid);
        var hold = EnsureComp<RuneProjectileHoldComponent>(uid);
        hold.Position = park;
        Dirty(uid, hold);
        _transform.SetWorldPosition(uid, park);
        if (physics.BodyType != BodyType.Static)
            _physics.SetBodyType(uid, BodyType.Static, body: physics);
    }

    private static Vector2 Rim(Vector2 center, Vector2 point, float radius)
    {
        var outward = point - center;
        var length = outward.Length();
        if (length < 0.001f)
            return center + new Vector2(radius + 0.2f, 0f);

        return center + outward / length * (radius + 0.2f);
    }

    private static bool TryRayCircle(Vector2 origin, Vector2 dir, Vector2 center, float radius, out float distance)
    {
        var from = origin - center;
        var b = 2f * Vector2.Dot(from, dir);
        var c = from.LengthSquared() - radius * radius;
        var disc = b * b - 4f * c;
        distance = 0f;
        if (disc < 0f)
            return false;

        var root = MathF.Sqrt(disc);
        var near = (-b - root) * 0.5f;
        var far = (-b + root) * 0.5f;
        if (near >= 0f)
        {
            distance = near;
            return true;
        }

        if (far < 0f)
            return false;

        distance = far;
        return true;
    }

    private void FreezeTracked<T>(MapCoordinates center, RuneTimeStopFieldComponent field, EntityUid fieldUid) where T : IComponent
    {
        var range = field.Radius * field.Radius;
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid == field.Caster || uid == fieldUid || field.Frozen.Contains(uid) || xform.MapID != center.MapId)
                continue;

            if ((_transform.GetWorldPosition(xform) - center.Position).LengthSquared() > range)
                continue;

            Freeze(uid, fieldUid, field, false);
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
        if (TryComp<HTNComponent>(ent, out var htn) && htn.Enabled)
        {
            _htn.SetHTNEnabled((ent, htn), false);
            state.StoppedNpc = true;
        }

        if (TryComp<MouseRotatorComponent>(ent, out var rotator))
        {
            state.RotationSpeed = rotator.RotationSpeed;
            rotator.GoalRotation = null;
            rotator.RotationSpeed = 0;
            Dirty(ent, rotator);
        }

        if (mob)
        {
            _blocker.UpdateCanMove(ent);
            return;
        }

        if (!TryComp<PhysicsComponent>(ent, out var body) || body.BodyType == BodyType.Static)
            return;

        state.Velocity = body.LinearVelocity;
        state.Angular = body.AngularVelocity;
        state.Body = body.BodyType;
        state.Hold = true;
        state.HoldPosition = _transform.GetWorldPosition(ent);
        _physics.SetBodyType(ent, BodyType.Static, body: body);
    }

    private void OnFrozenMove(Entity<RuneFrozenComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Mob)
            args.Cancel();
    }

    private void OnFrozenInteract(Entity<RuneFrozenComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.Mob)
            args.Cancelled = true;
    }

    private void OnFrozenUse(Entity<RuneFrozenComponent> ent, ref UseAttemptEvent args)
    {
        if (ent.Comp.Mob)
            args.Cancel();
    }

    private void OnFrozenAttack(Entity<RuneFrozenComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.Mob)
            args.Cancel();
    }

    private void OnFrozenThrow(Entity<RuneFrozenComponent> ent, ref ThrowAttemptEvent args)
    {
        if (ent.Comp.Mob)
            args.Cancel();
    }

    private void OnFrozenDirection(Entity<RuneFrozenComponent> ent, ref ChangeDirectionAttemptEvent args)
    {
        if (ent.Comp.Mob)
            args.Cancel();
    }

    private void OnFrozenDamage(Entity<RuneFrozenComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.Empty)
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
            var stoppedNpc = state.StoppedNpc;
            var rotationSpeed = state.RotationSpeed;
            var bodyType = state.Body;
            RemComp<RuneFrozenComponent>(frozen);
            RemComp<RuneProjectileHoldComponent>(frozen);

            if (rotationSpeed is { } speed && TryComp<MouseRotatorComponent>(frozen, out var rotator))
            {
                rotator.RotationSpeed = speed;
                Dirty(frozen, rotator);
            }

            if (stoppedNpc && TryComp<HTNComponent>(frozen, out var htn))
                _htn.SetHTNEnabled((frozen, htn), true);

            if (!mob)
            {
                if (bodyType is { } restored)
                    _physics.SetBodyType(frozen, restored);
                _physics.SetLinearVelocity(frozen, velocity);
                _physics.SetAngularVelocity(frozen, angular);
            }
            else
            {
                _blocker.UpdateCanMove(frozen);
                _popup.PopupEntity(Loc.GetString("rune-time-stop-end"), frozen, frozen, PopupType.LargeCaution);
            }

            if (!stored.Empty)
                _damageable.TryChangeDamage(frozen, stored, origin: ent.Comp.Caster);
        }

        ent.Comp.Frozen.Clear();
    }
}
