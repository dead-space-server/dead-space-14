using Content.Server.DeadSpace.Lavaland;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared.Construction;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class LavalandOreRedeemerTest
{
    private const string SteelOre = "SteelOre";
    private const string GoldOre = "GoldOre";

    [Test]
    public async Task FullStorageInsertRedeemsInsertedOre()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var storage = server.System<StorageSystem>();
        var stack = server.System<StackSystem>();

        EntityUid incomingOre = default;

        await server.WaitPost(() =>
        {
            var oreBox = entMan.SpawnEntity("OreBox", MapCoordinates.Nullspace);
            var oreBag = entMan.SpawnEntity("OreBag", MapCoordinates.Nullspace);
            incomingOre = SpawnOre(entMan, stack, SteelOre, 30);

            Assert.That(storage.Insert(oreBag, incomingOre, out _, playSound: false), Is.True);

            var ev = RedeemBagIntoBox(entMan, oreBag, oreBox);

            Assert.That(ev.Handled, Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<StackComponent>(incomingOre).Count, Is.EqualTo(30));
                Assert.That(entMan.GetComponent<LavalandRedeemedOreComponent>(incomingOre).ProcessedUnits, Is.EqualTo(30));
                Assert.That(entMan.GetComponent<LavalandRedeemedOreComponent>(incomingOre).CreditedUnits, Is.EqualTo(0));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PartialStorageMergeDoesNotRedeemLeftoverOre()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var storage = server.System<StorageSystem>();
        var stack = server.System<StackSystem>();

        EntityUid existingOre = default;
        EntityUid incomingOre = default;

        await server.WaitPost(() =>
        {
            var oreBox = entMan.SpawnEntity("OreBox", MapCoordinates.Nullspace);
            var oreBag = entMan.SpawnEntity("OreBag", MapCoordinates.Nullspace);
            existingOre = SpawnOre(entMan, stack, SteelOre, 20);
            incomingOre = SpawnOre(entMan, stack, SteelOre, 30);

            Assert.That(storage.Insert(oreBox, existingOre, out _, playSound: false), Is.True);
            Assert.That(storage.Insert(oreBag, incomingOre, out _, playSound: false), Is.True);

            BlockNewStorageItems(entMan, oreBox);

            var ev = RedeemBagIntoBox(entMan, oreBag, oreBox);

            Assert.That(ev.Handled, Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<StackComponent>(existingOre).Count, Is.EqualTo(30));
                Assert.That(entMan.GetComponent<StackComponent>(incomingOre).Count, Is.EqualTo(20));
                Assert.That(entMan.GetComponent<LavalandRedeemedOreComponent>(existingOre).ProcessedUnits, Is.EqualTo(10));
                Assert.That(entMan.GetComponent<LavalandRedeemedOreComponent>(existingOre).CreditedUnits, Is.EqualTo(0));
                AssertUnredeemed(entMan, incomingOre);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FullStorageDoesNotRedeemUninsertedOre()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var storage = server.System<StorageSystem>();
        var stack = server.System<StackSystem>();

        EntityUid existingOre = default;
        EntityUid incomingOre = default;

        await server.WaitPost(() =>
        {
            var oreBox = entMan.SpawnEntity("OreBox", MapCoordinates.Nullspace);
            var oreBag = entMan.SpawnEntity("OreBag", MapCoordinates.Nullspace);
            existingOre = SpawnOre(entMan, stack, SteelOre, 30);
            incomingOre = SpawnOre(entMan, stack, SteelOre, 30);

            Assert.That(storage.Insert(oreBox, existingOre, out _, playSound: false), Is.True);
            Assert.That(storage.Insert(oreBag, incomingOre, out _, playSound: false), Is.True);

            BlockNewStorageItems(entMan, oreBox);

            var ev = RedeemBagIntoBox(entMan, oreBag, oreBox);

            Assert.That(ev.Handled, Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<StackComponent>(existingOre).Count, Is.EqualTo(30));
                Assert.That(entMan.GetComponent<StackComponent>(incomingOre).Count, Is.EqualTo(30));
                AssertUnredeemed(entMan, existingOre);
                AssertUnredeemed(entMan, incomingOre);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DifferentOreWithoutSlotDoesNotRedeemUninsertedOre()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var storage = server.System<StorageSystem>();
        var stack = server.System<StackSystem>();

        EntityUid existingOre = default;
        EntityUid incomingOre = default;

        await server.WaitPost(() =>
        {
            var oreBox = entMan.SpawnEntity("OreBox", MapCoordinates.Nullspace);
            var oreBag = entMan.SpawnEntity("OreBag", MapCoordinates.Nullspace);
            existingOre = SpawnOre(entMan, stack, SteelOre, 20);
            incomingOre = SpawnOre(entMan, stack, GoldOre, 30);

            Assert.That(storage.Insert(oreBox, existingOre, out _, playSound: false), Is.True);
            Assert.That(storage.Insert(oreBag, incomingOre, out _, playSound: false), Is.True);

            BlockNewStorageItems(entMan, oreBox);

            var ev = RedeemBagIntoBox(entMan, oreBag, oreBox);

            Assert.That(ev.Handled, Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<StackComponent>(existingOre).Count, Is.EqualTo(20));
                Assert.That(entMan.GetComponent<StackComponent>(incomingOre).Count, Is.EqualTo(30));
                AssertUnredeemed(entMan, existingOre);
                AssertUnredeemed(entMan, incomingOre);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PartialStorageMergeKeepsRedeemedStateOnInsertedUnitsOnly()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var storage = server.System<StorageSystem>();
        var stack = server.System<StackSystem>();

        EntityUid existingOre = default;
        EntityUid incomingOre = default;

        await server.WaitPost(() =>
        {
            var oreBox = entMan.SpawnEntity("OreBox", MapCoordinates.Nullspace);
            var oreBag = entMan.SpawnEntity("OreBag", MapCoordinates.Nullspace);
            existingOre = SpawnOre(entMan, stack, SteelOre, 15);
            incomingOre = SpawnOre(entMan, stack, SteelOre, 30);

            var redeemed = entMan.EnsureComponent<LavalandRedeemedOreComponent>(incomingOre);
            redeemed.ProcessedUnits = 10;
            redeemed.CreditedUnits = 10;

            Assert.That(storage.Insert(oreBox, existingOre, out _, playSound: false), Is.True);
            Assert.That(storage.Insert(oreBag, incomingOre, out _, playSound: false), Is.True);

            BlockNewStorageItems(entMan, oreBox);

            var ev = RedeemBagIntoBox(entMan, oreBag, oreBox);

            Assert.That(ev.Handled, Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<StackComponent>(existingOre).Count, Is.EqualTo(30));
                Assert.That(entMan.GetComponent<StackComponent>(incomingOre).Count, Is.EqualTo(15));
                Assert.That(entMan.GetComponent<LavalandRedeemedOreComponent>(existingOre).ProcessedUnits, Is.EqualTo(15));
                Assert.That(entMan.GetComponent<LavalandRedeemedOreComponent>(existingOre).CreditedUnits, Is.EqualTo(10));
                AssertUnredeemed(entMan, incomingOre);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeconstructedProcessorOreIsPenalizedInOreBox()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var (user, card, processor, oreBox) = await SetupMiner(pair);

        await server.WaitPost(() =>
        {
            var ore = SpawnOre(entMan, server.System<StackSystem>(), SteelOre, 10);
            Assert.That(InsertInto(entMan, user, ore, processor), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            Assert.That(GetBalance(entMan, card), Is.EqualTo(10));
            entMan.EventBus.RaiseLocalEvent(processor, new MachineDeconstructedEvent());
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ejected = GetSteelOre(entMan);
            Assert.That(ejected, Is.Not.Empty);
            foreach (var ore in ejected)
                InsertInto(entMan, user, ore, oreBox);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetBalance(entMan, card), Is.EqualTo(0));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OreBoxToProcessorIsNotPenalizedUntilProcessorIsDeconstructed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var mining = server.System<LavalandMiningSystem>();
        var (user, card, processor, oreBox) = await SetupMiner(pair);

        await server.WaitPost(() =>
        {
            var ore = SpawnOre(entMan, server.System<StackSystem>(), SteelOre, 10);
            Assert.That(InsertInto(entMan, user, ore, oreBox), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            Assert.That(GetBalance(entMan, card), Is.EqualTo(10));
            mining.RedeemStorageIntoLathe(
                user,
                oreBox,
                (processor, entMan.GetComponent<LavalandOreRedeemerComponent>(processor)));
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetSteelOre(entMan), Is.Empty);
                Assert.That(GetBalance(entMan, card), Is.EqualTo(10));
            });
            entMan.EventBus.RaiseLocalEvent(processor, new MachineDeconstructedEvent());
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            foreach (var ore in GetSteelOre(entMan))
                InsertInto(entMan, user, ore, oreBox);
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            Assert.That(GetBalance(entMan, card), Is.EqualTo(0));
            mining.RedeemStorageIntoLathe(
                user,
                oreBox,
                (processor, entMan.GetComponent<LavalandOreRedeemerComponent>(processor)));
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(GetBalance(entMan, card), Is.EqualTo(0));
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<(EntityUid User, EntityUid Card, EntityUid Processor, EntityUid OreBox)> SetupMiner(
        Pair.TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid card = default;
        EntityUid processor = default;
        EntityUid oreBox = default;

        await server.WaitPost(() =>
        {
            user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            card = entMan.SpawnEntity("SalvageIDCard", map.GridCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(user, card), Is.True);

            processor = entMan.SpawnEntity("OreProcessor", map.GridCoords);
            server.System<PowerReceiverSystem>().SetNeedsPower(processor, false);
            oreBox = entMan.SpawnEntity("OreBox", map.GridCoords);
        });

        await pair.RunTicksSync(5);
        return (user, card, processor, oreBox);
    }

    private static bool InsertInto(IEntityManager entMan, EntityUid user, EntityUid used, EntityUid target)
    {
        var coordinates = entMan.GetComponent<TransformComponent>(target).Coordinates;
        var ev = new InteractUsingEvent(user, used, target, coordinates);
        entMan.EventBus.RaiseLocalEvent(target, ev);
        return ev.Handled;
    }

    private static int GetBalance(IEntityManager entMan, EntityUid card)
    {
        return entMan.GetComponent<LavalandMiningPointsComponent>(card).Balance;
    }

    private static System.Collections.Generic.List<EntityUid> GetSteelOre(IEntityManager entMan)
    {
        var ores = new System.Collections.Generic.List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<StackComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.StackTypeId == SteelOre && !entMan.IsQueuedForDeletion(uid))
                ores.Add(uid);
        }

        return ores;
    }

    private static EntityUid SpawnOre(IEntityManager entMan, StackSystem stack, string prototype, int count)
    {
        var ore = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
        stack.SetCount((ore, entMan.GetComponent<StackComponent>(ore)), count);
        return ore;
    }

    private static void AssertUnredeemed(IEntityManager entMan, EntityUid ore)
    {
        if (!entMan.TryGetComponent<LavalandRedeemedOreComponent>(ore, out var redeemed))
            return;

        Assert.Multiple(() =>
        {
            Assert.That(redeemed.ProcessedUnits, Is.EqualTo(0));
            Assert.That(redeemed.CreditedUnits, Is.EqualTo(0));
        });
    }

    private static void BlockNewStorageItems(IEntityManager entMan, EntityUid storageUid)
    {
        var storage = entMan.GetComponent<StorageComponent>(storageUid);
        storage.Grid.Clear();
        storage.OccupiedGrid.Clear();
    }

    private static InteractUsingEvent RedeemBagIntoBox(IEntityManager entMan, EntityUid oreBag, EntityUid oreBox)
    {
        var coordinates = entMan.GetComponent<TransformComponent>(oreBox).Coordinates;
        var ev = new InteractUsingEvent(oreBag, oreBag, oreBox, coordinates);
        entMan.EventBus.RaiseLocalEvent(oreBox, ev);
        return ev;
    }
}
