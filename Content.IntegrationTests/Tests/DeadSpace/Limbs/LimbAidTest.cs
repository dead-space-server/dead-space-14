using System.Linq;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Body.Components;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbAidTest
{
    [Test]
    public async Task APegLegReplacesALostLeg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var human = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = entities.System<LimbWoundSystem>();
            Assert.That(wounds.Amputate(human, LimbZone.LegLeft), Is.True);

            var peg = entities.SpawnEntity("ProstheticPegLeg", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(entities.System<LimbPegSystem>().Fit((peg, entities.GetComponent<LimbPegComponent>(peg)), human, LimbZone.LegLeft), Is.True);

            var limbs = entities.GetComponent<LimbBodyComponent>(human);
            var look = entities.GetComponent<HumanoidAppearanceComponent>(human);
            Assert.Multiple(() =>
            {
                Assert.That(limbs.Zones[(int) LimbZone.LegLeft].Severed, Is.False);
                Assert.That(limbs.Zones[(int) LimbZone.FootLeft].Severed, Is.False);
                Assert.That(entities.GetComponent<BodyComponent>(human).LegEntities.Count, Is.EqualTo(2));
                Assert.That(look.CustomBaseLayers[HumanoidVisualLayers.LLeg].Id, Is.EqualTo("MobPegLegLeft"));
                Assert.That(look.PermanentlyHidden, Does.Contain(HumanoidVisualLayers.LFoot));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ASplintKnitsAFractureInTwoSteps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            human = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = entities.System<LimbWoundSystem>();
            wounds.ApplyEdit(human, LimbZone.LegLeft, new LimbEdit { SetBone = true, Bone = LimbWoundRules.BreakBone });

            var splint = entities.SpawnEntity("FieldSplint", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(entities.System<LimbSplintSystem>().Apply((splint, entities.GetComponent<LimbSplintComponent>(splint)), human, LimbZone.LegLeft), Is.True);

            var leg = entities.GetComponent<LimbBodyComponent>(human).Zones[(int) LimbZone.LegLeft];
            Assert.That(leg.Splinted, Is.True);
            Assert.That(LimbWoundRules.Limp(leg, false), Is.False);
            entities.GetComponent<LimbKnittingComponent>(human).Due[LimbZone.LegLeft] = TimeSpan.Zero;
        });

        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var leg = server.EntMan.GetComponent<LimbBodyComponent>(human).Zones[(int) LimbZone.LegLeft];
            Assert.That(leg.Bone, Is.EqualTo(LimbWoundRules.StrainBone));
            server.EntMan.GetComponent<LimbKnittingComponent>(human).Due[LimbZone.LegLeft] = TimeSpan.Zero;
        });

        await server.WaitRunTicks(90);
        await server.WaitAssertion(() =>
        {
            var leg = server.EntMan.GetComponent<LimbBodyComponent>(human).Zones[(int) LimbZone.LegLeft];
            Assert.That(leg.Bone, Is.EqualTo(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ALostFootTakesNoShoes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var human = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var inventory = entities.System<InventorySystem>();
            if (inventory.TryGetSlotEntity(human, "shoes", out var worn))
                entities.DeleteEntity(worn.Value);

            Assert.That(entities.System<LimbWoundSystem>().Amputate(human, LimbZone.FootLeft), Is.True);
            var shoes = entities.SpawnEntity("ClothingShoesColorBlack", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(inventory.TryEquip(human, shoes, "shoes", silent: true, force: false), Is.False);
            Assert.That(LimbWearSystem.Missing(entities.GetComponent<LimbBodyComponent>(human), SlotFlags.GLOVES), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ABodyWithoutArmsAndLegsCannotMove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var human = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = entities.System<LimbWoundSystem>();
            var blocker = entities.System<ActionBlockerSystem>();
            foreach (var zone in new[] { LimbZone.ArmLeft, LimbZone.ArmRight, LimbZone.LegLeft })
                Assert.That(wounds.Amputate(human, zone), Is.True);
            Assert.That(blocker.CanMove(human), Is.True);

            Assert.That(wounds.Amputate(human, LimbZone.LegRight), Is.True);
            Assert.That(blocker.CanMove(human), Is.False);

            Assert.That(wounds.TryRegrowZone(human, LimbZone.LegRight, 0.3f), Is.True);
            Assert.That(blocker.CanMove(human), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyCavitiesHideWhatIsInside()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var human = entities.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var containers = entities.System<SharedContainerSystem>();
            foreach (var id in new[] { SurgeryCavityComponent.Head, SurgeryCavityComponent.Chest, SurgeryCavityComponent.Abdomen })
            {
                Assert.That(containers.TryGetContainer(human, id, out var cavity), Is.True, id);
                Assert.That(cavity.ShowContents, Is.False, id);
                Assert.That(cavity.OccludesLight, Is.True, id);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AChangelingRegrowsItsHeadFromStasis()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid ling = default;

        await server.WaitPost(() =>
        {
            ling = server.EntMan.SpawnEntity("MobLing", new MapCoordinates(Vector2.Zero, map.MapId));
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var action = entities.GetComponent<ActionsComponent>(ling).Actions
                .First(a => entities.HasComponent<RegenerativeStasisActionComponent>(a));
            var stasis = entities.GetComponent<RegenerativeStasisActionComponent>(action);

            Assert.That(entities.System<LimbWoundSystem>().Amputate(ling, LimbZone.Head), Is.True);
            Assert.That(stasis.IsInStasis, Is.False);
            Assert.That(entities.System<MobStateSystem>().IsAlive(ling), Is.True, "a headless changeling stays alive");

            entities.System<RegenerativeStasisSystem>().EnterStasis((action, stasis), ling);
            Assert.That(stasis.IsInStasis, Is.True);

            entities.System<RegenerativeStasisSystem>().ExitStasis((action, stasis), ling);
            var limbs = entities.GetComponent<LimbBodyComponent>(ling);
            Assert.Multiple(() =>
            {
                Assert.That(limbs.Zones[(int) LimbZone.Head].Severed, Is.False);
                Assert.That(entities.System<LimbWoundSystem>().HasPart(ling, LimbZone.Head), Is.True);
                Assert.That(entities.System<MobStateSystem>().IsAlive(ling), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task AChangelingActionStasisRegrowsTheHead(bool stasisFirst)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid ling = default;
        EntityUid action = default;

        await server.WaitPost(() =>
        {
            var entities = server.EntMan;
            ling = entities.SpawnEntity("MobLing", new MapCoordinates(Vector2.Zero, map.MapId));
            var minds = entities.System<SharedMindSystem>();
            var mind = minds.CreateMind(null, "ling");
            minds.TransferTo(mind, ling);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var actions = entities.System<SharedActionsSystem>();
            action = entities.GetComponent<ActionsComponent>(ling).Actions
                .First(a => entities.HasComponent<RegenerativeStasisActionComponent>(a));
            if (stasisFirst)
                actions.PerformAction(ling, (action, entities.GetComponent<ActionComponent>(action)), predicted: false);
            else
            {
                var damage = new DamageSpecifier();
                damage.DamageDict["Blunt"] = FixedPoint2.New(400);
                entities.System<DamageableSystem>().TryChangeDamage(ling, damage, true);
            }

            Assert.That(entities.System<LimbWoundSystem>().Amputate(ling, LimbZone.Head), Is.True);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var minds = entities.System<SharedMindSystem>();
            Assert.That(minds.TryGetMind(ling, out _, out _), Is.True, "mind left the body");
            var actions = entities.System<SharedActionsSystem>();
            var stasis = entities.GetComponent<RegenerativeStasisActionComponent>(action);
            if (!stasisFirst)
            {
                Assert.That(stasis.IsInStasis, Is.False, "losing the head does not enter stasis");
                actions.PerformAction(ling, (action, entities.GetComponent<ActionComponent>(action)), predicted: false);
            }

            Assert.That(stasis.IsInStasis, Is.True, "stasis survives the head loss");
            actions.ClearCooldown(action);
            actions.PerformAction(ling, (action, entities.GetComponent<ActionComponent>(action)), predicted: false);
        });
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<RegenerativeStasisActionComponent>(action).IsInStasis, Is.False);
                Assert.That(entities.System<LimbWoundSystem>().HasPart(ling, LimbZone.Head), Is.True);
                Assert.That(entities.System<MobStateSystem>().IsAlive(ling), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }
}
