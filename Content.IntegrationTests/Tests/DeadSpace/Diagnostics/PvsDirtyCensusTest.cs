// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Administration;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.IntegrationTests.Tests.DeadSpace.Diagnostics;

[TestFixture]
[NonParallelizable]
public sealed class PvsDirtyCensusTest
{
    private static readonly string[] Maps =
    [
        "Box",
        "DsTram2",
    ];

    [Explicit]
    [Test, TestCaseSource(nameof(Maps))]
    public async Task IdleStationCensus(string map)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var ticker = server.System<GameTicker>();
        var census = server.System<PvsDirtySystem>();

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.GameDummyTicker, false);
            cfg.SetCVar(CCVars.GameMap, map);
            ticker.RestartRound();
        });

        await PoolManager.WaitUntil(server, () => ticker.RunLevel == GameRunLevel.InRound, maxTicks: 3600, tickStep: 10);
        await server.WaitRunTicks(600);

        await server.WaitPost(() =>
        {
            var shell = new ConsoleShell(server.ResolveDependency<IConsoleHost>(), null, true);
            Assert.That(census.Start(shell, 10, out var error), error);
        });

        await PoolManager.WaitUntil(server, () => !census.Running, maxTicks: 3600, tickStep: 10);
        Assert.That(census.LastReport, Is.Not.Null);
        Assert.That(census.LastReport, Does.Contain("components:"));

        await pair.CleanReturnAsync();
    }
}
