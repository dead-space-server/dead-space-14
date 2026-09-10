using Content.Server.Body.Components;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Body.Components;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DeadSpace.CCCCVars;
using Robust.Shared.Configuration;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class AshWalkerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoles = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshWalkerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AshWalkerEggComponent, MapInitEvent>(OnEggMapInit);
        SubscribeLocalEvent<AshWalkerTribeMemberComponent, GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
        Subs.CVar(_cfg, CCCCVars.AshWalkersEnabled, _ => _ghostRoles.UpdateAllEui());
    }

    private void OnInit(Entity<AshWalkerComponent> ent, ref ComponentInit args)
    {
        RemComp<RespiratorComponent>(ent);
        RemComp<InternalsComponent>(ent);
    }

    private void OnEggMapInit(Entity<AshWalkerEggComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.HomeMap = Transform(ent).MapUid;
    }

    private void OnSpawnerUsed(Entity<AshWalkerTribeMemberComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (TryComp<AshWalkerEggComponent>(args.Spawner, out var egg))
        {
            ent.Comp.HomeMap = egg.HomeMap;
            Dirty(ent);
        }
    }
}
