using System.Linq;
using Content.Server.Administration;
using Content.Server.Maps;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
// DS14-start
using Content.Server.DeadSpace.Maps;
using Content.Server.DeadSpace.Voting;
using Robust.Shared.GameObjects;
// DS14-end

namespace Content.Server.GameTicking.Commands
{
    [AdminCommand(AdminFlags.Round)]
    public sealed partial class ForceMapCommand : LocalizedCommands
    {
        [Dependency] private IEntityManager _entityManager = default!; // DS14
        [Dependency] private IGameMapManager _gameMapManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;

        public override string Command => "forcemap";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteLine(Loc.GetString("shell-need-exactly-one-argument"));
                return;
            }

            var name = args[0];

            // An empty string clears the forced map
            if (!string.IsNullOrEmpty(name) && !_gameMapManager.CheckMapExists(name))
            {
                shell.WriteLine(Loc.GetString("cmd-forcemap-map-not-found", ("map", name)));
                return;
            }

            // DS14-start
            if (string.IsNullOrEmpty(name))
            {
                _gameMapManager.ClearForcedMap();
                _entityManager.System<AutoMapVoteSystem>().OnForcedMapCleared();
                shell.WriteLine(Loc.GetString("cmd-forcemap-cleared"));
            }
            else
            {
                _gameMapManager.SelectMap(name, MapSelectionContext.Forced);
                _entityManager.System<AutoMapVoteSystem>().OnForcedMapSelected();
                shell.WriteLine(Loc.GetString("cmd-forcemap-success", ("map", name)));
            }
            // DS14-end
        }

        public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                var options = _prototypeManager
                    .EnumeratePrototypes<GameMapPrototype>()
                    .Select(p => new CompletionOption(p.ID, p.MapName))
                    .OrderBy(p => p.Value);

                return CompletionResult.FromHintOptions(options, Loc.GetString($"cmd-forcemap-hint"));
            }

            return CompletionResult.Empty;
        }
    }
}
