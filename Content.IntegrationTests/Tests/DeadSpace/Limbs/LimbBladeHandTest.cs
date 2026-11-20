using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbBladeHandTest
{
    [Test]
    public async Task AKnifeWeldedOnAStumpCutsButHoldsNothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var wounds = entities.System<LimbWoundSystem>();
            var hands = entities.System<SharedHandsSystem>();
            var melee = entities.System<SharedMeleeWeaponSystem>();
            var where = new MapCoordinates(Vector2.Zero, map.MapId);
            var human = entities.SpawnEntity("MobHuman", where);
            var knife = entities.SpawnEntity("KitchenKnife", where);
            var plastic = entities.SpawnEntity("SheetPlastic", where);

            Assert.That(wounds.Amputate(human, LimbZone.HandLeft), Is.True);
            Assert.That(wounds.PartFits(LimbZone.HandLeft, knife), Is.True);
            Assert.That(wounds.PartFits(LimbZone.LegLeft, knife), Is.False);
            Assert.That(wounds.PartFits(LimbZone.HandLeft, plastic), Is.False);
            Assert.That(wounds.TryReattach(human, LimbZone.HandLeft, knife, false), Is.True);
            Assert.That(entities.HasComponent<UnremoveableComponent>(knife), Is.True);
            Assert.That(entities.GetComponent<LimbBodyComponent>(human).Zones[(int) LimbZone.HandLeft].Severed, Is.False);

            var held = entities.GetComponent<HandsComponent>(human);
            string bladeHand = null;
            foreach (var id in held.Hands.Keys)
            {
                if (hands.TryGetHeldItem((human, held), id, out var item) && item == knife)
                    bladeHand = id;
            }

            Assert.That(bladeHand, Is.Not.Null);
            Assert.That(hands.TryPickup(human, plastic, bladeHand, handsComp: held), Is.False);
            hands.SetActiveHand((human, held), bladeHand);
            var damage = melee.GetDamage(knife, human);
            Assert.That(damage.DamageDict.TryGetValue("Slash", out var slash) ? slash.Float() : 0f, Is.GreaterThan(0f));

            Assert.That(wounds.Amputate(human, LimbZone.HandLeft), Is.True);
            Assert.That(entities.Deleted(knife), Is.False);
            Assert.That(entities.HasComponent<UnremoveableComponent>(knife), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(knife).ParentUid, Is.Not.EqualTo(human));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BurnsHealWithoutHeatDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var wounds = entities.System<LimbWoundSystem>();
            var human = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(wounds.ApplyEdit(human, LimbZone.ArmLeft, new LimbEdit { Skin = 30f }), Is.True);

            var heal = new DamageSpecifier();
            heal.DamageDict["Heat"] = FixedPoint2.New(-5);
            Assert.That(wounds.CanMend(human, heal), Is.True);
            entities.System<DamageableSystem>().ChangeDamage(human, heal);
            Assert.That(entities.GetComponent<LimbBodyComponent>(human).Zones[(int) LimbZone.ArmLeft].Skin, Is.EqualTo(25f).Within(0.01f));
        });

        await pair.CleanReturnAsync();
    }
}
