//Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.Ghost.Roles.Components;
using Content.Shared.DeadSpace.GhostRoleNotify.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Ghost;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
using Content.Server.Ghost.Roles;
using Robust.Server.Player;
using Content.Shared.DeadSpace.Ghost.SharedGhostPing;

namespace Content.Server.DeadSpace.GhostRoleNotify.GhostRoleNotifySystem;

public sealed partial class GhostRoleNotifySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
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
                if (player.AttachedEntity != null && player.AttachedEntity.Value.IsValid() && _entityManager.HasComponent<GhostComponent>(player.AttachedEntity))
                {
                    RaiseNetworkEvent(new PingMessage(component.GroupPrototype), player);
                }
            }

        }
    }

}