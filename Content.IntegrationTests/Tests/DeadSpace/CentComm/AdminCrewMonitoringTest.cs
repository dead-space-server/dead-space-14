// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Medical.SuitSensors;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Inventory;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.CentComm;

[TestFixture]
public sealed class AdminCrewMonitoringTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AdminCrewMonitoringTestUniform
  components:
  - type: Item
  - type: Clothing
    slots: [INNERClothing]
  - type: SuitSensor
    randomMode: false
    mode: SensorCords

- type: entity
  id: AdminCrewMonitoringTestConsole
  components:
  - type: CrewMonitoringConsole
  - type: UserInterface
    interfaces:
      enum.CrewMonitoringUIKey.Key:
        type: CrewMonitoringBoundUserInterface
";

    [Test]
    public async Task AdminMonitorUpdatesWithoutServerAndChecksStationAndPermissions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Fresh = true });
        var server = pair.Server;
        var em = server.EntMan;
        var ui = server.System<SharedUserInterfaceSystem>();
        var admin = server.ResolveDependency<IAdminManager>();
        var player = await server.AddDummySession();
        var firstMap = await pair.CreateTestMap();
        var secondMap = await pair.CreateTestMap();
        EntityUid monitor = default;
        EntityUid console = default;
        EntityUid firstCrew = default;
        EntityUid secondCrew = default;
        EntityUid secondUniform = default;

        CrewMonitoringState ReadState(EntityUid entity)
        {
            Assert.That(ui.TryGetUiState<CrewMonitoringState>(entity, CrewMonitoringUIKey.Key, out var state), Is.True);
            return state!;
        }

        await server.WaitAssertion(() =>
        {
            var stations = server.System<StationSystem>();
            var firstStation = em.SpawnEntity("TestStation", MapCoordinates.Nullspace);
            var secondStation = em.SpawnEntity("TestStation", MapCoordinates.Nullspace);
            stations.AddGridToStation(firstStation, firstMap.Grid);
            stations.AddGridToStation(secondStation, secondMap.Grid);
            firstCrew = em.SpawnEntity("MobHuman", firstMap.GridCoords);
            secondCrew = em.SpawnEntity("MobHuman", secondMap.GridCoords);
            var firstUniform = em.SpawnEntity("AdminCrewMonitoringTestUniform", firstMap.GridCoords);
            secondUniform = em.SpawnEntity("AdminCrewMonitoringTestUniform", secondMap.GridCoords);
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryEquip(firstCrew, firstUniform, "jumpsuit"), Is.True);
            Assert.That(inventory.TryEquip(secondCrew, secondUniform, "jumpsuit"), Is.True);
            Assert.That(em.Count<CrewMonitoringServerComponent>(), Is.Zero);

            monitor = em.SpawnEntity("AdminObserver", firstMap.GridCoords);
            server.System<MindSystem>().ControlMob(player.UserId, monitor);
            Assert.That(ui.TryOpenUi(monitor, CrewMonitoringUIKey.Key, monitor), Is.False,
                "Possessing an admin-ghost entity does not grant administrator permissions.");
            admin.PromoteHost(player);
        });

        await PoolManager.WaitUntil(server, () => admin.GetAdminData(player) != null);
        await server.WaitAssertion(() =>
        {
            Assert.That(ui.TryOpenUi(monitor, CrewMonitoringUIKey.Key, firstCrew), Is.False,
                "Another actor cannot subscribe to the administrator's personal monitor.");
            Assert.That(ui.TryOpenUi(monitor, CrewMonitoringUIKey.Key, monitor), Is.True);
            var state = ReadState(monitor);
            Assert.That(state.Serverless, Is.True);
            Assert.That(state.Sensors.Select(sensor => sensor.OwnerUid), Does.Contain(em.GetNetEntity(firstCrew)));
            Assert.That(state.Sensors.Select(sensor => sensor.OwnerUid), Does.Not.Contain(em.GetNetEntity(secondCrew)));
            Assert.That(state.Sensors.Single(sensor => sensor.OwnerUid == em.GetNetEntity(firstCrew)).Coordinates, Is.Not.Null);

            console = em.SpawnEntity("AdminCrewMonitoringTestConsole", firstMap.GridCoords);
            Assert.That(ui.TryOpenUi(console, CrewMonitoringUIKey.Key, monitor), Is.True);
            Assert.That(ReadState(console).Serverless, Is.False);
            Assert.That(ReadState(console).Sensors, Is.Empty, "Ordinary monitors still depend on the station server.");

            server.System<SharedTransformSystem>().SetCoordinates(monitor, secondMap.GridCoords);
        });

        await PoolManager.WaitUntil(server, () => ReadState(monitor).Sensors.Any(sensor => sensor.OwnerUid == em.GetNetEntity(secondCrew)));
        await server.WaitAssertion(() =>
        {
            Assert.That(ReadState(monitor).Sensors.Select(sensor => sensor.OwnerUid), Does.Not.Contain(em.GetNetEntity(firstCrew)));
            server.System<SuitSensorSystem>().SetSensor(
                (secondUniform, em.GetComponent<SuitSensorComponent>(secondUniform)), SuitSensorMode.SensorBinary);
        });

        await PoolManager.WaitUntil(server, () => ReadState(monitor).Sensors
            .Any(sensor => sensor.OwnerUid == em.GetNetEntity(secondCrew) && sensor.Coordinates == null));
        await server.WaitAssertion(() =>
        {
            var status = ReadState(monitor).Sensors.Single(sensor => sensor.OwnerUid == em.GetNetEntity(secondCrew));
            Assert.That(status.TotalDamage, Is.Null, "The administrator's monitor preserves sensor reporting modes.");
            server.System<SuitSensorSystem>().SetSensor(
                (secondUniform, em.GetComponent<SuitSensorComponent>(secondUniform)), SuitSensorMode.SensorOff);
        });

        await PoolManager.WaitUntil(server, () => ReadState(monitor).Sensors.Count == 0);
        await server.WaitAssertion(() =>
        {
            Assert.That(ReadState(console).Sensors, Is.Empty);
            admin.DeAdmin(player);
            Assert.That(ui.IsUiOpen(monitor, CrewMonitoringUIKey.Key), Is.False);
            Assert.That(ui.TryOpenUi(monitor, CrewMonitoringUIKey.Key, monitor), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
