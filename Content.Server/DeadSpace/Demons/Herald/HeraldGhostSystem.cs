// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Herald.Components;
using Content.Shared.Maps;
using Content.Shared.DeadSpace.Demons.Herald;
using Robust.Server.GameObjects;
using Content.Shared.Popups;
using Content.Shared.Physics;
using Content.Shared.Mind;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Demons.Herald;

public sealed partial class HeraldGhostSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private PhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private GhostRoleSystem _ghost = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeraldGhostComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<HeraldGhostComponent, HeraldSpawnActionEvent>(DoSpawn);
    }

    private void OnComponentInit(EntityUid uid, HeraldGhostComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionHeraldSpawnEntity, component.ActionHeraldSpawn, uid);
    }

    private void DoSpawn(EntityUid uid, HeraldGhostComponent component, HeraldSpawnActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);
        var spawnCoordinates = _transform.GetMapCoordinates(uid, xform);
        var spawnRotation = _transform.GetWorldRotation(xform);
        var tileref = _turf.GetTileRef(xform.Coordinates);
        if (tileref != null)
        {
            if (_physics.GetEntitiesIntersectingBody(uid, (int)CollisionGroup.Impassable).Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("revenant-in-solid"), uid, uid);
                return;
            }
        }

        args.Handled = true;

        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;

        var ent = Spawn(component.HeraldMobSpawnId, spawnCoordinates, rotation: spawnRotation);

        if (!TryComp<GhostRoleComponent>(ent, out var ghostRoleComponent))
        {
            _mindSystem.TransferTo(mindId, ent);
            Spawn(component.DemonPortalSpawnId, spawnCoordinates, rotation: spawnRotation);
            QueueDel(uid);

            return;
        }

        var id = ghostRoleComponent.Identifier;
        if (!_player.TryGetSessionById(mind.UserId, out var session))
            return;

        if (session != null)
        {
            _ghost.Takeover(session, id);
        }
        else
        {
            return;
        }

        Spawn(component.DemonPortalSpawnId, spawnCoordinates, rotation: spawnRotation);

        QueueDel(uid);
    }
}
