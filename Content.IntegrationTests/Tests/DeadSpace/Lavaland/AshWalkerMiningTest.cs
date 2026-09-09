using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

public sealed class AshWalkerMiningTest : InteractionTest
{
    protected override string PlayerPrototype => "MobAshWalker";

    [Test]
    public async Task WieldedPickaxeMinesBasaltWithOneHitAndDropsOneYield()
    {
        await SpawnTarget("LavalandBasaltBoulder");
        var boulder = STarget!.Value;
        var pickaxe = ToServer(await PlaceInHands("Pickaxe"));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedWieldableSystem>().TryWield(pickaxe,
                SEntMan.GetComponent<WieldableComponent>(pickaxe), SPlayer), Is.True);
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            SCombatMode.SetInCombatMode(SPlayer, true);
            Assert.That(Server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(SPlayer, pickaxe,
                SEntMan.GetComponent<MeleeWeaponComponent>(pickaxe), boulder), Is.True);
        });
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(boulder), Is.True);
            var basalt = SEntMan.EntityQuery<StackComponent>().Where(stack => stack.StackTypeId == "Basalt");
            Assert.That(basalt.Sum(stack => stack.Count), Is.InRange(12, 16));
        });
    }
}
