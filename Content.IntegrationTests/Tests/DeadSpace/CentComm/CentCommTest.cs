// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.Atmos.Components;
using Content.Server.Communications;
using Content.Server.DeadSpace.CentComm;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.Communications;
using Content.Shared.GameTicking.Components;
using Content.Shared.Parallax;
using Content.Shared.Prototypes;
using Content.Shared.Station.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.CentComm;

[TestFixture]
public sealed class CentCommTest
{
    [Test]
    public async Task AnnouncementsReachDistantSpaceOnlyOnTheTargetMap()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Fresh = true });
        var server = pair.Server;
        var em = server.EntMan;
        var targetMap = await pair.CreateTestMap();
        var otherMap = await pair.CreateTestMap();
        var listeners = await server.AddDummySessions(2);

        await server.WaitAssertion(() =>
        {
            var station = em.SpawnEntity("TestStation", MapCoordinates.Nullspace);
            server.System<StationSystem>().AddGridToStation(station, targetMap.Grid);
            em.AddComponent<StationEventEligibleComponent>(station);
            var farAway = em.SpawnEntity("MobHuman", new MapCoordinates(5000, 5000, targetMap.MapId));
            var elsewhere = em.SpawnEntity("MobHuman", otherMap.GridCoords);
            var minds = server.System<MindSystem>();
            minds.ControlMob(listeners[0].UserId, farAway);
            minds.ControlMob(listeners[1].UserId, elsewhere);

            var ticker = server.System<GameTicker>();
            var rule = ticker.AddGameRule("PowerGridCheck");
            var targets = server.System<GameRuleStationSystem>();
            Assert.That(targets.GetTargetStation(rule), Is.EqualTo(station), "Target is selected before the announcement.");
            Assert.That(targets.GetEventPlayers(rule).Recipients, Does.Contain(listeners[0]));
            Assert.That(targets.GetEventPlayers(rule).Recipients, Does.Not.Contain(listeners[1]));
            Assert.That(targets.GetEventPlayers(station).Recipients, Does.Contain(listeners[0]));
            Assert.That(targets.GetEventPlayers(station).Recipients, Does.Not.Contain(listeners[1]));

            ticker.StartGameRule(rule);
            ticker.StartGameRule(rule);
            Assert.That(em.GetComponent<PowerGridCheckRuleComponent>(rule).AffectedStation, Is.EqualTo(station));
            ticker.EndGameRule(rule);
            Assert.That(targets.GetEventPlayers(rule).Recipients, Does.Contain(listeners[0]));
            Assert.That(targets.GetEventPlayers(rule).Recipients, Does.Not.Contain(listeners[1]));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnvironmentIncludesPlanetAtmosphereAndRestoresVacuum()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var prototypes = server.ProtoMan;
        var centcomm = server.System<CentCommSystem>();

        await server.WaitAssertion(() =>
        {
            var environments = prototypes.EnumeratePrototypes<CentCommEnvironmentPrototype>().ToList();
            Assert.That(environments.Any(environment => environment.Atmosphere != null));
            Assert.That(environments.Any(environment => environment.Atmosphere == null));

            foreach (var environment in environments.OrderBy(environment => environment.Atmosphere == null))
            {
                centcomm.ApplyEnvironment(map.MapUid, environment);
                var atmosphere = server.EntMan.GetComponent<MapAtmosphereComponent>(map.MapUid);
                Assert.That(server.EntMan.GetComponent<ParallaxComponent>(map.MapUid).Parallax,
                    Is.EqualTo(environment.Parallax));
                Assert.That(pair.Client.ProtoMan.HasIndex<Content.Client.Parallax.Data.ParallaxPrototype>(environment.Parallax));
                Assert.That(atmosphere.Space, Is.EqualTo(environment.Atmosphere == null));
                Assert.That(atmosphere.Mixture.Immutable, Is.True);
                if (environment.Atmosphere is { } expected)
                {
                    Assert.That(atmosphere.Mixture.Temperature, Is.EqualTo(expected.Temperature));
                    Assert.That(atmosphere.Mixture.ToArray(), Is.EqualTo(expected.ToArray()));
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RuleAllowlistRejectsModesAndExcludedEvents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var centcomm = server.System<CentCommSystem>();

        await server.WaitAssertion(() =>
        {
            foreach (var id in new[] { "ZombieOutbreak", "LoneOpsSpawn", "NinjaSpawn", "RenegadeSpawn", "ImmovableRodSpawn", "BlobSpawn", "WizardSpawn", "SleeperAgents", "ShadowlingMidround" })
                Assert.That(centcomm.IsAllowedRule(server.ProtoMan.Index<EntityPrototype>(id)), Is.False, id);

            foreach (var prototype in server.System<GameTicker>().GetAllGameRulePrototypes())
            {
                if (!prototype.HasComponent<StationEventComponent>(server.EntMan.ComponentFactory))
                    Assert.That(centcomm.IsAllowedRule(prototype), Is.False, prototype.ID);
            }

            foreach (var id in new[] { "GasLeak", "SolarFlare", "MouseMigration", "DragonSpawn", "ParadoxCloneSpawn", "GameRuleMeteorSwarmSmall" })
                Assert.That(centcomm.IsAllowedRule(server.ProtoMan.Index<EntityPrototype>(id)), Is.True, id);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetSurvivesDelayAndDoesNotLeakToOtherStations()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var centcommMap = await pair.CreateTestMap();
        var otherMap = await pair.CreateTestMap();
        var em = server.EntMan;
        var ticker = server.System<GameTicker>();
        var stations = server.System<StationSystem>();
        var centcomm = server.System<CentCommSystem>();
        EntityUid centralRule = default;
        EntityUid normalRule = default;
        EntityUid centralStation = default;
        EntityUid otherStation = default;

        await server.WaitAssertion(() =>
        {
            centralStation = em.SpawnEntity("NanotrasenCentralCommand", MapCoordinates.Nullspace);
            stations.AddGridToStation(centralStation, centcommMap.Grid);
            otherStation = em.SpawnEntity("TestStation", MapCoordinates.Nullspace);
            stations.AddGridToStation(otherStation, otherMap.Grid);
            em.AddComponent<StationEventEligibleComponent>(otherStation);
            Assert.That(em.HasComponent<StationEventEligibleComponent>(centralStation), Is.False);

            centralRule = ticker.AddGameRule("PowerGridCheck", centralStation);
            normalRule = ticker.AddGameRule("PowerGridCheck");
            ticker.StartGameRule(centralRule);
            ticker.StartGameRule(normalRule);
            Assert.That(em.HasComponent<DelayedStartRuleComponent>(centralRule), Is.True);
            Assert.That(server.System<GameRuleStationSystem>().IsTarget(centralRule, centcommMap.Grid), Is.True);
            Assert.That(server.System<GameRuleStationSystem>().IsTarget(centralRule, otherMap.Grid), Is.False);
            Assert.That(server.System<GameRuleStationSystem>().IsTarget(normalRule, centcommMap.Grid), Is.False);
            Assert.That(server.System<GameRuleStationSystem>().IsTarget(normalRule, otherMap.Grid), Is.True);

            // Starting a delayed rule again advances it without waiting for the timer.
            ticker.StartGameRule(centralRule);
            ticker.StartGameRule(normalRule);
            Assert.That(em.GetComponent<PowerGridCheckRuleComponent>(centralRule).AffectedStation, Is.EqualTo(centralStation));
            Assert.That(em.GetComponent<PowerGridCheckRuleComponent>(normalRule).AffectedStation, Is.EqualTo(otherStation));

            var centralHuman = em.SpawnEntity("MobHuman", centcommMap.GridCoords);
            var otherHuman = em.SpawnEntity("MobHuman", otherMap.GridCoords);
            var hallucinations = ticker.AddGameRule("MassHallucinations", centralStation);
            ticker.StartGameRule(hallucinations);
            Assert.That(em.HasComponent<ParacusiaComponent>(centralHuman), Is.True);
            Assert.That(em.HasComponent<ParacusiaComponent>(otherHuman), Is.False);
            ticker.EndGameRule(hallucinations);
            Assert.That(em.HasComponent<ParacusiaComponent>(centralHuman), Is.False);

            // A rule that loads a shuttle must move its grids to CentComm as well.
            var raid = ticker.AddGameRule("PirateRaid", centralStation);
            var raidGrids = em.GetComponent<RuleGridsComponent>(raid);
            Assert.That(raidGrids.Map, Is.EqualTo(centcommMap.MapId));
            Assert.That(raidGrids.MapGrids, Is.Not.Empty);
            foreach (var grid in raidGrids.MapGrids)
                Assert.That(server.Transform(grid).MapUid, Is.EqualTo(centcommMap.MapUid));

            // Removing the explicit target must not fall back to another station.
            em.DeleteEntity(centralStation);
            var abandonedRule = ticker.AddGameRule("PowerGridCheck", centralStation);
            ticker.StartGameRule(abandonedRule);
            ticker.StartGameRule(abandonedRule);
            Assert.That(em.GetComponent<PowerGridCheckRuleComponent>(abandonedRule).AffectedStation, Is.Not.EqualTo(otherStation));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CentCommConsoleChangesOnlyItsOwnAlertAndChecksAccess()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var em = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var station = em.SpawnEntity("NanotrasenCentralCommand", MapCoordinates.Nullspace);
            var otherStation = em.SpawnEntity("TestStation", MapCoordinates.Nullspace);
            server.System<StationSystem>().AddGridToStation(station, map.Grid);
            var console = em.SpawnEntity("CentcommComputerComms", map.GridCoords);
            var actor = em.SpawnEntity(null, map.GridCoords);
            var comms = server.System<CommunicationsConsoleSystem>();
            var alerts = server.System<AlertLevelSystem>();
            var comp = em.GetComponent<CommunicationsConsoleComponent>(console);
            comms.UpdateCommsConsoleInterface(console, comp);
            var ui = server.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<CommunicationsConsoleInterfaceState>(console, CommunicationsConsoleUiKey.Key, out var state));
            Assert.That(state!.AlertLevels, Does.Contain("blue"));

            em.EventBus.RaiseLocalEvent(console, new CommunicationsConsoleSelectAlertLevelMessage("blue") { Actor = actor });
            Assert.That(alerts.GetLevel(station), Is.EqualTo("green"));

            var access = em.AddComponent<Content.Shared.Access.Components.AccessComponent>(actor);
            access.Tags.Add("CentralCommand");
            em.EventBus.RaiseLocalEvent(console, new CommunicationsConsoleSelectAlertLevelMessage("blue") { Actor = actor });
            Assert.That(alerts.GetLevel(station), Is.EqualTo("blue"));
            Assert.That(alerts.GetLevel(otherStation), Is.EqualTo("green"));
        });

        await pair.CleanReturnAsync();
    }
}
