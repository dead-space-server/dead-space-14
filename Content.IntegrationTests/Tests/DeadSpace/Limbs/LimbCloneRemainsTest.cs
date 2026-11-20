// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Cloning;
using Content.Server.DeadSpace.Cloning;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbCloneRemainsTest
{
    [Test]
    public async Task ABrainAndAHeadGoIntoTheScannerAndTheBrainClones()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var remains = entities.System<CloningLimbSystem>();
            var body = entities.System<SharedBodySystem>();

            var donor = entities.SpawnEntity("MobReptilian", at);
            var brain = body.GetBodyOrgans(donor).First(o => entities.HasComponent<BrainComponent>(o.Id)).Id;
            Assert.That(remains.IsRemains(brain), Is.False, "a brain inside a body");
            Assert.That(body.RemoveOrgan(brain), Is.True);
            Assert.That(remains.IsRemains(brain), Is.True, "a loose brain");

            Assert.That(entities.System<CloningSystem>().TryCloning(brain, at, "CloningPod", out var clone), Is.True);
            Assert.That(entities.GetComponent<HumanoidAppearanceComponent>(clone.Value).Species.Id, Is.EqualTo("Reptilian"));
            Assert.That(entities.GetComponent<DnaComponent>(clone.Value).DNA, Is.EqualTo(entities.GetComponent<DnaComponent>(donor).DNA));
            Assert.That(entities.GetComponent<MetaDataComponent>(clone.Value).EntityName, Is.EqualTo(entities.GetComponent<MetaDataComponent>(donor).EntityName));

            var headless = entities.SpawnEntity("MobHuman", at);
            Assert.That(entities.System<LimbWoundSystem>().Amputate(headless, LimbZone.Head), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var remains = entities.System<CloningLimbSystem>();
            var heads = 0;
            var query = entities.EntityQueryEnumerator<Content.Shared.Body.Part.BodyPartComponent>();
            while (query.MoveNext(out var uid, out var part))
            {
                if (part.PartType == Content.Shared.Body.Part.BodyPartType.Head && part.Body == null && remains.IsRemains(uid))
                    heads++;
            }

            Assert.That(heads, Is.GreaterThan(0), "a severed head");
        });

        await pair.CleanReturnAsync();
    }
}
