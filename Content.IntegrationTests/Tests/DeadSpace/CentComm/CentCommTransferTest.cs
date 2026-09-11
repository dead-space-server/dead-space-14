// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.Components;
using Content.Server.DeadSpace.CentComm;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.DeadSpace.CentComm;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weather;
using Content.Shared.Timing;
using Content.Shared.Shuttles.Systems;
using Robust.Server.Console;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.CentComm;

[TestFixture]
public sealed class CentCommTransferTest
{
    [TestCase(CentCommTemperature.Cold, 0f, 253.15f)]
    [TestCase(CentCommTemperature.Normal, 0f, 293.15f)]
    [TestCase(CentCommTemperature.Hot, 0f, 333.15f)]
    [TestCase(CentCommTemperature.Custom, 12.5f, 285.65f)]
    public void ConvertsCelsius(CentCommTemperature preset, float celsius, float expected)
    {
        Assert.That(CentCommTransferSettings.TryGetTemperature(preset, celsius, out var kelvin));
        Assert.That(kelvin, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void RejectsInvalidTemperatures()
    {
        Assert.That(CentCommTransferSettings.TryGetTemperature(CentCommTemperature.Space, 0, out var vacuum));
        Assert.That(vacuum, Is.Null);
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -273.15f, float.MaxValue })
            Assert.That(CentCommTransferSettings.TryGetTemperature(CentCommTemperature.Custom, value, out _), Is.False);
        Assert.That(CentCommTransferSettings.TryGetTemperature((CentCommTemperature) 99, 20, out _), Is.False);
    }

