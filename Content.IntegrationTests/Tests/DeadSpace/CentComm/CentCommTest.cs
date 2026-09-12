// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Atmos.Components;
using Content.Server.Communications;
using Content.Server.DeadSpace.CentComm;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.Communications;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking.Components;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.DeadSpace.CentComm;

[TestFixture]
public sealed class CentCommTest
{
    private static readonly ProtoId<CentCommEnvironmentPrototype> AtmosphereEnvironment = "CentCommTestAtmosphere";
    private static readonly ProtoId<CentCommEnvironmentPrototype> VacuumEnvironment = "CentCommTestVacuum";

    [TestPrototypes]
    private const string Prototypes = @"
- type: parallax
  id: CentCommTestParallax
  layers: []

- type: centCommEnvironment
  id: CentCommTestAtmosphere
  parallax: CentCommTestParallax
  atmosphere:
    volume: 2500
    temperature: 280
    moles:
      Oxygen: 10

- type: centCommEnvironment
  id: CentCommTestVacuum
  parallax: CentCommTestParallax

- type: entity
  id: CentCommTestStation
  parent: TestStation
  components:
  - type: CentCommStation

- type: entity
  id: CentCommTestMapEvent
  parent: BaseGameRule
  components:
  - type: StationEvent
  - type: RuleGrids
  - type: LoadMapRule
    mapPath: /centcomm-test-outpost.yml

- type: entity
  id: CentCommTestOutpostEvent
  parent: CentCommTestMapEvent
  components:
  - type: AntagSelection
    definitions:
    - spawnerPrototype: CentCommTestAntagSpawner
      pickPlayer: false

- type: entity
  id: CentCommTestInheritedOutpostEvent
  parent: CentCommTestOutpostEvent

- type: entity
  id: CentCommTestAntagSpawner
  components:
  - type: GhostRoleAntagSpawner

- type: entity
  id: CentCommTestNonEventRule
  parent: BaseGameRule
  components:
  - type: LoadMapRule

- type: entity
  id: CentCommTestAbstractEvent
  parent: CentCommTestMapEvent
  abstract: true

- type: entity
  id: CentCommTestDelayedPowerEvent
  parent: BaseGameRule
  components:
  - type: GameRule
    delay:
      min: 60
      max: 60
  - type: StationEvent
  - type: PowerGridCheckRule
";

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
            var rule = ticker.AddGameRule("CentCommTestDelayedPowerEvent");
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
        var centcomm = server.System<CentCommSystem>();

