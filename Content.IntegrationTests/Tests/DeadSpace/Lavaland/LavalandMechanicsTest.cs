// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Numerics;
using Content.Server.DeadSpace.Lavaland;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Power.Components;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class LavalandMechanicsTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    [Test]
    public async Task ResonatorDamageFallsOffOnBosses()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var protos = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var boss = entities.SpawnEntity("MobLavalandMadMiner", MapCoordinates.Nullspace);
            var bystander = entities.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var bolt = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var comp = entities.AddComponent<LavalandResonatorBoltComponent>(bolt);
            comp.BossDamageFalloff = 0.05f;
            comp.BossDamageMinMultiplier = 0.9f;
            comp.BossDamageWindow = TimeSpan.FromMinutes(1);
            var damage = new DamageSpecifier(protos.Index(Blunt), FixedPoint2.New(10));

            // 1st hit full, then -5% per recent hit, clamped by the minimum multiplier.
            foreach (var expected in new[] { 10f, 9.5f, 9f, 9f })
            {
                var ev = new BeforeProjectileHitEvent(damage, boss);
                entities.EventBus.RaiseLocalEvent(bolt, ref ev);
                Assert.That(ev.Damage.GetTotal().Float(), Is.EqualTo(expected).Within(0.01f));
            }

            var regular = new BeforeProjectileHitEvent(damage, bystander);
            entities.EventBus.RaiseLocalEvent(bolt, ref regular);
            Assert.That(regular.Damage.GetTotal().Float(), Is.EqualTo(10f).Within(0.01f));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MadMinerRescuesOnlyPlayers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var miner = entities.SpawnEntity("MobLavalandMadMiner", map.GridCoords);
            entities.GetComponent<LavalandMadMinerComponent>(miner).Active = true;
            var mob = entities.SpawnEntity("MobHuman", map.GridCoords);
            var chasm = entities.SpawnEntity(null, map.GridCoords);

            var ev = new ChasmFallingAttemptEvent(mob, chasm);
            Assert.Multiple(() =>
            {
                Assert.That(server.System<LavalandMadMinerSystem>().TryRescueFromChasm(mob, ev), Is.False);
                Assert.That(ev.Cancelled, Is.False);
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MorkiteExtractorPowersPipeNetworkAndFillsWell()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid extractor = default;
        EntityUid vent = default;
        EntityUid well = default;

        await server.WaitPost(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            var plating = server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId;
            mapSystem.SetTile(map.Grid, new Vector2i(0, 1), new Tile(plating));
            entities.AddComponent<LavalandMapComponent>(map.MapUid);

            extractor = entities.SpawnEntity("LavalandMorkiteExtractor", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            // The extractor joins the network through a gas port under it, turned north towards the vent.
            var port = entities.SpawnEntity("GasPort", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            server.System<SharedTransformSystem>().SetLocalRotation(port, Angle.FromDegrees(180));
            // The vent's pipe faces south, straight into the port.
            vent = entities.SpawnEntity("GasVentPump", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            well = entities.SpawnEntity("LavalandBluespaceWell", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
        });

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<LavalandMorkitePoweredComponent>(vent), Is.True);
                Assert.That(entities.GetComponent<ApcPowerReceiverComponent>(vent).NeedsPower, Is.False);
                Assert.That(server.System<LavalandMorkiteMissionSystem>().GetMorkiteRate(well), Is.GreaterThan(0f));
            });
            entities.DeleteEntity(extractor);
        });

        await server.WaitRunTicks(90);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<LavalandMorkitePoweredComponent>(vent), Is.False);
                Assert.That(entities.GetComponent<ApcPowerReceiverComponent>(vent).NeedsPower, Is.True);
            });
        });
        await pair.CleanReturnAsync();
    }
}