    [Test]
    public async Task ServerCatalogIncludesEveryClientParallax()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var ids = pair.Server.System<CentCommTransferSystem>().GetParallaxes();
            var clientIds = pair.Client.ProtoMan.EnumeratePrototypes<Content.Client.Parallax.Data.ParallaxPrototype>()
                .Select(prototype => prototype.ID).ToArray();
            Assert.That(ids, Is.Not.Empty.And.EquivalentTo(clientIds));
            Assert.That(ids, Does.Contain("Wizard"), "Transfer choices must extend beyond the CentComm environment pool.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CommandsAndEuiRejectMissingOrRevokedFunPermission()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Fresh = true, Dirty = true });
        var server = pair.Server;
        var player = await server.AddDummySession();
        var admin = server.ResolveDependency<IAdminManager>();
        var host = server.ResolveDependency<IConsoleHost>();
        var shell = new ConsoleShell(host, player, false);
        var transfer = server.System<CentCommTransferSystem>();
        var request = new CentCommTransferRequest("Default", CentCommTemperature.Normal, 20f, null);
        var commands = new[] { "centcomm_transfer", "addgamerulecentcomm" };
        var permissions = server.ResolveDependency<IConGroupController>();

        await server.WaitAssertion(() =>
        {
            foreach (var command in commands)
            {
                Assert.That(permissions.CanCommand(player, command), Is.False);
                Assert.Throws<AssertionException>(() => host.AvailableCommands[command].Execute(shell, "", []));
            }
            Assert.That(transfer.TryStart(player, request, out _), Is.False);

            admin.PromoteHost(player);
        });
        await PoolManager.WaitUntil(server, () => admin.GetAdminData(player) != null, maxTicks: 180);
        await server.WaitAssertion(() =>
        {
            var data = admin.GetAdminData(player)!;
            data.Flags = AdminFlags.Admin;
            foreach (var command in commands)
                Assert.That(permissions.CanCommand(player, command), Is.False);
            Assert.That(transfer.TryStart(player, request, out _), Is.False);

            data.Flags |= AdminFlags.Fun;
            foreach (var command in commands)
                Assert.That(permissions.CanCommand(player, command), Is.True);

            var ui = new CentCommTransferEui();
            server.ResolveDependency<EuiManager>().OpenEui(ui, player);
            admin.DeAdmin(player);
            Assert.That(ui.IsShutDown, Is.True);
            ui.HandleMessage(request);
            foreach (var command in commands)
                Assert.That(permissions.CanCommand(player, command), Is.False);
            Assert.That(server.EntMan.Count<CentCommTransferComponent>(), Is.Zero);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TransferWaitsThenReturnsToSamePoseAndAppliesEnvironment()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var em = server.EntMan;
        var map = await pair.CreateTestMap();
        var system = server.System<CentCommTransferSystem>();
        var xforms = server.System<SharedTransformSystem>();
        var originalPosition = new Vector2(42f, -17f);
        var originalRotation = Angle.FromDegrees(37);
        var request = new CentCommTransferRequest("Wizard", CentCommTemperature.Custom, -12.5f, "SnowfallLight");

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<GameTicker>().RunLevel, Is.EqualTo(GameRunLevel.InRound));
            var station = em.SpawnEntity("NanotrasenCentralCommand", MapCoordinates.Nullspace);
            server.System<StationSystem>().AddGridToStation(station, map.Grid);
            em.EnsureComponent<ShuttleComponent>(map.Grid);
            server.System<ShuttleSystem>().Disable(map.Grid);
            xforms.SetLocalPosition(map.Grid, originalPosition);
            xforms.SetLocalRotation(map.Grid, originalRotation);
            var link = em.AllComponentsList<StationCentcommComponent>().Select(entry => entry.Component).FirstOrDefault()
                ?? em.AddComponent<StationCentcommComponent>(em.SpawnEntity(null, MapCoordinates.Nullspace));
            link.Entity = map.Grid;
            link.MapEntity = map.MapUid;
            Assert.That(server.System<RoundEndSystem>().GetCentcommGridEntity(), Is.EqualTo(map.Grid.Owner));
            Assert.That(em.HasComponent<PhysicsComponent>(map.Grid), Is.True);


            Assert.That(system.TryStart(null, new CentCommTransferRequest("missing-parallax", CentCommTemperature.Normal, 0, null), out _), Is.False);
            Assert.That(system.TryStart(null, new CentCommTransferRequest("Wizard", CentCommTemperature.Normal, 0, "missing-weather"), out _), Is.False);
            Assert.That(em.HasComponent<CentCommTransferComponent>(map.Grid), Is.False);
            Assert.That(system.TryStart(null, request, out var result), Is.True, result);
            Assert.That(system.TryStart(null, request, out _), Is.False, "Concurrent transfers must be rejected.");
            var transfer = em.GetComponent<CentCommTransferComponent>(map.Grid);
            Assert.That(transfer.StartAt - server.Timing.CurTime, Is.EqualTo(TimeSpan.FromMinutes(1)));
            system.Update(0);
            Assert.That(em.HasComponent<FTLComponent>(map.Grid), Is.False);

            // Advance only the preparation deadline; the actual transition uses the normal FTL system.
            transfer.StartAt = server.Timing.CurTime;
            system.Update(0);
            var starting = em.GetComponent<FTLComponent>(map.Grid);
            Assert.That(starting.State, Is.EqualTo(FTLState.Starting));
            Assert.That(starting.StartupTime, Is.EqualTo(server.System<ShuttleSystem>().DefaultStartupTime));
            Assert.That(server.Transform(map.Grid).MapUid, Is.EqualTo(map.MapUid));
            starting.StateTime = StartEndTime.FromCurTime(server.Timing, TimeSpan.Zero);
        });

        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.Transform(map.Grid).MapUid, Is.Not.EqualTo(map.MapUid));
            var ftl = em.GetComponent<FTLComponent>(map.Grid);
            Assert.That(ftl.State, Is.EqualTo(FTLState.Travelling));
            ftl.StateTime = StartEndTime.FromCurTime(server.Timing, TimeSpan.Zero);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var ftl = em.GetComponent<FTLComponent>(map.Grid);
            Assert.That(ftl.State, Is.EqualTo(FTLState.Arriving));
            ftl.StateTime = StartEndTime.FromCurTime(server.Timing, TimeSpan.Zero);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var transform = server.Transform(map.Grid);
            Assert.That(transform.MapUid, Is.EqualTo(map.MapUid));
            Assert.That(transform.LocalPosition, Is.EqualTo(originalPosition));
            Assert.That(transform.LocalRotation, Is.EqualTo(originalRotation));
            Assert.That(em.GetComponent<PhysicsComponent>(map.Grid).BodyType, Is.EqualTo(BodyType.Static));
            Assert.That(em.GetComponent<ParallaxComponent>(map.MapUid).Parallax, Is.EqualTo("Wizard"));
            var atmosphere = em.GetComponent<MapAtmosphereComponent>(map.MapUid);
            Assert.That(atmosphere.Space, Is.False);
            Assert.That(atmosphere.Mixture.Temperature, Is.EqualTo(260.65f).Within(0.001f));
            Assert.That(atmosphere.Mixture.Pressure, Is.EqualTo(Atmospherics.OneAtmosphere).Within(0.001f));
            Assert.That(em.GetComponent<WeatherComponent>(map.MapUid).Weather.ContainsKey("SnowfallLight"));
        });
        await pair.CleanReturnAsync();
    }
}