        await server.WaitAssertion(() =>
        {
            var environments = new[]
            {
                server.ProtoMan.Index(AtmosphereEnvironment),
                server.ProtoMan.Index(VacuumEnvironment),
            };

            foreach (var environment in environments)
            {
                centcomm.ApplyEnvironment(map.MapUid, environment);
                var atmosphere = server.EntMan.GetComponent<MapAtmosphereComponent>(map.MapUid);
                Assert.That(server.EntMan.GetComponent<ParallaxComponent>(map.MapUid).Parallax,
                    Is.EqualTo(environment.Parallax));
                Assert.That(atmosphere.Space, Is.EqualTo(environment.Atmosphere == null));
                Assert.That(atmosphere.Mixture.Immutable, Is.True);
                if (environment.Atmosphere is { } expected)
                {
                    Assert.That(atmosphere.Mixture.Temperature, Is.EqualTo(expected.Temperature));
                    Assert.That(atmosphere.Mixture.ToArray(), Is.EqualTo(expected.ToArray()));
                }
                else
                {
                    Assert.That(atmosphere.Mixture.TotalMoles, Is.Zero);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CentCommTestMapEvent", true)]
    [TestCase("CentCommTestOutpostEvent", false)]
    [TestCase("CentCommTestInheritedOutpostEvent", false)]
    [TestCase("CentCommTestNonEventRule", false)]
    [TestCase("CentCommTestAbstractEvent", false)]
    public async Task RuleAllowlistRejectsOutpostsAndNonRunnableEvents(string ruleId, bool allowed)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var centcomm = server.System<CentCommSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(centcomm.IsAllowedRule(server.ProtoMan.Index<EntityPrototype>(ruleId)), Is.EqualTo(allowed));
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

            centralRule = ticker.AddGameRule("CentCommTestDelayedPowerEvent", centralStation);
            normalRule = ticker.AddGameRule("CentCommTestDelayedPowerEvent");
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

            // Removing the explicit target must not fall back to another station.
            em.DeleteEntity(centralStation);
            var abandonedRule = ticker.AddGameRule("CentCommTestDelayedPowerEvent", centralStation);
            ticker.StartGameRule(abandonedRule);
            ticker.StartGameRule(abandonedRule);
            Assert.That(em.GetComponent<PowerGridCheckRuleComponent>(abandonedRule).AffectedStation, Is.Not.EqualTo(otherStation));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CentCommStationInitializationPreservesCoordinateDiskAccess()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var centcommMap = await pair.CreateTestMap();
        var shuttleMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var shuttles = server.System<ShuttleSystem>();
            Assert.That(shuttles.TryAddFTLDestination(centcommMap.MapId, true, out _), Is.True);
            Assert.That(shuttles.TryAddFTLDestination(shuttleMap.MapId, true, false, false, out _), Is.True);
            var station = new StationConfig
            {
                StationPrototype = "CentCommTestStation",
                StationComponentOverrides = new(),
            };
            server.System<StationSystem>().InitializeNewStation(station, new[] { centcommMap.Grid.Owner });

            var console = em.SpawnEntity("ComputerShuttle", shuttleMap.GridCoords);
            Assert.That(shuttles.CanFTLTo(shuttleMap.Grid, centcommMap.MapId, console), Is.False);

            var disk = em.SpawnEntity("CoordinatesDisk", shuttleMap.GridCoords);
            var coordinates = em.GetComponent<ShuttleDestinationCoordinatesComponent>(disk);
            coordinates.Destination = shuttleMap.MapUid;
            var slots = server.System<ItemSlotsSystem>();
            Assert.That(slots.TryInsert(console, SharedShuttleConsoleComponent.DiskSlotName, disk, null), Is.True);
            Assert.That(shuttles.CanFTLTo(shuttleMap.Grid, centcommMap.MapId, console), Is.False,
                "A disk for another destination must not grant access to CentComm.");

            coordinates.Destination = centcommMap.MapUid;
            Assert.That(shuttles.CanFTLTo(shuttleMap.Grid, centcommMap.MapId, console), Is.True);
            Assert.That(slots.TryEject(console, SharedShuttleConsoleComponent.DiskSlotName, null, out _), Is.True);
            Assert.That(shuttles.CanFTLTo(shuttleMap.Grid, centcommMap.MapId, console), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LoadedMapsRelocateOnlyForCentComm(bool targetCentComm)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var stationMap = await pair.CreateTestMap();
        var templateMap = await pair.CreateTestMap();
        EntityUid loadedMap = default;
        EntityUid? spawner = null;

        await server.WaitAssertion(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            var spawnPoint = em.SpawnEntity(null, templateMap.GridCoords);
            em.AddComponent<SpawnPointComponent>(spawnPoint);
            Assert.That(server.System<MapLoaderSystem>().TrySaveMap(templateMap.MapId,
                new ResPath("/centcomm-test-outpost.yml")), Is.True);
            mapSystem.DeleteMap(templateMap.MapId);

            var station = em.SpawnEntity(targetCentComm ? "CentCommTestStation" : "TestStation", MapCoordinates.Nullspace);
            server.System<StationSystem>().AddGridToStation(station, stationMap.Grid);
            if (!targetCentComm)
                em.AddComponent<StationEventEligibleComponent>(station);

            var ruleId = targetCentComm ? "CentCommTestMapEvent" : "CentCommTestOutpostEvent";
            var rule = server.System<GameTicker>().AddGameRule(ruleId, targetCentComm ? station : null);
            Assert.That(server.System<GameRuleStationSystem>().GetTargetStation(rule), Is.EqualTo(station));
            var grids = em.GetComponent<RuleGridsComponent>(rule);
            Assert.That(grids.Map, Is.Not.Null);
            Assert.That(grids.MapGrids, Is.Not.Empty);
            loadedMap = mapSystem.GetMapOrInvalid(grids.Map!.Value);
            Assert.That(loadedMap == stationMap.MapUid, Is.EqualTo(targetCentComm));
            foreach (var grid in grids.MapGrids)
                Assert.That(server.Transform(grid).MapUid, Is.EqualTo(loadedMap));

            if (!targetCentComm)
            {
                var selection = em.GetComponent<AntagSelectionComponent>(rule);
                server.System<AntagSelectionSystem>().MakeAntag((rule, selection), null, selection.Definitions.Single());
                spawner = em.AllComponentsList<GhostRoleAntagSpawnerComponent>()
                    .Single(entry => entry.Component.Rule == rule).Uid;
                Assert.That(server.Transform(spawner.Value).MapUid, Is.EqualTo(loadedMap));
            }
        });

        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(em.EntityExists(loadedMap), Is.True, "The loaded map must survive deferred deletion.");
            if (spawner is { } antag)
                Assert.That(server.Transform(antag).MapUid, Is.EqualTo(loadedMap));
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
            var originalLevel = alerts.GetLevel(station);
            var otherLevel = alerts.GetLevel(otherStation);
            var requestedLevel = state!.AlertLevels!.First(level => level != originalLevel);

            em.EventBus.RaiseLocalEvent(console, new CommunicationsConsoleSelectAlertLevelMessage(requestedLevel) { Actor = actor });
            Assert.That(alerts.GetLevel(station), Is.EqualTo(originalLevel));

            var access = em.AddComponent<Content.Shared.Access.Components.AccessComponent>(actor);
            access.Tags.Add("CentralCommand");
            em.EventBus.RaiseLocalEvent(console, new CommunicationsConsoleSelectAlertLevelMessage(requestedLevel) { Actor = actor });
            Assert.That(alerts.GetLevel(station), Is.EqualTo(requestedLevel));
            Assert.That(alerts.GetLevel(otherStation), Is.EqualTo(otherLevel));
        });

        await pair.CleanReturnAsync();
    }
}
