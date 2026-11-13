using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class AGhostCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    public override string Command => "aghost";
    public override string Help => "aghost";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var names = _playerManager.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return CompletionResult.FromHintOptions(names, LocalizationManager.GetString("shell-argument-username-optional-hint"));
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(LocalizationManager.GetString("shell-wrong-arguments-number"));
            return;
        }

        var player = shell.Player;
        var self = player != null;

        if (player == null)
        {
            // If you are not a player, you require a player argument.
            if (args.Length == 0)
            {
                shell.WriteError(LocalizationManager.GetString("shell-need-exactly-one-argument"));
                return;
            }

            var didFind = _playerManager.TryGetSessionByUsername(args[0], out player);
            if (!didFind)
            {
                shell.WriteError(LocalizationManager.GetString("shell-target-player-does-not-exist"));
                return;
            }
        }

        // If you are a player and a username is provided, a lookup is done to find the target player.
        if (args.Length == 1)
        {
            var didFind = _playerManager.TryGetSessionByUsername(args[0], out player);
            if (!didFind)
            {
                shell.WriteError(LocalizationManager.GetString("shell-target-player-does-not-exist"));
                return;
            }
        }

        var mindSystem = _entities.System<MindSystem>(); // DS14
        var metaDataSystem = _entities.System<MetaDataSystem>();
        var ghostSystem = _entities.System<GhostSystem>(); // DS14
        var transformSystem = _entities.System<TransformSystem>();
        var gameTicker = _entities.System<GameTicker>();

        var targetPlayer = player!;

        if (!mindSystem.TryGetMind(targetPlayer, out var mindId, out var mind)) // DS14
        {
            shell.WriteError(self ? LocalizationManager.GetString("aghost-no-mind-self") : LocalizationManager.GetString("aghost-no-mind-other")); // DS14
            return;
        }

        // DS14-start
        if (mind.VisitingEntity is { Valid: true } visiting)
        {
            if (_entities.TryGetComponent<GhostComponent>(visiting, out var oldGhostComponent))
            {
                mindSystem.UnVisit(mindId, mind);

                if (oldGhostComponent.CanGhostInteract)
                    return;
            }
            else
                mindSystem.UnVisit(mindId, mind);
        }
        // DS14-end

        var canReturn = mind.CurrentEntity != null && !_entities.HasComponent<GhostComponent>(mind.CurrentEntity.Value); // DS14
        var coordinates = GetAdminObserverCoordinates(targetPlayer, gameTicker); // DS14

        var ghost = _entities.SpawnEntity(GameTicker.AdminObserverPrototypeName, coordinates);
        transformSystem.AttachToGridOrMap(ghost, _entities.GetComponent<TransformComponent>(ghost));

        if (canReturn)
        {
            // TODO: Remove duplication between all this and "GamePreset.OnGhostAttempt()"...
            if (!string.IsNullOrWhiteSpace(mind.CharacterName))
                metaDataSystem.SetEntityName(ghost, mind.CharacterName);
            else if (!string.IsNullOrWhiteSpace(targetPlayer.Name))
                metaDataSystem.SetEntityName(ghost, targetPlayer.Name);

            mindSystem.Visit(mindId, ghost, mind);
        }
        else
        {
            metaDataSystem.SetEntityName(ghost, targetPlayer.Name);
            mindSystem.TransferTo(mindId, ghost, mind: mind);
        }

        var comp = _entities.GetComponent<GhostComponent>(ghost);
        ghostSystem.SetCanReturnToBody((ghost, comp), canReturn);
    }

    // DS14-start
    private EntityCoordinates GetAdminObserverCoordinates(ICommonSession player, GameTicker gameTicker)
    {
        if (player.AttachedEntity is not { } attached ||
            !_entities.TryGetComponent(attached, out TransformComponent? xform) ||
            xform.MapID == MapId.Nullspace ||
            xform.MapUid is not { } mapUid ||
            _entities.Deleted(mapUid) ||
            !xform.Coordinates.IsValid(_entities))
        {
            return gameTicker.GetObserverSpawnPoint();
        }

        return xform.Coordinates;
    }
    // DS14-end
}
