using Content.Server.Ghost.Roles.Components;
using Content.Shared.DeadSpace.GhostRoleNotify.Components;
using Robust.Shared.Prototypes;
using Content.Shared.DeadSpace.GhostRoleNotify.Prototypes;
using Content.Server.Ghost.Roles;

namespace Content.Server.DeadSpace.GhostRoleNotify.GhostRoleNotifySystem;

public sealed partial class GhostRoleNotifySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    void Lolol()
    {
        //  _prototypes.EnumeratePrototypes<GhostRoleGroupNotify>()
        foreach (var proto in _prototypes.EnumeratePrototypes<GhostRoleGroupNotify>())
        {
            Console.WriteLine(proto.Name);
        }
    }
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostRoleNotifyComponent, ComponentStartup>(OnInit, after: new[] { typeof(GhostRoleSystem) });
    }

    private void OnInit(EntityUid uid, GhostRoleNotifyComponent component, ref ComponentStartup args)
    {
        Lolol();
        if (!TryComp<GhostRoleComponent>(uid, out var ghostRole))
        {
            Console.WriteLine("netu");
        }
        else
        {
            Console.WriteLine(ghostRole.RoleName);

        }
    }

}