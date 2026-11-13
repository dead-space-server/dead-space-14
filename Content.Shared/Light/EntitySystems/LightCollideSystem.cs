using Content.Shared.Light.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Light.EntitySystems;

public sealed partial class LightCollideSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SlimPoweredLightSystem _lights = default!;

    private EntityQuery<LightOnCollideComponent> _lightQuery;
    private EntityQuery<LightOnCollideColliderComponent> _colliderQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;

    public override void Initialize()
    {
        base.Initialize();

        _lightQuery = GetEntityQuery<LightOnCollideComponent>();
        _colliderQuery = GetEntityQuery<LightOnCollideColliderComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();

        SubscribeLocalEvent<LightOnCollideComponent, PreventCollideEvent>(OnLightPreventCollide);
        SubscribeLocalEvent<LightOnCollideColliderComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<LightOnCollideColliderComponent, StartCollideEvent>(OnStart);
        SubscribeLocalEvent<LightOnCollideColliderComponent, EndCollideEvent>(OnEnd);

        SubscribeLocalEvent<LightOnCollideColliderComponent, ComponentShutdown>(OnCollideShutdown);
    }

    private void OnCollideShutdown(Entity<LightOnCollideColliderComponent> ent, ref ComponentShutdown args)
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

            if (_lightQuery.HasComp(other))
            {
                _physics.RegenerateContacts(other);
            }
        }
    }

    private void OnLightPreventCollide(Entity<LightOnCollideComponent> ent, ref PreventCollideEvent args)
    {
        if (!_fixturesQuery.TryGetComponent(ent, out var fixtures) ||
            !fixtures.Fixtures.TryGetValue(ent.Comp.FixtureId, out var lightFixture) ||
            args.OurFixture != lightFixture)
        {
            return;
        }

        if (!_colliderQuery.HasComp(args.OtherEntity))
            args.Cancelled = true;
    }

    // You may be wondering what de fok this is doing here.
    // At the moment there's no easy way to do collision whitelists based on components.
    private void OnPreventCollide(Entity<LightOnCollideColliderComponent> ent, ref PreventCollideEvent args)
    {
        if (!_lightQuery.HasComp(args.OtherEntity))
        {
            args.Cancelled = true;
        }
    }

    private void OnEnd(Entity<LightOnCollideColliderComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (!_lightQuery.HasComp(args.OtherEntity))
            return;

        // TODO: Engine bug IsTouching box2d yay.
        var contacts = _physics.GetTouchingContacts(args.OtherEntity) - 1;

        if (contacts > 0)
            return;

        _lights.SetEnabled(args.OtherEntity, false);
    }

    private void OnStart(Entity<LightOnCollideColliderComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (!_lightQuery.HasComp(args.OtherEntity))
            return;

        _lights.SetEnabled(args.OtherEntity, true);
    }
}
