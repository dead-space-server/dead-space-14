using Content.IntegrationTests.Fixtures;
using Content.Server.Maps;
using Content.Server.DeadSpace.Maps; // DS14
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.IntegrationTests.Tests.Commands;

[TestFixture]
public sealed class ForceMapTest : GameTest
{
    private const string DefaultMapName = "Empty";
    private const string BadMapName = "asdf_asd-fa__sdfAsd_f"; // Hopefully no one ever names a map this...
    private const string TestMapEligibleName = "ForceMapTestEligible";
    private const string TestMapIneligibleName = "ForceMapTestIneligible";

    [TestPrototypes]
    private static readonly string TestMaps = @$"
- type: gameMap
  id: {TestMapIneligibleName}
  mapName: {TestMapIneligibleName}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 20
  maxPlayers: 80
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""

- type: gameMap
  id: {TestMapEligibleName}
  mapName: {TestMapEligibleName}
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
        - type: StationNameSetup
          mapNameTemplate: ""Empty""
";

    [Test]
    public async Task TestForceMapCommand()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.EntMan;
        var configManager = server.ResolveDependency<IConfigurationManager>();
        var consoleHost = server.ResolveDependency<IConsoleHost>();
        var gameMapMan = server.ResolveDependency<IGameMapManager>();

        await server.WaitAssertion(() =>
        {
            // Make sure we're set to the default map
            Assert.That(gameMapMan.GetSelectedMap()?.ID, Is.EqualTo(DefaultMapName),
                $"Test didn't start on expected map ({DefaultMapName})!");

            // Try changing to a map that doesn't exist
            consoleHost.ExecuteCommand($"forcemap {BadMapName}");
            Assert.That(gameMapMan.GetSelectedMap()?.ID, Is.EqualTo(DefaultMapName),
                $"Forcemap succeeded with a map that does not exist ({BadMapName})!");

            // Try changing to a valid map
            consoleHost.ExecuteCommand($"forcemap {TestMapEligibleName}");
            Assert.That(gameMapMan.GetSelectedMap()?.ID, Is.EqualTo(TestMapEligibleName),
                $"Forcemap failed with a valid map ({TestMapEligibleName})");

            // Try changing to a map that exists but is ineligible
            consoleHost.ExecuteCommand($"forcemap {TestMapIneligibleName}");
            Assert.That(gameMapMan.GetSelectedMap()?.ID, Is.EqualTo(TestMapIneligibleName),
                $"Forcemap failed with valid but ineligible map ({TestMapIneligibleName})!");

            // Try clearing the force-selected map
            consoleHost.ExecuteCommand("forcemap \"\"");
            Assert.That(gameMapMan.GetSelectedMap()?.ID, Is.EqualTo(DefaultMapName),
                $"Running 'forcemap \"\"' did not restore the default map selection!"); // DS14

        });

        // Cleanup
        configManager.SetCVar(CCVars.GameMap, DefaultMapName);
    }

    // DS14-start
    [Test]
    public async Task TestForceMapOverridesAutoMapVoteSelection()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true
        });
        var cleanReturned = false;

        try
        {
            var server = pair.Server;

            var configManager = server.ResolveDependency<IConfigurationManager>();
            var consoleHost = server.ResolveDependency<IConsoleHost>();
            var gameMapMan = server.ResolveDependency<IGameMapManager>();

            // Reset map selection explicitly instead of paying the CI cost of a fresh pair.
            await server.WaitPost(() =>
            {
                gameMapMan.ClearSelectedMap();
                configManager.SetCVar(CCVars.GameMap, DefaultMapName);
            });

            await server.WaitAssertion(() =>
            {
                gameMapMan.BeginAutoMapVoteOverride();

                consoleHost.ExecuteCommand($"forcemap {TestMapEligibleName}");
                Assert.That(gameMapMan.GetSelectedMap()?.ID, Is.EqualTo(TestMapEligibleName),
                    $"Forcemap did not override auto map vote state with map ({TestMapEligibleName})!");

                gameMapMan.SelectMap(TestMapIneligibleName, MapSelectionContext.AutoMapVote);
                Assert.That(gameMapMan.GetSelectedMap()?.ID, Is.EqualTo(TestMapEligibleName),
                    $"Auto map vote selection overrode forced map ({TestMapEligibleName})!");

                gameMapMan.BeginAutoMapVoteOverride();
                Assert.That(gameMapMan.GetSelectedMap(), Is.Null,
                    "Starting a new auto map vote cycle did not clear the previous forced map override.");
            });

            await server.WaitPost(() =>
            {
                gameMapMan.ClearSelectedMap();
                configManager.SetCVar(CCVars.GameMap, DefaultMapName);
            });

            await pair.CleanReturnAsync();
            cleanReturned = true;
        }
        finally
        {
            if (!cleanReturned)
                await pair.DisposeAsync();
        }
    }
    // DS14-end
}
