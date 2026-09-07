using System.Collections.Generic;
using System.Linq;
using Content.Server.Body.Components;
using Content.Server.DeadSpace.AshWalkers;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Languages.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class AshWalkerTest
{
    private const string HostileFaction = "SimpleHostile";
    private const string TribeFaction = "AshWalker";

    [Test]
    public async Task EggsRequireLavalandAndSpawnOneTribeMemberEach()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var roles = server.System<GhostRoleSystem>();
        var minds = server.System<SharedMindSystem>();
        var maps = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();
        var inventory = server.System<InventorySystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var session = server.PlayerMan.Sessions.Single();
        var home = await pair.CreateTestMap();
        var other = await pair.CreateTestMap();
        var members = new HashSet<EntityUid>();
        var memberMinds = new HashSet<EntityUid>();
        var eggs = new List<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var ghost = entMan.SpawnEntity("MobObserver", home.GridCoords);
            var mind = minds.CreateMind(session.UserId);
            minds.TransferTo(mind, ghost);

            var egg = entMan.SpawnEntity("AshWalkerEgg", home.GridCoords);
            var id = entMan.GetComponent<GhostRoleComponent>(egg).Identifier;
            Assert.That(roles.GetGhostRolesInfo(session).Any(role => role.Identifier == id), Is.False);
            Assert.That(roles.Takeover(session, id), Is.False);

            entMan.AddComponent<LavalandMapComponent>(home.MapUid).Planet = "Lavaland";
            cfg.SetCVar(CCCCVars.AshWalkersEnabled, false);
            Assert.That(roles.GetGhostRolesInfo(session).Any(role => role.Identifier == id), Is.False);
            Assert.That(roles.Takeover(session, id), Is.False);

            cfg.SetCVar(CCCCVars.AshWalkersEnabled, true);
            maps.SetPaused(home.MapUid, true);
            Assert.That(roles.GetGhostRolesInfo(session).Any(role => role.Identifier == id), Is.False);
            Assert.That(roles.Takeover(session, id), Is.False);
            maps.SetPaused(home.MapUid, false);

            transform.SetCoordinates(egg, other.GridCoords);
            Assert.That(roles.Takeover(session, id), Is.False);
            transform.SetCoordinates(egg, home.GridCoords);

            var destroyed = entMan.SpawnEntity("AshWalkerEgg", home.GridCoords);
            var destroyedId = entMan.GetComponent<GhostRoleComponent>(destroyed).Identifier;
            entMan.QueueDeleteEntity(destroyed);
            Assert.That(roles.GetGhostRolesInfo(session).Any(role => role.Identifier == destroyedId), Is.False);
            Assert.That(roles.Takeover(session, destroyedId), Is.False);

            eggs.Add(egg);
            eggs.Add(entMan.SpawnEntity("AshWalkerEgg", home.GridCoords));
            eggs.Add(entMan.SpawnEntity("AshWalkerEgg", home.GridCoords));

            foreach (var current in eggs)
            {
                id = entMan.GetComponent<GhostRoleComponent>(current).Identifier;
                Assert.That(roles.GetGhostRolesInfo(session).Any(role => role.Identifier == id), Is.True);
                Assert.That(roles.Takeover(session, id), Is.True);
                Assert.That(roles.Takeover(session, id), Is.False);

                Assert.That(session.AttachedEntity, Is.Not.Null);
                var member = session.AttachedEntity!.Value;
                Assert.That(members.Add(member), Is.True);
                Assert.That(entMan.HasComponent<AshWalkerComponent>(member), Is.True);
                Assert.That(entMan.GetComponent<AshWalkerTribeMemberComponent>(member).HomeMap,
                    Is.EqualTo(home.MapUid));
                Assert.That(minds.TryGetMind(member, out var spawnedMind, out _), Is.True);
                Assert.That(memberMinds.Add(spawnedMind), Is.True);
                Assert.That(entMan.HasComponent<RespiratorComponent>(member), Is.False);
                Assert.That(entMan.HasComponent<InternalsComponent>(member), Is.False);

                var language = entMan.GetComponent<LanguageComponent>(member);
                Assert.That(language.KnownLanguages.Select(value => value.Id),
                    Is.EquivalentTo(new[] { "ReptilianLanguage" }));
                Assert.That(language.SelectedLanguage, Is.EqualTo("ReptilianLanguage"));
                Assert.That(inventory.TryGetSlotEntity(member, "jumpsuit", out var uniform), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(uniform!.Value).EntityPrototype!.ID,
                    Is.EqualTo("ClothingUniformAshWalker"));
                Assert.That(inventory.TryGetSlotEntity(member, "back", out var bag), Is.True);
                Assert.That(entMan.GetComponent<StorageComponent>(bag!.Value).Container.ContainedEntities,
                    Has.Count.EqualTo(6));

                transform.SetCoordinates(member, other.GridCoords);
                Assert.That(entMan.GetComponent<AshWalkerTribeMemberComponent>(member).HomeMap,
                    Is.EqualTo(home.MapUid));
            }
        });

        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(eggs.All(entMan.Deleted), Is.True);
            Assert.That(members.Count, Is.EqualTo(3));
            Assert.That(memberMinds.Count, Is.EqualTo(3));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RestrictionsAllowBowsAndLeaveOrdinaryReptiliansUnchanged()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var inventory = server.System<InventorySystem>();
        var hands = server.System<SharedHandsSystem>();
        var wielding = server.System<SharedWieldableSystem>();
        var factions = server.System<NpcFactionSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var walker = entMan.SpawnEntity("MobAshWalker", map.GridCoords);
            var reptilian = entMan.SpawnEntity("MobReptilian", map.GridCoords);
            var shoes = entMan.SpawnEntity("ClothingShoesColorBlack", map.GridCoords);
            var gun = entMan.SpawnEntity("WeaponPistolMk58", map.GridCoords);
            var bow = entMan.SpawnEntity("BowImprovised", map.GridCoords);

            Assert.That(inventory.CanEquip(walker, shoes, "shoes", out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("ash-walker-cannot-equip-footwear"));
            Assert.That(inventory.CanEquip(reptilian, walker, shoes, "shoes", out reason), Is.False);
            Assert.That(reason, Is.EqualTo("ash-walker-cannot-equip-footwear"));
            Assert.That(inventory.CanEquip(reptilian, shoes, "shoes", out _), Is.True);
            Assert.That(entMan.HasComponent<RespiratorComponent>(reptilian), Is.True);
            Assert.That(entMan.HasComponent<AshWalkerComponent>(reptilian), Is.False);

            var shot = new AttemptShootEvent(walker, null);
            entMan.EventBus.RaiseLocalEvent(gun, ref shot);
            Assert.That(shot.Cancelled, Is.True);
            shot = new AttemptShootEvent(reptilian, null);
            entMan.EventBus.RaiseLocalEvent(gun, ref shot);
            Assert.That(shot.Cancelled, Is.False);

            Assert.That(hands.GetActiveItem(walker), Is.Not.Null);
            Assert.That(hands.TryDrop(walker, hands.GetActiveItem(walker)!.Value), Is.True);
            Assert.That(hands.TryPickup(walker, bow), Is.True);
            Assert.That(wielding.TryWield(bow, entMan.GetComponent<WieldableComponent>(bow), walker), Is.True);
            shot = new AttemptShootEvent(walker, null);
            entMan.EventBus.RaiseLocalEvent(bow, ref shot);
            Assert.That(shot.Cancelled, Is.False);
            Assert.That(factions.IsFactionHostile(HostileFaction, TribeFaction), Is.True);

            var armor = entMan.SpawnEntity("ClothingOuterArmorBoneAshWalker", map.GridCoords);
            Assert.That(inventory.CanEquip(walker, armor, "outerClothing", out _), Is.True);
            Assert.That(inventory.CanEquip(reptilian, armor, "outerClothing", out reason), Is.False);
            Assert.That(reason, Is.EqualTo("ash-walker-clothing-cannot-equip"));
            Assert.That(inventory.CanEquip(walker, reptilian, armor, "outerClothing", out reason), Is.False);
            Assert.That(reason, Is.EqualTo("ash-walker-clothing-cannot-equip"));
            Assert.That(inventory.CanEquip(reptilian, walker, armor, "outerClothing", out _), Is.True);

            var damage = server.System<DamageableSystem>();
            var prey = entMan.SpawnEntity("MobLavalandGoliath", map.GridCoords);
            var hit = new DamageSpecifier { DamageDict = { ["Blunt"] = 10, ["Piercing"] = 10 } };
            Assert.That(damage.TryChangeDamage(prey, hit, out var ordinaryHit, origin: reptilian), Is.True);
            Assert.That(damage.TryChangeDamage(prey, hit, out var hunterHit, origin: walker), Is.True);
            Assert.That(hunterHit.GetTotal().Float(), Is.EqualTo(ordinaryHit.GetTotal().Float() * 2.5f).Within(0.01));
            Assert.That(hit.GetTotal().Float(), Is.EqualTo(20));
            Assert.That(damage.TryChangeDamage(reptilian, hit, out ordinaryHit, origin: reptilian), Is.True);
            Assert.That(damage.TryChangeDamage(reptilian, hit, out hunterHit, origin: walker), Is.True);
            Assert.That(hunterHit.GetTotal(), Is.EqualTo(ordinaryHit.GetTotal()));

            var healing = new DamageSpecifier { DamageDict = { ["Blunt"] = -2 } };
            Assert.That(damage.TryChangeDamage(prey, healing, out var ordinaryHealing, origin: reptilian), Is.True);
            Assert.That(damage.TryChangeDamage(prey, healing, out var hunterHealing, origin: walker), Is.True);
            Assert.That(hunterHealing.GetTotal(), Is.EqualTo(ordinaryHealing.GetTotal()));
        });

        await pair.CleanReturnAsync();
    }
}
