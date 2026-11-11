using System.Linq;
using System.Numerics;
using Content.Server.DeadSpace.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Surgery;

[TestFixture]
public sealed class SurgeryGraftTest
{
    private static EntityUid TakeOrgan(IEntityManager entMan, EntityUid body, string name)
    {
        var bodies = entMan.System<SharedBodySystem>();
        var organ = bodies.GetBodyOrgans(body)
            .Select(found => found.Id)
            .First(id => entMan.GetComponent<MetaDataComponent>(id).EntityPrototype.ID.Contains(name));
        Assert.That(bodies.RemoveOrgan(organ), Is.True);
        return organ;
    }

    [Test]
    public async Task ForeignOrganNeedsMutagen()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var graft = entMan.System<SurgeryGraftSystem>();
            var lizard = entMan.SpawnEntity("MobReptilian", at);
            var donor = entMan.SpawnEntity("MobHuman", at);
            var patient = entMan.SpawnEntity("MobHuman", at);
            var dosed = entMan.SpawnEntity("MobHuman", at);
            var diona = entMan.SpawnEntity("MobDiona", at);

            var lizardHeart = TakeOrgan(entMan, lizard, "Heart");
            var lizardLungs = TakeOrgan(entMan, lizard, "Lungs");
            var humanHeart = TakeOrgan(entMan, donor, "Heart");

            var bloodstream = entMan.GetComponent<BloodstreamComponent>(dosed);
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(dosed, bloodstream.BloodSolutionName, out var blood, out _), Is.True);
            solutions.TryAddReagent(blood.Value, "GraftMutagenHumanReptile", FixedPoint2.New(5), out _);

            graft.Grafted(patient, humanHeart, LimbZone.Torso, nameof(LimbOrgan.Heart), null);
            Assert.That(entMan.HasComponent<SurgeryRejectionComponent>(patient), Is.False, "same species should not reject");

            graft.Grafted(dosed, lizardLungs, LimbZone.Torso, nameof(LimbOrgan.Lungs), null);
            Assert.That(entMan.HasComponent<SurgeryRejectionComponent>(dosed), Is.False, "mutagen in blood should prevent rejection");

            Assert.That(entMan.HasComponent<SurgeryOriginComponent>(lizardHeart), Is.True);
            Assert.That(graft.Accepts(patient, lizardHeart, false), Is.True);
            graft.Grafted(patient, lizardHeart, LimbZone.Torso, nameof(LimbOrgan.Heart), null);
            var rejection = entMan.GetComponent<SurgeryRejectionComponent>(patient);
            Assert.Multiple(() =>
            {
                Assert.That(rejection.Entries, Has.Count.EqualTo(1));
                Assert.That(rejection.Entries[0].Organ, Is.EqualTo(nameof(LimbOrgan.Heart)));
                Assert.That(rejection.Zones, Is.Empty, "an organ should not blacken the limb");
                Assert.That(graft.Accepts(diona, lizardHeart, false), Is.False, "a diona takes no foreign organs");
            });
        });

        await pair.CleanReturnAsync();
    }
}
