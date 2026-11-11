using System.Linq;
using System.Numerics;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbGraftFistTest
{
    [TestCase(LimbZone.HandRight, BodyPartType.Hand)]
    [TestCase(LimbZone.ArmRight, BodyPartType.Arm)]
    public async Task AGraftedLizardHandClaws(LimbZone zone, BodyPartType type)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var wounds = entities.System<LimbWoundSystem>();
            var melee = entities.System<SharedMeleeWeaponSystem>();
            var hands = entities.System<SharedHandsSystem>();
            var where = new MapCoordinates(Vector2.Zero, map.MapId);
            var lizard = entities.SpawnEntity("MobReptilian", where);
            var human = entities.SpawnEntity("MobHuman", where);

            Assert.That(wounds.Amputate(lizard, zone), Is.True);
            EntityUid part = default;
            var query = entities.EntityQueryEnumerator<BodyPartComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Body == null && comp.PartType == type && comp.Symmetry == BodyPartSymmetry.Right)
                    part = uid;
            }

            Assert.That(part, Is.Not.EqualTo(default(EntityUid)));
            Assert.That(wounds.Amputate(human, zone), Is.True);
            Assert.That(wounds.TryReattach(human, zone, part, false), Is.True);

            var held = entities.GetComponent<HandsComponent>(human);
            var right = held.Hands.First(hand => hand.Value.Location == HandLocation.Right).Key;
            hands.SetActiveHand((human, held), right);

            var damage = melee.GetDamage(human, human);
            Assert.That(damage.DamageDict.TryGetValue("Slash", out var slash) ? slash.Float() : 0f, Is.GreaterThan(0f));
            Assert.That(damage.DamageDict.TryGetValue("Blunt", out var blunt) ? blunt.Float() : 0f, Is.EqualTo(0f));
        });

        await pair.CleanReturnAsync();
    }
}
