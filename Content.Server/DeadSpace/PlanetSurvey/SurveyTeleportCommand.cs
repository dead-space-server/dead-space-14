// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.PlanetSurvey;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class SurveyTeleportCommand : LocalizedEntityCommands
{
    private static readonly string[] Places = { "landing", "town", "far" };

    [Dependency] private PlanetSurveySystem _survey = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public override string Command => "surveytp";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } user)
        {
            shell.WriteError(Loc.GetString("cmd-player-only"));
            return;
        }

        if (_survey.Planet is not { } planet ||
            !EntityManager.TryGetComponent<SurveyDirectorComponent>(planet, out var director) ||
            !director.Ready)
        {
            shell.WriteError(Loc.GetString("cmd-surveytp-none"));
            return;
        }

        var place = args.Length > 0 ? args[0] : "landing";
        var target = place switch
        {
            "town" => director.Edge,
            "far" => director.Far + new Vector2(0f, 2f),
            _ => director.Landing + new Vector2(0f, 2f),
        };

        _xform.SetCoordinates(user, new EntityCoordinates(planet, target));
        _xform.AttachToGridOrMap(user);
        shell.WriteLine(Loc.GetString("cmd-surveytp-done", ("planet", EntityManager.ToPrettyString(planet))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1 ? CompletionResult.FromOptions(Places) : CompletionResult.Empty;
    }
}
