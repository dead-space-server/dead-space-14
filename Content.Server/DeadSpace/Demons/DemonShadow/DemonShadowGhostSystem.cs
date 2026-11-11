// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.DemonShadow;
using Content.Shared.DeadSpace.Demons.DemonShadow.Components;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Demons.DemonShadow;

public sealed class DemonShadowGhostSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly GhostRoleSystem _ghost = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DemonShadowGhostComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DemonShadowGhostComponent, ShadowSpawnActionEvent>(DoSpawn);
    }

    private void OnComponentInit(EntityUid uid, DemonShadowGhostComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionShadowSpawnEntity, component.ActionShadowSpawn, uid);
    }

    private void DoSpawn(EntityUid uid, DemonShadowGhostComponent component, ShadowSpawnActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);
        var spawnCoordinates = _transform.GetMapCoordinates(uid, xform);
        var spawnRotation = _transform.GetWorldRotation(xform);
        var tileref = _turf.GetTileRef(xform.Coordinates);
        if (tileref != null)
        {
            if (_physics.GetEntitiesIntersectingBody(uid, (int) CollisionGroup.Impassable).Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("revenant-in-solid"), uid, uid);
                return;
            }
        }

        args.Handled = true;

        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;

        var ent = Spawn(component.ShadowMobSpawnId, spawnCoordinates, rotation: spawnRotation);

        if (!TryComp<GhostRoleComponent>(ent, out var ghostRoleComponent))
        {
            _mindSystem.TransferTo(mindId, ent);
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

        QueueDel(uid);
    }
}
