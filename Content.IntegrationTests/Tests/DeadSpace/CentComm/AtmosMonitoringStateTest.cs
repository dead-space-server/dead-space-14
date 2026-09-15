// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.CentComm;

[TestFixture]
public sealed class AtmosMonitoringStateTest
{
    [Test]
    public async Task ReparentedConsoleSendsFullStateToEveryOlderBaseline()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid console = default;
        GameTick baseline = default;

        await server.WaitPost(() =>
        {
            console = em.SpawnEntity("ComputerAtmosMonitoring", map.GridCoords);
            baseline = server.Timing.CurTick;
        });
        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            server.System<SharedTransformSystem>().SetParent(console, map.MapUid);
            var component = em.GetComponent<AtmosMonitoringConsoleComponent>(console);
            Assert.That(component.ForceFullUpdateTick, Is.GreaterThan(baseline));

            var first = em.GetComponentState(em.EventBus, component, null, baseline + 1);
            var second = em.GetComponentState(em.EventBus, component, null, baseline + 1);
            Assert.That(first, Is.Not.Null.And.Not.InstanceOf<IComponentDeltaState>());
            Assert.That(second, Is.Not.Null.And.Not.InstanceOf<IComponentDeltaState>());

            var caughtUp = em.GetComponentState(em.EventBus, component, null, component.ForceFullUpdateTick + 1);
            Assert.That(caughtUp, Is.InstanceOf<IComponentDeltaState>());
        });

        await pair.CleanReturnAsync();
    }
}
