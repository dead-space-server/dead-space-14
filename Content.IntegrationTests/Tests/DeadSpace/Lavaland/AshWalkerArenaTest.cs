using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.DeadSpace.Lavaland;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.Lavaland;

[TestFixture]
public sealed class AshWalkerArenaTest : InteractionTest
{
    protected override string PlayerPrototype => "MobAshWalker";

    [TestCase("MobLavalandHierophant", "HierophantTrophy")]
    [TestCase("MobLavalandAshDrake", "AshDrakeTrophy")]
    [TestCase("MobLavalandBubblegum", "BubblegumTrophy")]
    [TestCase("MobLavalandColossus", "ColossusTrophy")]
    public async Task HunterCanDamageArenaBossAndDeathAwardsOneTrophy(string bossPrototype, string trophyPrototype)
    {
        await AddGravity();
        await Server.WaitPost(() =>
        {
            var tile = new Tile(TileMan["Plating"].TileId);
            for (var x = -4; x <= 4; x++)
            for (var y = -4; y <= 4; y++)
                MapSystem.SetTile(MapData.Grid, MapData.Grid.Comp, new Vector2i(x, y), tile);
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid, 0.5f, 0.5f));
            TargetCoords = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.Grid, 1.5f, 0.5f));
        });
        await SpawnTarget(bossPrototype);
        var weapon = await PlaceInHands("DaggerBloodAshWalker");
        await RunTicks(60);
        await Server.WaitAssertion(() =>
        {
            var boss = STarget!.Value;
            var bossComp = SEntMan.GetComponent<LavalandBossComponent>(boss);
            var arena = SEntMan.AddComponent<LavalandBossArenaComponent>(MapData.Grid);
            arena.Grid = MapData.Grid;
            arena.Map = MapData.MapUid;
            arena.Boss = boss;
            arena.MaxHealth = bossComp.MaxHealth;
            arena.ScaledMaxHealth = bossComp.MaxHealth;
            arena.FightStartDistance = 0.01f;
            bossComp.Arena = MapData.Grid;

            var arenas = Server.System<LavalandBossArenaSystem>();
            Assert.That(arenas.IsCountedParticipant(SPlayer, arena), Is.True);
            var damage = Server.System<DamageableSystem>();
            var hit = new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } };
            Assert.That(damage.TryChangeDamage(boss, hit, out _), Is.False);
            var before = SEntMan.GetComponent<DamageableComponent>(boss).TotalDamage;
            var weaponUid = ToServer(weapon);
            SCombatMode.SetInCombatMode(SPlayer, true);
            Assert.That(Server.System<SharedMeleeWeaponSystem>().AttemptLightAttack(SPlayer, weaponUid,
                SEntMan.GetComponent<MeleeWeaponComponent>(weaponUid), boss), Is.True);
            Assert.That(SEntMan.GetComponent<DamageableComponent>(boss).TotalDamage, Is.GreaterThan(before));
            Assert.That(arena.FightStarted, Is.True);

            Server.System<MobStateSystem>().ChangeMobState(boss, MobState.Dead);
            var duplicateDeath = new MobStateChangedEvent(boss, SEntMan.GetComponent<MobStateComponent>(boss),
                MobState.Alive, MobState.Dead);
            SEntMan.EventBus.RaiseLocalEvent(boss, duplicateDeath);
            Assert.That(bossComp.DeathRewardsSpawned, Is.True);
            Assert.That(arena.Ended, Is.True);
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>().Count(meta => meta.EntityPrototype?.ID == trophyPrototype), Is.EqualTo(1));
        });
        var deadBoss = STarget!.Value;
        await RunTicks(5);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(deadBoss), Is.True));
    }
}
