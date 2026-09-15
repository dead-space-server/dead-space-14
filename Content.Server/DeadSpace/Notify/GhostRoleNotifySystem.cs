//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.Ghost.Roles.Components;
using Content.Shared.DeadSpace.Notify.Components;
using Content.Shared.Ghost;
using Content.Shared.DeadSpace.Notify.Prototypes;
using Content.Server.Ghost.Roles;
using Content.Shared.DeadSpace.Arena;
using Robust.Server.Player;
using Content.Shared.DeadSpace.Notify;

namespace Content.Server.DeadSpace.Notify;

public sealed partial class GhostRoleNotifySystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostRoleNotifysComponent, ComponentStartup>(OnInit, after: new[] { typeof(GhostRoleSystem) });
    }

    private void OnInit(EntityUid uid, GhostRoleNotifysComponent component, ref ComponentStartup args)
    {
        if (TryComp<GhostRoleComponent>(uid, out var ghostRole))
        {
            foreach (var player in _playerManager.Sessions)
            {
                if (player.AttachedEntity is not { } attached || !attached.IsValid())
                    continue;

                // Пинг играет и для призраков, и для игроков, находящихся на арене.
                if (_entityManager.HasComponent<GhostComponent>(attached) ||
                    _entityManager.HasComponent<ArenaPlayerComponent>(attached))
                {
                    RaiseNetworkEvent(new PingMessage(component.GroupPrototype), player);
                }
            }

        }
    }

}