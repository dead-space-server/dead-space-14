// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Numerics;
using Content.Shared.DeadSpace.Abilities;
using Content.Shared.Ghost;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Abilities.Systems;

public sealed partial class ThrowingThingsSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrowingThingsActionEvent>(OnAction);
    }

    private void OnAction(ThrowingThingsActionEvent args)
    {
        if (args.Handled)
            return;

        var targetMapPos = _transform.ToMapCoordinates(args.Target);
        if (targetMapPos.MapId == MapId.Nullspace)
            return;

        var uid = args.Performer;
        var performerMapPos = _transform.GetMapCoordinates(uid);
        var targetPos = targetMapPos.Position;
        var componentTypes = ResolveComponentTypes(args.Components);

        var items = new List<(EntityUid Item, float DistanceSquared)>();

        foreach (var item in _lookup.GetEntitiesInRange(performerMapPos, args.Range))
        {
            if (item == uid)
                continue;
            if (!CanThrow(item))
                continue;
            if (!TryComp<PhysicsComponent>(item, out var physics) || physics.BodyType == BodyType.Static)
                continue;
            if (!MatchesFilter(item, args.Entities, componentTypes))
                continue;

            var itemPos = _transform.GetMapCoordinates(item).Position;
            items.Add((item, Vector2.DistanceSquared(performerMapPos.Position, itemPos)));
        }

        items.Sort(static (left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));

        var count = Math.Min(items.Count, args.HowMuch);

        for (var i = 0; i < count; i++)
        {
            var item = items[i].Item;
            var itemPos = _transform.GetMapCoordinates(item).Position;

            var diff = targetPos - itemPos;
            if (diff.LengthSquared() < 0.0001f)
                diff = Vector2.UnitY;

            if (HasComp<ProjectileComponent>(item))
                LaunchProjectile(item, diff, args.ThrowStrength, uid);
            else
                _throwing.TryThrow(item, diff, args.ThrowStrength, uid, recoil: false, compensateFriction: false);
        }

        args.Handled = true;
    }

    private List<Type> ResolveComponentTypes(List<string> componentNames)
    {
        var types = new List<Type>(componentNames.Count);
        foreach (var name in componentNames)
        {
            if (_componentFactory.TryGetRegistration(name, out var registration))
                types.Add(registration.Type);
            else
                Log.Warning($"ThrowingThingsSystem: неизвестный компонент '{name}', пропускаем.");
        }
        return types;
    }

    private bool MatchesFilter(EntityUid item, List<EntProtoId> entities, List<Type> componentTypes)
    {
        if (entities.Count == 0 && componentTypes.Count == 0)
            return false;

        if (entities.Count > 0)
        {
            var prototype = MetaData(item).EntityPrototype;
            if (prototype != null && entities.Contains(prototype.ID))
                return true;
        }

        foreach (var type in componentTypes)
        {
            if (HasComp(item, type))
                return true;
        }

        return false;
    }

    private void LaunchProjectile(EntityUid item, Vector2 direction, float strength, EntityUid shooter)
    {
        if (!TryComp<PhysicsComponent>(item, out var physics))
            return;

        _physics.SetBodyType(item, BodyType.Dynamic, body: physics);
        _physics.SetLinearVelocity(item, Vector2.Normalize(direction) * strength, body: physics);

        if (TryComp<ProjectileComponent>(item, out var projectile))
        {
            projectile.Shooter = shooter;
            projectile.Weapon = shooter;
        }
    }

    private bool CanThrow(EntityUid uid)
    {
        return !(
            HasComp<GhostComponent>(uid) ||
            HasComp<MapGridComponent>(uid) ||
            HasComp<MapComponent>(uid)
        );
    }
}