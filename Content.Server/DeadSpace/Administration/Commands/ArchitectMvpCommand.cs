// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.DeadSpace.RoundEnd;
using Content.Server.DeadSpace.RoundEnd.Credits;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.RoundEnd;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Administration.Commands;

/// <summary>
/// Plays the Architect MVP scene (the one from the round-end credits) with a chosen player's character.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class ArchitectMvpCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IRobustRandom _random = default!;

    public string Command => "architectmvp";
    public string Description => "Play the Architect MVP scene for everyone (or only for you).";
    public string Help => "Usage: architectmvp [username] [me]\nWithout a username your own character is used. Add 'me' to watch it alone.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 2)
        {
            shell.WriteError(Help);
            return;
        }

        var onlyMe = args.Length > 0 && args[^1] == "me";
        var name = args.Length > 0 && !(args.Length == 1 && onlyMe) ? args[0] : null;

        ICommonSession? session;
        if (name != null)
        {
            if (!_players.TryGetSessionByUsername(name, out session))
            {
                shell.WriteError($"No player named {name}.");
                return;
            }
        }
        else
        {
            session = shell.Player;
        }

        if (session == null)
        {
            shell.WriteError("Give a username when running this from the server console.");
            return;
        }

        if (onlyMe && shell.Player == null)
        {
            shell.WriteError("The server console can't watch the scene.");
            return;
        }

        var minds = _entities.System<SharedMindSystem>();
        var dolls = _entities.System<RoundEndDollStateSystem>();
        var stats = _entities.System<RoundEndCreditsStatsSystem>();
        var ev = new ArchitectMvpShowcaseEvent { Name = session.Name };
        if (minds.TryGetMind(session, out var mindId, out var mind))
        {
            ev.Name = mind.CharacterName ?? session.Name;
            ev.Doll = dolls.GetDollData(mindId);
            ev.Cast.Kills = (int) stats.GetStat(mindId, RoundEndCreditsStat.Kills);
            ev.Cast.Assists = (int) stats.GetStat(mindId, RoundEndCreditsStat.Assists);
        }

        // Placeholder contenders: a Deacon for kills and a Seraphim for assists, in their own gear, with made-up numbers.
        ev.Cast.Contenders.Add(new RoundEndCreditsContender(
            Loc.GetString("architect-mvp-placeholder-deacon"),
            _random.Next(3, 9),
            _random.Next(0, 4),
            new RoundEndDollData { BodyPrototype = "MobHuman", FallbackGear = "TheCircleDeaconShowcaseGear" }));
        ev.Cast.Contenders.Add(new RoundEndCreditsContender(
            Loc.GetString("architect-mvp-placeholder-seraphim"),
            _random.Next(0, 4),
            _random.Next(3, 9),
            new RoundEndDollData { BodyPrototype = "MobHuman", FallbackGear = "TheCircleSeraphimShowcaseGear" }));

        if (onlyMe)
            _entities.EntityNetManager.SendSystemNetworkMessage(ev, shell.Player!.Channel);
        else
            _entities.EntityNetManager.SendSystemNetworkMessage(ev);

        shell.WriteLine($"Playing the Architect MVP scene with {ev.Name}.");
    }
}
