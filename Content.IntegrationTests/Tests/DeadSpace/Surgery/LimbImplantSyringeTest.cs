using System.Numerics;
using Content.Shared.DeadSpace.Abilities.Slide;
using Content.Shared.DeadSpace.Abilities.StandUp;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Dodge;
using Content.Shared.DeadSpace.Reactions;
using Content.Shared.DeadSpace.Sandevistan;
using Content.Shared.DeadSpace.Weapons;
using Content.Shared.DeadSpace.Weapons.Akimbo;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.Surgery;

[TestFixture]
public sealed class LimbImplantSyringeTest
{
    [TestCase("SandevistanLoneImplanter", "Back")]
    [TestCase("MantisBladesLoneImplanter", "ArmLeft")]
    [TestCase("MonowireLoneImplanter", "ArmLeft")]
    [TestCase("KerenskyImplanter", "Back")]
    public async Task ALoneSyringeSeatsItsImplant(string syringe, string slot)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var implanters = entMan.System<SharedImplanterSystem>();
            var implants = entMan.System<LimbImplantSystem>();

            var human = entMan.SpawnEntity("MobHuman", at);
            var implanter = entMan.SpawnEntity(syringe, at);
            var comp = entMan.GetComponent<ImplanterComponent>(implanter);
            Assert.That(implanters.CanImplantLimb(human, comp, out _), Is.True);

            implanters.Implant(human, human, implanter, comp);
            Assert.That(implants.Occupied(human, slot), Is.True);

            if (syringe is "SandevistanLoneImplanter" or "KerenskyImplanter")
            {
                var visual = entMan.GetComponent<SandevistanBodyVisualComponent>(human);
                var expected = syringe == "KerenskyImplanter" ? "kerensky" : "sandevistan";
                Assert.That(visual.Sprite.ToString(), Does.EndWith($"_DeadSpace/Mobs/Effects/{expected}.rsi"));
            }

            if (syringe == "KerenskyImplanter")
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<DodgeComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<DodgeVisualComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<LimbTargetBonusComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<SemiAutoFireRateComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<AkimboAccuracyComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<SpeedSlidingComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<InstantStandUpComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<NearbyCriticalShoutComponent>(human), Is.True);
                    Assert.That(entMan.GetComponent<DodgeComponent>(human).GuaranteedStaminaCost, Is.EqualTo(6f));
                    Assert.That(entMan.GetComponent<SemiAutoFireRateComponent>(human).OnlyWhileActive, Is.True);
                    Assert.That(entMan.GetComponent<NearbyCriticalShoutComponent>(human).Cooldown, Is.EqualTo(5f));
                    Assert.That(server.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>("KerenskyImplant").HideSpawnMenu, Is.False);
                    Assert.That(server.ResolveDependency<IPrototypeManager>().Index<EntityPrototype>("KerenskyImplanter").HideSpawnMenu, Is.False);
                });
            }
        });

        await pair.CleanReturnAsync();
    }
}
