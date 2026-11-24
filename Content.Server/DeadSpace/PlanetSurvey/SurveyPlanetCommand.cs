// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.PlanetSurvey;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class SurveyPlanetCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PlanetSurveySystem _survey = default!;
    [Dependency] private StationSystem _station = default!;

    public override string Command => "surveyplanet";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        ProtoId<SurveyWorldPrototype>? world = null;
        SurveyMotive? motive = null;

        if (args.Length > 0 && args[0] != "*")
        {
            if (!_proto.HasIndex<SurveyWorldPrototype>(args[0]))
            {
                shell.WriteError(Loc.GetString("cmd-surveyplanet-bad-world"));
                return;
            }

            world = args[0];
        }

        if (args.Length > 1 && args[1] != "*")
        {
            if (!Enum.TryParse<SurveyMotive>(args[1], true, out var parsed))
            {
                shell.WriteError(Loc.GetString("cmd-surveyplanet-bad-motive"));
                return;
            }

            motive = parsed;
        }

        if (args.Length > 2 && bool.TryParse(args[2], out var replace) && replace && !_survey.TryDiscard(out var people))
        {
            shell.WriteError(Loc.GetString("cmd-surveyplanet-occupied", ("count", people)));
            return;
        }

        var station = shell.Player?.AttachedEntity is { } user ? _station.GetOwningStation(user) : null;
        station ??= _station.GetStations().FirstOrDefault();

        _ = Run(shell, station, world, motive);
    }

    private async Task Run(IConsoleShell shell, EntityUid? station, ProtoId<SurveyWorldPrototype>? world, SurveyMotive? motive)
    {
        var planet = await _survey.Generate(station, world, motive);
        shell.WriteLine(planet is { } map
            ? Loc.GetString("cmd-surveyplanet-done", ("planet", EntityManager.ToPrettyString(map)))
            : Loc.GetString("cmd-surveyplanet-failed"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(_proto.EnumeratePrototypes<SurveyWorldPrototype>().Select(p => p.ID).Prepend("*")),
            2 => CompletionResult.FromOptions(Enum.GetNames<SurveyMotive>().Prepend("*")),
            3 => CompletionResult.FromOptions(new[] { "false", "true" }),
            _ => CompletionResult.Empty,
        };
    }
}
