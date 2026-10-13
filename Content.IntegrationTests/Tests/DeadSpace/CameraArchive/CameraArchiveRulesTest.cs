// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.CameraArchives;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.CameraArchives;

[TestFixture]
public sealed class CameraArchiveRulesTest
{
    [Test]
    public async Task IgnoredBodiesAreNotPeopleAndACloakHidesTheName()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var stills = server.System<CameraArchiveSystem>();
            var human = ents.SpawnEntity("MobHuman", map.GridCoords);
            var mouse = ents.SpawnEntity("MobMouse", map.GridCoords);
            var ghost = ents.SpawnEntity("MobHuman", map.GridCoords);
            ents.EnsureComponent<GhostComponent>(ghost);
            var hidden = ents.SpawnEntity("MobHuman", map.GridCoords);
            ents.EnsureComponent<CameraArchiveIgnoreComponent>(hidden);

            Assert.That(stills.CountsAsPerson(human), Is.True);
            Assert.That(stills.CountsAsPerson(mouse), Is.False);
            Assert.That(stills.CountsAsPerson(ghost), Is.False);
            Assert.That(stills.CountsAsPerson(hidden), Is.False);

            var camera = ents.SpawnEntity("SurveillanceCameraSecurity", map.GridCoords);
            var cloak = ents.SpawnEntity("ClothingNeckCameraConcealCloak", map.GridCoords);
            Assert.That(server.System<InventorySystem>().TryEquip(human, cloak, "neck", force: true, silent: true), Is.True);

            var still = stills.Capture(camera);
            Assert.That(still, Is.Not.Null);
            var people = 0;
            foreach (var entity in still!.Entities)
            {
                if (entity.Prototype == "MobHuman")
                    people++;
            }

            Assert.That(people, Is.EqualTo(1));
            var covered = still.Entities.Find(entity => entity.Conceal);
            Assert.That(covered, Is.Not.Null);
            Assert.That(covered!.Humanoid, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AWallHidesAPersonFromTheCamera()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var grid = map.Grid.Owner;

        EntityUid camera = default;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var ents = server.EntMan;
            var xform = server.System<SharedTransformSystem>();
            camera = ents.SpawnEntity("SurveillanceCameraSecurity", new EntityCoordinates(grid, 0.5f, 1.5f));
            xform.SetLocalRotation(camera, Angle.Zero);
            human = ents.SpawnEntity("MobHuman", new EntityCoordinates(grid, 4.5f, 1.5f));
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<CameraArchiveSystem>().Sees(camera, human), Is.True);
            var wall = server.EntMan.SpawnEntity("WallSolid", new EntityCoordinates(grid, 2.5f, 1.5f));
            server.System<SharedTransformSystem>().AnchorEntity(wall);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<CameraArchiveSystem>().Sees(camera, human), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
