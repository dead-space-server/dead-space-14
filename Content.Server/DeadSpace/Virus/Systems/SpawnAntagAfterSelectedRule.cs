using Content.Server.Antag;
using Content.Server.DeadSpace.Virus.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Virus.Systems;

public sealed partial class SpawnAntagAfterSelectedRule : GameRuleSystem<SpawnAntagAfterSelectedComponent>
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private GhostRoleSystem _ghost = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnAntagAfterSelectedComponent, AfterAntagEntitySelectedEvent>(AfterAntagEntitySelected);
    }

    private void AfterAntagEntitySelected(Entity<SpawnAntagAfterSelectedComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
            return;

        if (!_player.TryGetSessionById(mind.UserId, out var session))
            return;

        var xform = Transform(args.EntityUid);
        var antag = Spawn(ent.Comp.Prototype,
            _transform.GetMapCoordinates(args.EntityUid, xform),
            rotation: _transform.GetWorldRotation(xform));

        if (TryComp<GhostRoleComponent>(antag, out var ghostRoleComponent))
            _ghost.Takeover(session, ghostRoleComponent.Identifier);
        else
            _mind.ControlMob(args.EntityUid, antag);

        QueueDel(args.EntityUid);
    }

}
