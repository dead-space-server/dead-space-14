using Content.Shared.Gravity;
using Content.Shared.StepTrigger.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.StepTrigger.Systems;

public sealed partial class StepTriggerSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

    //DS-14
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _triggersByTripper = new();
    private readonly List<EntityUid> _colliding = new();
    private readonly List<EntityUid> _stale = new();

    public override void Initialize()
    {
        UpdatesOutsidePrediction = true;
        SubscribeLocalEvent<StepTriggerComponent, AfterAutoHandleStateEvent>(TriggerHandleState);

        SubscribeLocalEvent<StepTriggerComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<StepTriggerComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<StepTriggerComponent, ComponentShutdown>(OnTriggerShutdown); //DS-14
        EntityManager.EntityDeleted += OnEntityDeleted; //DS-14
#if DEBUG
        SubscribeLocalEvent<StepTriggerComponent, ComponentStartup>(OnStartup);
    }
    private void OnStartup(EntityUid uid, StepTriggerComponent component, ComponentStartup args)
    {
        if (!component.Active)
            return;

        if (!TryComp(uid, out FixturesComponent? fixtures) || fixtures.FixtureCount == 0)
            Log.Warning($"{ToPrettyString(uid)} has an active step trigger without any fixtures.");
#endif
    }

    //DS-14 start
    public override void Shutdown()
    {
        EntityManager.EntityDeleted -= OnEntityDeleted;
        base.Shutdown();
    }
    //DS-14 end

    public override void Update(float frameTime)
    {
        var query = GetEntityQuery<PhysicsComponent>();
        var enumerator = EntityQueryEnumerator<StepTriggerActiveComponent, StepTriggerComponent, TransformComponent>();

        while (enumerator.MoveNext(out var uid, out var active, out var trigger, out var transform))
        {
            if (!Update(uid, trigger, transform, query))
            {
                continue;
            }

            RemCompDeferred(uid, active);
        }
    }

    private bool Update(EntityUid uid, StepTriggerComponent component, TransformComponent transform, EntityQuery<PhysicsComponent> query)
    {
        //DS-14 start
        ClearDeleted(uid, component);
        //DS-14 end

        if (!component.Active ||
            component.Colliding.Count == 0)
        {
            return true;
        }

        //DS-14 start
        if (IsBlacklistBlocked(uid, component, transform))
            return false;
        //DS-14 end

        //DS-14 start
        _colliding.Clear();
        _colliding.AddRange(component.Colliding);
        foreach (var otherUid in _colliding)
        {
            if (!component.Colliding.Contains(otherUid))
                continue;

            UpdateColliding(uid, component, transform, otherUid, query);
        }
        //DS-14 end

        return false;
    }

    private void UpdateColliding(EntityUid uid, StepTriggerComponent component, TransformComponent ownerXform, EntityUid otherUid, EntityQuery<PhysicsComponent> query)
    {
        if (!query.TryGetComponent(otherUid, out var otherPhysics))
            return;

        var otherXform = Transform(otherUid);
        // TODO: This shouldn't be calculating based on world AABBs.
        var ourAabb = _entityLookup.GetAABBNoContainer(uid, ownerXform.LocalPosition, ownerXform.LocalRotation);
        var otherAabb = _entityLookup.GetAABBNoContainer(otherUid, otherXform.LocalPosition, otherXform.LocalRotation);

        if (!ourAabb.Intersects(otherAabb))
        {
            if (component.CurrentlySteppedOn.Remove(otherUid))
            {
                Dirty(uid, component);
            }
            return;
        }

        // max 'area of enclosure' between the two aabbs
        // this is hard to explain
        var intersect = Box2.Area(otherAabb.Intersect(ourAabb));
        var ratio = Math.Max(intersect / Box2.Area(otherAabb), intersect / Box2.Area(ourAabb));
        if (otherPhysics.LinearVelocity.Length() < component.RequiredTriggeredSpeed
            || component.CurrentlySteppedOn.Contains(otherUid)
            || ratio < component.IntersectRatio
            || !CanTrigger(uid, otherUid, component))
        {
            return;
        }

        if (component.StepOn)
        {
            var evStep = new StepTriggeredOnEvent(uid, otherUid);
            RaiseLocalEvent(uid, ref evStep);
        }
        else
        {
            var evStep = new StepTriggeredOffEvent(uid, otherUid);
            RaiseLocalEvent(uid, ref evStep);
        }

        //DS-14 start
        if (TerminatingOrDeleted(otherUid))
        {
            Forget(uid, component, otherUid);
            return;
        }
        //DS-14 end

        component.CurrentlySteppedOn.Add(otherUid);
        Dirty(uid, component);
    }

    //DS-14 start
    public bool IsBlacklistBlocked(EntityUid uid, StepTriggerComponent? component = null, TransformComponent? transform = null)
    {
        if (!Resolve(uid, ref component, false) || component.Blacklist == null)
            return false;

        if (!Resolve(uid, ref transform, false) || transform.GridUid is not { } gridUid)
            return false;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var position = _map.LocalToTile(gridUid, grid, transform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, position);
        while (anchored.MoveNext(out var ent))
        {
            if (ent == uid)
                continue;

            if (_whitelistSystem.IsWhitelistPass(component.Blacklist, ent.Value))
                return true;
        }

        return false;
    }
    //DS-14 end

    private bool CanTrigger(EntityUid uid, EntityUid otherUid, StepTriggerComponent component)
    {
        if (!component.Active || component.CurrentlySteppedOn.Contains(otherUid))
            return false;

        // Can't trigger if we don't ignore weightless entities
        // and the entity is flying or currently weightless
        // Makes sense simulation wise to have this be part of steptrigger directly IMO
        if (!component.IgnoreWeightless && TryComp<PhysicsComponent>(otherUid, out var physics) &&
            (physics.BodyStatus == BodyStatus.InAir || _gravity.IsWeightless(otherUid)))
            return false;

        var msg = new StepTriggerAttemptEvent { Source = uid, Tripper = otherUid };

        RaiseLocalEvent(uid, ref msg);

        return msg.Continue && !msg.Cancelled;
    }

    private void OnStartCollide(EntityUid uid, StepTriggerComponent component, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (!args.OtherFixture.Hard)
            return;

        // Temporary immunity can end while the entities remain in contact.
        // UpdateColliding checks whether a tracked contact may trigger.
        EnsureComp<StepTriggerActiveComponent>(uid);

        if (component.Colliding.Add(otherUid))
        {
            Link(uid, otherUid); //DS-14
            Dirty(uid, component);
        }
    }

    private void OnEndCollide(EntityUid uid, StepTriggerComponent component, ref EndCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (!component.Colliding.Remove(otherUid))
            return;

        Unlink(uid, otherUid); //DS-14

        var wasSteppedOn = component.CurrentlySteppedOn.Remove(otherUid);
        Dirty(uid, component);

        if (component.StepOn && wasSteppedOn)
        {
            var evStepOff = new StepTriggeredOffEvent(uid, otherUid);
            RaiseLocalEvent(uid, ref evStepOff);
        }

        if (component.Colliding.Count == 0)
        {
            RemCompDeferred<StepTriggerActiveComponent>(uid);
        }
    }

    private void TriggerHandleState(EntityUid uid, StepTriggerComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (component.Colliding.Count > 0)
        {
            EnsureComp<StepTriggerActiveComponent>(uid);
        }
        else
        {
            RemCompDeferred<StepTriggerActiveComponent>(uid);
        }
    }

    public void SetIntersectRatio(EntityUid uid, float ratio, StepTriggerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (MathHelper.CloseToPercent(component.IntersectRatio, ratio))
            return;

        component.IntersectRatio = ratio;
        Dirty(uid, component);
    }

    public void SetRequiredTriggerSpeed(EntityUid uid, float speed, StepTriggerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (MathHelper.CloseToPercent(component.RequiredTriggeredSpeed, speed))
            return;

        component.RequiredTriggeredSpeed = speed;
        Dirty(uid, component);
    }

    public void SetActive(EntityUid uid, bool active, StepTriggerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (active == component.Active)
            return;

        component.Active = active;
        if (active && component.Colliding.Count != 0)
            EnsureComp<StepTriggerActiveComponent>(uid);
        Dirty(uid, component);
    }

    //DS-14 start
    private void OnTriggerShutdown(Entity<StepTriggerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var other in ent.Comp.Colliding)
            Unlink(ent.Owner, other);

        foreach (var other in ent.Comp.CurrentlySteppedOn)
            Unlink(ent.Owner, other);
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> ent)
    {
        if (!_triggersByTripper.Remove(ent.Owner, out var triggers))
            return;

        foreach (var trigger in triggers)
        {
            if (!TryComp(trigger, out StepTriggerComponent? component))
                continue;

            var removed = component.Colliding.Remove(ent.Owner);
            var stepped = component.CurrentlySteppedOn.Remove(ent.Owner);
            if (!removed && !stepped)
                continue;

            if (component.Colliding.Count == 0)
                RemCompDeferred<StepTriggerActiveComponent>(trigger);

            Dirty(trigger, component);
        }
    }

    private void ClearDeleted(EntityUid uid, StepTriggerComponent component)
    {
        _stale.Clear();
        foreach (var other in component.Colliding)
        {
            if (TerminatingOrDeleted(other))
                _stale.Add(other);
        }

        foreach (var other in component.CurrentlySteppedOn)
        {
            if (TerminatingOrDeleted(other))
                _stale.Add(other);
        }

        foreach (var other in _stale)
            Forget(uid, component, other);
    }

    private void Forget(EntityUid uid, StepTriggerComponent component, EntityUid otherUid)
    {
        Unlink(uid, otherUid);
        var removed = component.Colliding.Remove(otherUid);
        var stepped = component.CurrentlySteppedOn.Remove(otherUid);
        if (!removed && !stepped)
            return;

        if (component.Colliding.Count == 0)
            RemCompDeferred<StepTriggerActiveComponent>(uid);

        Dirty(uid, component);
    }

    private void Link(EntityUid trigger, EntityUid tripper)
    {
        if (!_triggersByTripper.TryGetValue(tripper, out var triggers))
        {
            triggers = new HashSet<EntityUid>();
            _triggersByTripper.Add(tripper, triggers);
        }

        triggers.Add(trigger);
    }

    private void Unlink(EntityUid trigger, EntityUid tripper)
    {
        if (!_triggersByTripper.TryGetValue(tripper, out var triggers))
            return;

        triggers.Remove(trigger);
        if (triggers.Count == 0)
            _triggersByTripper.Remove(tripper);
    }
    //DS-14 end
}

[ByRefEvent]
public struct StepTriggerAttemptEvent
{
    public EntityUid Source;
    public EntityUid Tripper;
    public bool Continue;
    /// <summary>
    ///     Set by systems which wish to cancel the step trigger event, regardless of event ordering.
    /// </summary>
    public bool Cancelled;
}

/// <summary>
/// Raised when an entity stands on a steptrigger initially (assuming it has both on and off states).
/// </summary>
[ByRefEvent]
public readonly record struct StepTriggeredOnEvent(EntityUid Source, EntityUid Tripper);

/// <summary>
/// Raised when an entity leaves a steptrigger if it has on and off states OR when an entity intersects a steptrigger.
/// </summary>
[ByRefEvent]
public readonly record struct StepTriggeredOffEvent(EntityUid Source, EntityUid Tripper);
