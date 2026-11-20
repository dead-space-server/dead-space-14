// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Kitchen.EntitySystems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Kitchen;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbGrindTest
{
    [Test]
    public async Task SeveredLimbsGrindButHeadsDoNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        EntityUid human = default;
        EntityUid arm = default;
        EntityUid other = default;
        EntityUid head = default;
        await server.WaitPost(() =>
        {
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            arm = entities.SpawnEntity("LeftArmHuman", at);
            other = entities.SpawnEntity("LeftArmHuman", at);
            head = entities.SpawnEntity("HeadHuman", at);
            human = entities.SpawnEntity("MobHuman", at);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var grinder = entities.System<ReagentGrinderSystem>();
            Assert.That(entities.System<SharedBodySystem>().GetBodyChildrenOfType(human, BodyPartType.Arm)
                .All(p => !grinder.CanGrind(p.Id)), Is.True, "an attached arm stays ungrindable");
            Assert.That(grinder.CanGrind(arm), Is.True, "arm");
            Assert.That(grinder.CanGrind(head), Is.False, "head");

            var ground = grinder.GetGrinderSolution(arm, GrinderProgram.Grind);
            Assert.That(ground, Is.Not.Null);
            Assert.That(ground.Volume.Float(), Is.GreaterThan(0f));
            ground.RemoveAllSolution();
            Assert.That(grinder.GetGrinderSolution(other, GrinderProgram.Grind).Volume.Float(), Is.GreaterThan(0f), "arms share one solution");

            Assert.That(entities.System<LimbWoundSystem>().Amputate(human, LimbZone.ArmRight), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var grinder = entities.System<ReagentGrinderSystem>();
            var severed = new List<EntityUid>();
            var query = entities.EntityQueryEnumerator<BodyPartComponent>();
            while (query.MoveNext(out var uid, out var part))
            {
                if (part.Body == null && part.PartType == BodyPartType.Arm && part.Symmetry == BodyPartSymmetry.Right)
                    severed.Add(uid);
            }

            Assert.That(severed, Is.Not.Empty);
            Assert.That(severed.All(p => grinder.CanGrind(p)), Is.True, "a severed arm grinds");
        });

        await pair.CleanReturnAsync();
    }
}
