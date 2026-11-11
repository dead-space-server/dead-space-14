using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.DeadSpace.Abilities.Evolution.Components;
using Content.Shared.DeadSpace.Abilities.Evolution;
using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.Zombies;
using Content.Shared.DeadSpace.EntityPanel;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Abilities.Evolution;

public sealed class EvolutionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GhostRoleSystem _ghost = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    /// <summary>
    /// Those the server has just shown the menu to, through their own action: only they may pick, and only once.
    /// </summary>
    private readonly HashSet<EntityUid> _awaitingSelection = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvolutionComponent, ReadyEvolutionEvent>(OnReadyEvolution);
        SubscribeLocalEvent<EvolutionComponent, EvolutionDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<EvolutionComponent, EvolutionActionEvent>(OnSelectEntityAction);
        SubscribeLocalEvent<EvolutionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EvolutionComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<EvolutionComponent, EntityZombifiedEvent>(OnZombification);
        SubscribeLocalEvent<EvolutionComponent, InfectionNecroficationEvent>(OnNecrofication);

        SubscribeNetworkEvent<SelectEntityEvent>(OnSelectEntity);
    }

    private void OnReadyEvolution(EntityUid uid, EvolutionComponent component, ReadyEvolutionEvent args)
    {
        _actionsSystem.AddAction(uid, ref component.EvolutionActionEntity, component.EvolutionAction, uid);

        if (!component.CreateGhostRole)
            return;

        if (!HasComp<GhostRoleComponent>(uid))
        {
            var ghostRole = AddComp<GhostRoleComponent>(uid);

            ghostRole.RoleName = component.GhostRoleName;
            ghostRole.RoleDescription = component.GhostRoleDesk;

            if (!HasComp<GhostTakeoverAvailableComponent>(uid))
                AddComp<GhostTakeoverAvailableComponent>(uid);
        }
    }

    private void OnShutdown(EntityUid uid, EvolutionComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.EvolutionActionEntity);
    }

    private void OnZombification(EntityUid uid, EvolutionComponent component, EntityZombifiedEvent args)
    {
        _actionsSystem.RemoveAction(uid, component.EvolutionActionEntity);
    }

    private void OnNecrofication(EntityUid uid, EvolutionComponent component, InfectionNecroficationEvent args)
    {
        _actionsSystem.RemoveAction(uid, component.EvolutionActionEntity);
    }

    private void OnExamine(EntityUid uid, EvolutionComponent component, ExaminedEvent args)
    {
        var time = _timing.CurTime - component.TimeUntilEvolution;
        double seconds = Math.Abs(time.TotalSeconds);
        int roundedSeconds = (int)Math.Round(seconds);

        if (args.Examiner == args.Examined)
        {
            if (time.TotalSeconds < 0)
            {
                args.PushMarkup(Loc.GetString($"Вы будете готовы к эволюции через [color=green]{roundedSeconds} секунд[/color]."));
            }
            else
            {
                args.PushMarkup(Loc.GetString($"Вы готовы эвалюционировать."));
            }
        }
    }

    private void OnSelectEntityAction(EntityUid uid, EvolutionComponent component, EvolutionActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ActorComponent>(uid, out var actorComponent))
        {
            var ev = new RequestEntityMenuEvent(uid.Id, true, false);

            foreach (var entityUid in component.SpawnedEntities)
            {
                ev.Prototypes.Add(entityUid);
            }
            ev.Prototypes.Sort();
            RaiseNetworkEvent(ev, actorComponent.PlayerSession);
            _awaitingSelection.Add(uid);
        }

        args.Handled = true;
    }

    private void OnSelectEntity(SelectEntityEvent msg, EntitySessionEventArgs args)
    {
        if (!msg.IsUseEvolutionSystem)
            return;

        // The client names neither whom nor what freely: its own body, the menu it was shown, one of its options.
        if (args.SenderSession.AttachedEntity is not { Valid: true } uid ||
            uid.Id != msg.Target ||
            !TryComp<EvolutionComponent>(uid, out var evolutionComponent) ||
            !_awaitingSelection.Remove(uid) ||
            !evolutionComponent.SpawnedEntities.Contains(new EntProtoId(msg.PrototypeId)))
        {
            return;
        }

        evolutionComponent.SelectEntity = msg.PrototypeId;
        OnEvolutionAction(uid, evolutionComponent);
    }

    private void OnEvolutionAction(EntityUid uid, EvolutionComponent comp)
    {
        BeginSpawn(uid, comp);
    }

    private void BeginSpawn(EntityUid uid, EvolutionComponent component)
    {
        var searchDoAfter = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(5f), new EvolutionDoAfterEvent(), uid)
        {
            DistanceThreshold = 3,
            BreakOnMove = true
        };

        if (!_doAfter.TryStartDoAfter(searchDoAfter))
            return;
    }

    private void OnDoAfter(EntityUid uid, EvolutionComponent component, EvolutionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var xform = Transform(uid);

        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            return;
        }

        var ent = Spawn(component.SelectEntity,
            _transform.GetMapCoordinates(uid, xform),
            rotation: _transform.GetWorldRotation(xform));

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
