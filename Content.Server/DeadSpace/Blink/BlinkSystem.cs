// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Destructible;
using Content.Server.Physics.Controllers;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Blink;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Tag;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Blink;

public sealed partial class BlinkSystem : SharedBlinkSystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> WindowTag = "Window";

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(MoverController));
        SubscribeNetworkEvent<BlinkRequestEvent>(OnBlinkRequest);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<ActiveBlinkDashComponent, StartCollideEvent>(OnDashCollide);
    }

    private void OnDamage(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null || args.Origin == ent.Owner)
            return;

        // The herald's dash pick shrugs off hits.
        if (TryComp<BlinkStyleComponent>(ent, out var style) && style.Style == BlinkStyle.Dash)
            return;

        var query = EntityQueryEnumerator<BlinkItemComponent>();
        while (query.MoveNext(out var item, out var blink))
        {
            if (!CanUseItem(ent.Owner, item, blink))
                continue;

            var now = _timing.CurTime;
            EnsureComp<BlinkUserComponent>(ent.Owner).LastDamaged = now;

            var lockoutEnd = now + blink.DamageLockout;
            var cooldownEnd = blink.NextUse > lockoutEnd
                ? blink.NextUse
                : lockoutEnd;
            _alerts.ShowAlert(ent.Owner,
                blink.CooldownAlert,
                cooldown: (now, cooldownEnd),
                autoRemove: false);
            return;
        }
    }

    private void OnBlinkRequest(BlinkRequestEvent msg, EntitySessionEventArgs args)
    {
        var item = GetEntity(msg.Item);
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp<BlinkItemComponent>(item, out var blink) ||
            !CanUseItem(user, item, blink))
            return;

        var target = GetCoordinates(msg.Target);
        var origin = _transform.GetMapCoordinates(user);
        var targetMap = _transform.ToMapCoordinates(target);
        var damage = EnsureComp<BlinkUserComponent>(user);
        var now = _timing.CurTime;
        TryComp<BlinkStyleComponent>(user, out var style);
        var lockout = style is not { Style: BlinkStyle.Dash };

        if (now < blink.NextUse || lockout && now < damage.LastDamaged + blink.DamageLockout ||
            origin.MapId != targetMap.MapId || !_transform.IsValid(target))
            return;

        if (Vector2.Distance(origin.Position, targetMap.Position) > blink.Range)
            return;

        if (style is { Style: BlinkStyle.Teleport })
        {
            if (!TryTeleport(user, target, origin, targetMap, style))
                return;
        }
        else if (!TryStartDash(user, targetMap, blink.DashSpeed, blink.DashTimeout, blink.DashStallTimeout, blink.DashSound))
            return;

        blink.NextUse = now + blink.Cooldown;
        Dirty(item, blink);
        _alerts.ShowAlert(user, blink.CooldownAlert, cooldown: (now, blink.NextUse), autoRemove: false);
    }

    /// <summary>
    /// Jumps straight to a point the user can see, as long as nothing solid stands there.
    /// </summary>
    private bool TryTeleport(EntityUid user, EntityCoordinates target, MapCoordinates origin, MapCoordinates targetMap,
        BlinkStyleComponent style)
    {
        if (HasComp<ActiveBlinkDashComponent>(user) ||
            !_examine.InRangeUnOccluded(origin, targetMap, Vector2.Distance(origin.Position, targetMap.Position) + 0.5f, null) ||
            _turf.GetTileRef(target) is not { } tile || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
            return false;

        if (TryComp<PullableComponent>(user, out var pullable) && _pulling.IsPulled(user, pullable))
            _pulling.TryStopPull(user, pullable);

        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling is { } pulled &&
            TryComp<PullableComponent>(pulled, out var pulledComp))
            _pulling.TryStopPull(pulled, pulledComp);

        var xform = Transform(user);
        _audio.PlayPvs(style.TeleportSound, xform.Coordinates);
        _transform.SetCoordinates(user, xform, target);
        _transform.AttachToGridOrMap(user, xform);
        _audio.PlayPvs(style.TeleportSound, user);
        return true;
    }

    public bool TryStartDash(
        EntityUid user,
        MapCoordinates target,
        float speed,
        TimeSpan duration,
        TimeSpan stallTimeout,
        SoundSpecifier? sound = null,
        bool visual = true)
    {
        var origin = _transform.GetMapCoordinates(user);
        var offset = target.Position - origin.Position;
        if (origin.MapId != target.MapId || offset.LengthSquared() < 0.001f ||
            !TryComp<PhysicsComponent>(user, out var physics))
            return false;

        var direction = offset.Normalized();
        var active = EnsureComp<ActiveBlinkDashComponent>(user);
        active.Direction = direction;
        active.Speed = speed;
        active.Target = target;
        active.EndTime = _timing.CurTime + duration;
        active.StallTimeout = stallTimeout;
        active.LastPosition = origin.Position;
        active.LastProgress = _timing.CurTime;
        ApplyDashCollision(user, active, physics);
        _physics.SetLinearVelocity(user, direction * speed, body: physics);
        if (sound != null)
            _audio.PlayPvs(sound, user);
        // DS14: some dashes (Cain and Abel) go without the bloody blink trail.
        if (visual)
            RaiseNetworkEvent(new BlinkDashVisualEvent(GetNetEntity(user), duration));
        return true;
    }

    private void OnDashCollide(Entity<ActiveBlinkDashComponent> ent, ref StartCollideEvent args)
    {
        if (!_tags.HasTag(args.OtherEntity, WindowTag))
            return;

        if (TryComp<DamageableComponent>(args.OtherEntity, out var damageable) &&
            _destructible.TryGetDestroyedAt(args.OtherEntity, out var destroyedAt))
        {
            var remainingHealth = FixedPoint2.Max(FixedPoint2.Zero, destroyedAt.Value - damageable.TotalDamage);
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = remainingHealth * 0.5;
            _damageable.TryChangeDamage(args.OtherEntity, damage, ignoreResistances: true, origin: ent.Owner);
        }

        EndDash(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveBlinkDashComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var active, out var physics))
        {
            var position = _transform.GetMapCoordinates(uid);
            var remaining = active.Target.Position - position.Position;

            if (Vector2.DistanceSquared(position.Position, active.LastPosition) >= 0.0025f)
            {
                active.LastPosition = position.Position;
                active.LastProgress = _timing.CurTime;
            }

            if (_timing.CurTime >= active.EndTime ||
                _timing.CurTime >= active.LastProgress + active.StallTimeout ||
                position.MapId != active.Target.MapId ||
                remaining.LengthSquared() <= 0.04f ||
                Vector2.Dot(remaining, active.Direction) <= 0f)
            {
                EndDash(uid, physics);
                continue;
            }

            _physics.SetLinearVelocity(uid, active.Direction * active.Speed, body: physics);
        }
    }

    private void EndDash(EntityUid uid, PhysicsComponent? physics = null)
    {
        if (TryComp<ActiveBlinkDashComponent>(uid, out var active))
            RestoreCollision(uid, active, physics);

        if (Resolve(uid, ref physics, false))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

        RemCompDeferred<ActiveBlinkDashComponent>(uid);
    }

    private void ApplyDashCollision(EntityUid uid, ActiveBlinkDashComponent active, PhysicsComponent physics)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        active.OriginalCollisionMasks.Clear();
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            active.OriginalCollisionMasks[id] = fixture.CollisionMask;
            var dashMask = fixture.CollisionMask & (int) CollisionGroup.Impassable;
            _physics.SetCollisionMask(uid, id, fixture, dashMask, fixtures, physics);
        }
    }

    private void RestoreCollision(EntityUid uid, ActiveBlinkDashComponent active, PhysicsComponent? physics)
    {
        if (!Resolve(uid, ref physics, false) ||
            !TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (id, mask) in active.OriginalCollisionMasks)
        {
            if (fixtures.Fixtures.TryGetValue(id, out var fixture))
                _physics.SetCollisionMask(uid, id, fixture, mask, fixtures, physics);
        }

        active.OriginalCollisionMasks.Clear();
    }

    private bool CanUseItem(EntityUid user, EntityUid item, BlinkItemComponent blink)
    {
        return blink.NeedHand
            ? _hands.IsHolding(user, item)
            : _inventory.TryGetContainingSlot(item, out _) && Transform(item).ParentUid == user;
    }
}

[RegisterComponent]
public sealed partial class BlinkUserComponent : Component
{
    public TimeSpan LastDamaged = TimeSpan.MinValue;
}

[RegisterComponent]
public sealed partial class ActiveBlinkDashComponent : Component
{
    public Vector2 Direction;
    public float Speed;
    public TimeSpan EndTime;
    public TimeSpan StallTimeout;
    public TimeSpan LastProgress;
    public MapCoordinates Target;
    public Vector2 LastPosition;
    public readonly Dictionary<string, int> OriginalCollisionMasks = new();
}
