using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeadSpace.AshWalkers;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Temperature.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.Destructible;
using Content.Shared.DragDrop;
using Content.Shared.Inventory;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Storage;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class AshWalkerNestTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: AshWalkerNest
          id: TestAshWalkerNestFast
          components:
          - type: AshWalkerNest
            sacrificeDelay: 0
            incubationTime: 10
            maxEggs: 4

        - type: entity
          parent: AshWalkerNest
          id: TestAshWalkerNestSlow
          components:
          - type: AshWalkerNest
            sacrificeDelay: 1
            flesh: 2
            maxEggs: 1
        """;

    private static void SetTribe(IEntityManager entMan, EntityUid walker, EntityCoordinates coordinates)
    {
        var egg = entMan.SpawnEntity("AshWalkerEgg", coordinates);
        entMan.EventBus.RaiseLocalEvent(walker, new GhostRoleSpawnerUsedEvent(egg, walker));
        entMan.DeleteEntity(egg);
    }

    [Test]
    public async Task SacrificePreservesEquipmentAndIncubatesOneUsableEgg()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var nests = server.System<AshWalkerNestSystem>();
        var roles = server.System<GhostRoleSystem>();
        var minds = server.System<SharedMindSystem>();
        var inventory = server.System<InventorySystem>();
        var damage = server.System<DamageableSystem>();
        var maps = server.System<SharedMapSystem>();
        var session = server.PlayerMan.Sessions.Single();
        var map = await pair.CreateTestMap();
        Entity<AshWalkerNestComponent> nest = default;
        EntityUid walker = default;
        EntityUid mind = default;
        EntityUid draggedCorpse = default;
        NetEntity netNest = default;
        NetEntity netWalker = default;
        NetEntity netCorpse = default;
        EntityUid bag = default;
        EntityUid supply = default;
        EntityUid egg = default;
        TimeSpan incubationRemaining = default;
        var originalEggs = new HashSet<EntityUid>();

        List<EntityUid> Eggs()
        {
            var result = new List<EntityUid>();
            var query = entMan.EntityQueryEnumerator<AshWalkerEggComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (!entMan.IsQueuedForDeletion(uid))
                    result.Add(uid);
            }
            return result;
        }

        await server.WaitAssertion(() =>
        {
            entMan.AddComponent<LavalandMapComponent>(map.MapUid).Planet = "Lavaland";
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            var nestUid = entMan.SpawnEntity("TestAshWalkerNestFast", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            nest = (nestUid, entMan.GetComponent<AshWalkerNestComponent>(nestUid));
            walker = entMan.SpawnEntity("MobAshWalker", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            SetTribe(entMan, walker, map.GridCoords);
            mind = minds.CreateMind(session.UserId);
            minds.TransferTo(mind, walker);
            draggedCorpse = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            damage.TryChangeDamage(draggedCorpse, new DamageSpecifier { DamageDict = { ["Blunt"] = 300 } }, true);
            bag = entMan.SpawnEntity("ClothingBackpackSatchelLeather", map.GridCoords);
            supply = entMan.SpawnEntity("Gauze", map.GridCoords);
            Assert.That(inventory.TryEquip(draggedCorpse, bag, "back", force: true), Is.True);
            Assert.That(server.System<SharedContainerSystem>().Insert(supply,
                entMan.GetComponent<StorageComponent>(bag).Container), Is.True);
            netNest = entMan.GetNetEntity(nest);
            netWalker = entMan.GetNetEntity(walker);
            netCorpse = entMan.GetNetEntity(draggedCorpse);
            for (var i = 0; i < 3; i++)
                originalEggs.Add(entMan.SpawnEntity("AshWalkerEgg", new EntityCoordinates(map.Grid, -2.5f + i, -2.5f)));
        });

        await pair.RunTicksSync(70);
        await pair.Client.WaitAssertion(() =>
        {
            var clientEntMan = pair.Client.EntMan;
            var clientNest = clientEntMan.GetEntity(netNest);
            var clientWalker = clientEntMan.GetEntity(netWalker);
            var clientCorpse = clientEntMan.GetEntity(netCorpse);
            var canDrag = new CanDragEvent();
            clientEntMan.EventBus.RaiseLocalEvent(clientCorpse, ref canDrag);
            Assert.That(canDrag.Handled, Is.True);
            var canDrop = new CanDropTargetEvent(clientWalker, clientCorpse);
            clientEntMan.EventBus.RaiseLocalEvent(clientNest, ref canDrop);
            Assert.That(canDrop.Handled && canDrop.CanDrop, Is.True);
            var invalidDrop = new CanDropTargetEvent(clientWalker, clientWalker);
            clientEntMan.EventBus.RaiseLocalEvent(clientNest, ref invalidDrop);
            Assert.That(invalidDrop.Handled, Is.True);
            Assert.That(invalidDrop.CanDrop, Is.False);
            clientEntMan.EntityNetManager.SendSystemNetworkMessage(new DragDropRequestEvent(netCorpse, netNest));
            clientEntMan.EntityNetManager.SendSystemNetworkMessage(new DragDropRequestEvent(netCorpse, netNest));
        });

        await pair.RunTicksSync(70);
        await server.WaitAssertion(() =>
        {
            Assert.That(Eggs(), Is.EquivalentTo(originalEggs));
            Assert.That(entMan.Deleted(draggedCorpse), Is.True);
            Assert.That(nest.Comp.Flesh, Is.EqualTo(1));
            for (var i = 0; i < 3; i++)
            {
                var corpse = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
                damage.TryChangeDamage(corpse, new DamageSpecifier { DamageDict = { ["Blunt"] = 300 } }, true);
                Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.True);
                Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.False);
                Assert.That(entMan.HasComponent<ButcherableComponent>(corpse), Is.False);
            }
            Assert.That(nest.Comp.Flesh, Is.EqualTo(4));
        });

        await pair.RunSeconds(1.1f);
        await server.WaitAssertion(() =>
        {
            Assert.That(Eggs(), Has.Count.EqualTo(4));
            Assert.That(nest.Comp.Flesh, Is.EqualTo(2));
            Assert.That(entMan.Deleted(bag), Is.False);
            Assert.That(entMan.GetComponent<StorageComponent>(bag).Container.Contains(supply), Is.True);
            Assert.That(server.System<SharedContainerSystem>().IsEntityInContainer(bag), Is.False);
            egg = Eggs().Single(uid => !originalEggs.Contains(uid));
            Assert.That(entMan.HasComponent<AshWalkerIncubatingEggComponent>(egg), Is.True);
            var ghost = entMan.SpawnEntity("MobObserver", map.GridCoords);
            minds.TransferTo(mind, ghost);
            var id = entMan.GetComponent<GhostRoleComponent>(egg).Identifier;
            Assert.That(roles.Takeover(session, id), Is.False);
            maps.SetPaused(map.MapUid, true);
            incubationRemaining = entMan.GetComponent<AshWalkerIncubatingEggComponent>(egg).ReadyAt -
                                  server.ResolveDependency<IGameTiming>().CurTime;
        });

        await pair.RunSeconds(11);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<AshWalkerIncubatingEggComponent>(egg), Is.True);
            maps.SetPaused(map.MapUid, false);
            Assert.That(entMan.GetComponent<AshWalkerIncubatingEggComponent>(egg).ReadyAt -
                        server.ResolveDependency<IGameTiming>().CurTime, Is.EqualTo(incubationRemaining));
        });
        await pair.RunSeconds((float) incubationRemaining.TotalSeconds + 1);
        await server.WaitAssertion(() =>
        {
            Assert.That(Eggs(), Has.Count.EqualTo(4));
            Assert.That(nest.Comp.Flesh, Is.EqualTo(2));
            Assert.That(entMan.HasComponent<AshWalkerIncubatingEggComponent>(egg), Is.False);
            var id = entMan.GetComponent<GhostRoleComponent>(egg).Identifier;
            Assert.That(roles.Takeover(session, id), Is.True);
            Assert.That(roles.Takeover(session, id), Is.False);
            Assert.That(entMan.GetComponent<AshWalkerTribeMemberComponent>(session.AttachedEntity!.Value).HomeMap,
                Is.EqualTo(map.MapUid));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NestRejectsInvalidOfferingsAndStopsWhenDestroyed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var nests = server.System<AshWalkerNestSystem>();
        var damage = server.System<DamageableSystem>();
        var transform = server.System<SharedTransformSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var map = await pair.CreateTestMap();
        Entity<AshWalkerNestComponent> nest = default;
        EntityUid walker = default;
        EntityUid corpse = default;
        EntityUid egg = default;
        EntityUid existingEgg = default;

        await server.WaitAssertion(() =>
        {
            var maps = server.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);

            entMan.AddComponent<LavalandMapComponent>(map.MapUid).Planet = "Lavaland";
            var nestUid = entMan.SpawnEntity("TestAshWalkerNestSlow", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            nest = (nestUid, entMan.GetComponent<AshWalkerNestComponent>(nestUid));
            existingEgg = entMan.SpawnEntity("AshWalkerEgg", new EntityCoordinates(map.Grid, -1.5f, 0.5f));
            walker = entMan.SpawnEntity("MobAshWalker", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            SetTribe(entMan, walker, map.GridCoords);
            corpse = entMan.SpawnEntity("MobHuman", new EntityCoordinates(map.Grid, 0.5f, 1.5f));
            Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.False);
            damage.TryChangeDamage(corpse, new DamageSpecifier { DamageDict = { ["Blunt"] = 300 } }, true);
            entMan.AddComponent<KitchenSpikeVictimComponent>(corpse);
            Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.False);
            entMan.RemoveComponent<KitchenSpikeVictimComponent>(corpse);
            entMan.AddComponent<LavalandLegionInfestedComponent>(corpse);
            Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.False);
            entMan.RemoveComponent<LavalandLegionInfestedComponent>(corpse);
            cfg.SetCVar(CCCCVars.AshWalkersEnabled, false);
            Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.False);
            cfg.SetCVar(CCCCVars.AshWalkersEnabled, true);
            Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.True);
            transform.SetCoordinates(walker, new EntityCoordinates(map.Grid, 3.5f, 0.5f));
        });

        await pair.RunTicksSync(100);
        await server.WaitAssertion(() =>
        {
            Assert.That(nest.Comp.Flesh, Is.EqualTo(2));
            Assert.That(entMan.Deleted(corpse), Is.False);
            entMan.DeleteEntity(existingEgg);
        });

        await pair.RunTicksSync(70);
        await server.WaitAssertion(() =>
        {
            Assert.That(nest.Comp.Flesh, Is.Zero);
            transform.SetCoordinates(walker, new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            foreach (var prototype in new[] { "MobAshWalker", "MobMouse", "MobLavalandHivelordBrood" })
            {
                var invalid = entMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, 0.5f, 1.5f));
                damage.TryChangeDamage(invalid, new DamageSpecifier { DamageDict = { ["Blunt"] = 1000 } }, true);
                Assert.That(nests.TryStartSacrifice(nest, walker, invalid), Is.False, prototype);
                entMan.DeleteEntity(invalid);
            }
            var eggs = entMan.EntityQueryEnumerator<AshWalkerIncubatingEggComponent>();
            Assert.That(eggs.MoveNext(out egg, out _), Is.True);
            Assert.That(nests.TryStartSacrifice(nest, walker, corpse), Is.True);
            server.System<SharedDestructibleSystem>().DestroyEntity(nest.Owner);
            Assert.That(entMan.IsQueuedForDeletion(egg), Is.True);
        });

        await pair.RunTicksSync(100);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(egg), Is.True);
            Assert.That(entMan.Deleted(corpse), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeProtectionPreventsBarotraumaAndPreservesDamageVulnerability()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var barotrauma = server.System<BarotraumaSystem>();
        var temperature = server.System<TemperatureSystem>();
        var damage = server.System<DamageableSystem>();
        var map = await pair.CreateTestMap();
        EntityUid walker = default;
        EntityUid reptilian = default;

        await server.WaitAssertion(() =>
        {
            walker = entMan.SpawnEntity("MobAshWalker", map.GridCoords);
            reptilian = entMan.SpawnEntity("MobReptilian", map.GridCoords);
            temperature.ForceChangeTemperature(walker, 300);
            var protection = entMan.GetComponent<BarotraumaComponent>(walker);
            Assert.That(barotrauma.GetFeltLowPressure(walker, protection, 0), Is.GreaterThan(90));
            Assert.That(barotrauma.GetFeltHighPressure(walker, protection, 10000), Is.LessThan(110));
            Assert.That(barotrauma.GetFeltLowPressure(reptilian,
                entMan.GetComponent<BarotraumaComponent>(reptilian), 0), Is.Zero);
        });

        await pair.RunTicksSync(180);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(walker).TotalDamage.Float(), Is.Zero);
            damage.TryChangeDamage(walker, new DamageSpecifier { DamageDict = { ["Blunt"] = 170 } }, true);
            Assert.That(entMan.GetComponent<MobStateComponent>(walker).CurrentState, Is.EqualTo(MobState.Alive));
            damage.TryChangeDamage(walker, new DamageSpecifier { DamageDict = { ["Heat"] = 10 } });
            Assert.That(entMan.GetComponent<DamageableComponent>(walker).Damage.DamageDict["Heat"].Float(), Is.EqualTo(5));
            damage.TryChangeDamage(walker, new DamageSpecifier { DamageDict = { ["Blunt"] = 50 } }, true);
            Assert.That(entMan.GetComponent<MobStateComponent>(walker).CurrentState, Is.EqualTo(MobState.Critical));
            damage.TryChangeDamage(walker, new DamageSpecifier { DamageDict = { ["Blunt"] = 130 } }, true);
            Assert.That(entMan.GetComponent<MobStateComponent>(walker).CurrentState, Is.EqualTo(MobState.Dead));
        });
        await pair.CleanReturnAsync();
    }
}
