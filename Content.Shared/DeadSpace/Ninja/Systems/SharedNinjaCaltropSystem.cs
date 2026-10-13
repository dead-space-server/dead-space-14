using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Content.Shared.Emp;
using Content.Shared.Actions;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System.Numerics;
using Content.Shared.Ninja.Systems;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class SharedNinjaCaltropSystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SharedNinjaSmokeAbilitySystem _smoke = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaCaltropComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaCaltropComponent, GetItemActionsEvent>(OnGetActions);

        SubscribeLocalEvent<NinjaCaltropComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);

        SubscribeLocalEvent<NinjaCaltropComponent, NinjaCaltropAbilityActionEvent>(OnCaltropAbility);
    }

    private void OnMapInit(Entity<NinjaCaltropComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.ActionEntity, comp.Action);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaCaltropComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaCaltropComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.ActionEntity);
        }
    }

    private void OnCaltropAbility(Entity<NinjaCaltropComponent> ent, ref NinjaCaltropAbilityActionEvent args)
    {
        var xform = Transform(args.Performer);
        var spawnpositions = GetCaltropSpawnPositions(xform.Coordinates, xform.WorldRotation);

        if (spawnpositions.Count == 0)
            return;

        if (_net.IsServer)
        {
            if (!_ninja.TryUseCharge(args.Performer, ent.Comp.Charge))
            {
                _popup.PopupClient(Loc.GetString("ninja-no-power"), args.Performer, args.Performer);
                return;
            }
        }
        else if (!_ninja.HasCharge(args.Performer, ent.Comp.Charge))
        {
            _popup.PopupClient(Loc.GetString("ninja-no-power"), args.Performer, args.Performer);
            return;
        }

        args.Handled = true;

        foreach (var position in spawnpositions)
        {
            PredictedSpawnAtPosition(ent.Comp.CaltropProto, position);
        }

        if (TryComp<NinjaSmokeAbilityComponent>(ent, out var smokeComp))
            _smoke.TrySpawnNinjaSmoke((ent.Owner, smokeComp), true);

    }

    private List<EntityCoordinates> GetCaltropSpawnPositions(EntityCoordinates position, Angle angle)
    {
        var backwards = -angle.ToWorldVec();
        var side = new Vector2(-backwards.Y, backwards.X);
        var spawnPositions = new List<EntityCoordinates>();
        var physicsQuery = GetEntityQuery<PhysicsComponent>();

        for (var i = -1; i <= 1; i++)
        {
            var tilePosition = position.Offset(backwards * 1.5f + side * i).SnapToGrid();
            if (!IsTileFree(tilePosition, physicsQuery))
                continue;

            spawnPositions.Add(tilePosition);
        }

        return spawnPositions;
    }

    private bool IsTileFree(EntityCoordinates coords, EntityQuery<PhysicsComponent> physicsQuery)
    {
        if (!_turf.TryGetTileRef(coords, out var tileRef))
            return false;

        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInTile(tileRef.Value, entities);

        foreach (var ent in entities)
        {
            if (physicsQuery.TryGetComponent(ent, out var physics) &&
                physics.BodyType == BodyType.Static &&
                physics.Hard &&
                (physics.CollisionLayer & (int)CollisionGroup.Impassable) != 0)
            {
                return false;
            }
        }

        return true;
    }
}