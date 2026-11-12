using Content.Shared.Power.EntitySystems;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Physics; // DS14
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraSystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private EntityQuery<CameraActiveOnCollideComponent> _cameraQuery = default!;
    // DS14-start
    [Dependency] private EntityQuery<CameraActiveOnCollideColliderComponent> _colliderQuery = default!;
    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery = default!;
    // DS14-end

    public void InitializeCollide()
    {
        SubscribeLocalEvent<CameraActiveOnCollideComponent, PreventCollideEvent>(OnCameraPreventCollide); // DS14
        SubscribeLocalEvent<CameraActiveOnCollideColliderComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CameraActiveOnCollideColliderComponent, StartCollideEvent>(OnStart);
        SubscribeLocalEvent<CameraActiveOnCollideColliderComponent, EndCollideEvent>(OnEnd);

        SubscribeLocalEvent<CameraActiveOnCollideColliderComponent, ComponentShutdown>(OnCollideShutdown);
        SubscribeLocalEvent<CameraActiveOnCollideComponent, SurveillanceCameraGetIsViewedExternallyEvent>(OnOverrideState);
    }

    private void OnCollideShutdown(Entity<CameraActiveOnCollideColliderComponent> ent, ref ComponentShutdown args)
    {
        // TODO: Check this on the event.
        if (TerminatingOrDeleted(ent.Owner))
            return;

        // Regenerate contacts for everything we were colliding with.
        var contacts = _physics.GetContacts(ent.Owner);

        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching)
                continue;

            var other = contact.OtherEnt(ent.Owner);

            if (_cameraQuery.HasComp(other))
            {
                _physics.RegenerateContacts(other);
            }
        }
    }

    // DS14-start
    private void OnCameraPreventCollide(Entity<CameraActiveOnCollideComponent> ent, ref PreventCollideEvent args)
    {
        if (!_fixturesQuery.TryGetComponent(ent, out var fixtures) ||
            !fixtures.Fixtures.TryGetValue(ent.Comp.FixtureId, out var cameraFixture) ||
            args.OurFixture != cameraFixture)
        {
            return;
        }

        if (!_colliderQuery.HasComp(args.OtherEntity))
            args.Cancelled = true;
    }
    // DS14-end

    // You may be wondering what de fok this is doing here.
    // At the moment there's no easy way to do collision whitelists based on components.
    private void OnPreventCollide(Entity<CameraActiveOnCollideColliderComponent> ent, ref PreventCollideEvent args)
    {
        if (!_cameraQuery.HasComp(args.OtherEntity))
        {
            args.Cancelled = true;
        }
    }

    private void OnEnd(Entity<CameraActiveOnCollideColliderComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (!_cameraQuery.TryComp(args.OtherEntity, out var cameraCollider))
            return;

        // TODO: Engine bug IsTouching box2d yay.
        var contacts = _physics.GetTouchingContacts(args.OtherEntity) - 1;

        if (contacts > 0)
            return;

        cameraCollider.Enabled = false;
        Dirty(args.OtherEntity, cameraCollider);
        UpdateVisuals(args.OtherEntity);
    }

    private void OnStart(Entity<CameraActiveOnCollideColliderComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (!_cameraQuery.TryComp(args.OtherEntity, out var cameraCollider))
            return;

        cameraCollider.Enabled = true;
        Dirty(args.OtherEntity, cameraCollider);
        UpdateVisuals(args.OtherEntity);
    }

    private void OnOverrideState(Entity<CameraActiveOnCollideComponent> ent, ref SurveillanceCameraGetIsViewedExternallyEvent args)
    {
        if (ent.Comp.RequiresPower && !_power.IsPowered(ent.Owner))
            return;

        if (!ent.Comp.Enabled)
            return;

        args.Viewed = true;
    }
}

