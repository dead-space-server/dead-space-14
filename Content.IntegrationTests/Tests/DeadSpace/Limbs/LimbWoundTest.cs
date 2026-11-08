using System.Linq;
using System.Numerics;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbWoundTest
{
    [Test]
    public async Task RegrowingAnArmAndALegBringsBackTheHandAndFoot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var body = server.EntMan.System<SharedBodySystem>();
            Assert.That(wounds.Amputate(human, LimbZone.ArmLeft), Is.True);
            Assert.That(wounds.Amputate(human, LimbZone.LegRight), Is.True);
            Assert.That(wounds.TryRegrowZone(human, LimbZone.ArmLeft, 0.3f), Is.True);
            Assert.That(wounds.TryRegrowZone(human, LimbZone.LegRight, 0.3f), Is.True);

            var limbs = server.EntMan.GetComponent<LimbBodyComponent>(human);
            Assert.Multiple(() =>
            {
                Assert.That(body.GetBodyChildrenOfType(human, BodyPartType.Hand).Count(), Is.EqualTo(2));
                Assert.That(body.GetBodyChildrenOfType(human, BodyPartType.Foot).Count(), Is.EqualTo(2));
                Assert.That(limbs.Zones[(int) LimbZone.HandLeft].Severed, Is.False);
                Assert.That(limbs.Zones[(int) LimbZone.FootRight].Severed, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RestoringADeadLimbsHealthBringsItBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var pool = LimbWoundRules.HealthFor(LimbZone.LegLeft, server.EntMan.GetComponent<LimbBodyComponent>(human));
            wounds.ApplyEdit(human, LimbZone.LegLeft, new LimbEdit { SetHealth = true, Health = 0f, SetAtrophy = true });
            wounds.ApplyEdit(human, LimbZone.LegLeft, new LimbEdit { SetHealth = true, Health = pool });

            var leg = server.EntMan.GetComponent<LimbBodyComponent>(human).Zones[(int) LimbZone.LegLeft];
            Assert.That(leg.Health, Is.EqualTo(pool));
            Assert.That(leg.Atrophied, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicineHealsTheCutLegEvenWhenTheTickAlsoBurns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var damage = server.EntMan.System<DamageableSystem>();
            var limbs = server.EntMan.GetComponent<LimbBodyComponent>(human);
            var pool = LimbWoundRules.HealthFor(LimbZone.LegLeft, limbs);
            wounds.ApplyEdit(human, LimbZone.LegLeft, new LimbEdit { SetHealth = true, Health = 20f });
            var cut = new DamageSpecifier();
            cut.DamageDict["Slash"] = FixedPoint2.New(20);
            damage.ChangeDamage(human, cut);
            var dose = new DamageSpecifier();
            dose.DamageDict["Slash"] = FixedPoint2.New(-20);
            dose.DamageDict["Heat"] = FixedPoint2.New(0.2);
            damage.ChangeDamage(human, dose);

            Assert.That(limbs.Zones[(int) LimbZone.LegLeft].Health, Is.EqualTo(pool));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AFullLimbDropsItsRecordedDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var damage = server.EntMan.System<DamageableSystem>();
            var limbs = server.EntMan.GetComponent<LimbBodyComponent>(human);
            var pool = LimbWoundRules.HealthFor(LimbZone.LegLeft, limbs);
            var hit = new DamageSpecifier();
            hit.DamageDict["Slash"] = FixedPoint2.New(20);
            limbs.LedgerZone = LimbZone.LegLeft;
            damage.ChangeDamage(human, hit);
            Assert.That(limbs.Zones[(int) LimbZone.LegLeft].HitSlash, Is.GreaterThan(0f));

            wounds.ApplyEdit(human, LimbZone.LegLeft, new LimbEdit { SetHealth = true, Health = pool });
            Assert.That(limbs.Zones[(int) LimbZone.LegLeft].HitSlash, Is.EqualTo(0f));
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(human).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AHealthyBodyCanStillMendAWornLeg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var damage = server.EntMan.System<DamageableSystem>();
            var limbs = server.EntMan.GetComponent<LimbBodyComponent>(human);
            var pool = LimbWoundRules.HealthFor(LimbZone.LegLeft, limbs);
            wounds.ApplyEdit(human, LimbZone.LegLeft, new LimbEdit { SetHealth = true, Health = pool * 0.15f });

            var pack = new DamageSpecifier();
            pack.DamageDict["Blunt"] = FixedPoint2.New(-15);
            Assert.That(server.EntMan.GetComponent<DamageableComponent>(human).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(wounds.CanMend(human, pack), Is.True);

            var hit = new DamageSpecifier();
            hit.DamageDict["Blunt"] = FixedPoint2.New(5);
            damage.ChangeDamage(human, hit);
            damage.ChangeDamage(human, pack);

            Assert.That(limbs.Zones[(int) LimbZone.LegLeft].Health, Is.EqualTo(pool));
            Assert.That(wounds.CanMend(human, pack), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("MobHuman")]
    [TestCase("MobSlimePerson")]
    [TestCase("MobIPC")]
    public async Task RoundstartMindShieldFindsTheBrain(string mob)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = server.EntMan.SpawnEntity(mob, new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.That(server.EntMan.System<Content.Shared.Implants.SharedSubdermalImplantSystem>().AddImplant(body, "MindShieldImplant"), Is.Not.Null);
            Assert.That(server.EntMan.HasComponent<Content.Shared.Mindshield.Components.MindShieldComponent>(body), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoLegsBlocksStandingUpFromKnockdown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var attempt = new StandUpAttemptEvent(true);
            server.EntMan.EventBus.RaiseLocalEvent(human, ref attempt);
            Assert.That(attempt.Cancelled, Is.False);

            Assert.That(wounds.Amputate(human, LimbZone.LegLeft), Is.True);
            Assert.That(wounds.Amputate(human, LimbZone.LegRight), Is.True);
            attempt = new StandUpAttemptEvent(true);
            server.EntMan.EventBus.RaiseLocalEvent(human, ref attempt);
            Assert.That(attempt.Cancelled, Is.True);
            Assert.That(attempt.Autostand, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HumanSpawnsWithLimbState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<LimbBodyComponent>(human), Is.True);
                Assert.That(server.EntMan.HasComponent<LimbTargetComponent>(human), Is.True);
                Assert.That(server.EntMan.HasComponent<LimbAimComponent>(human), Is.True);
                Assert.That(server.EntMan.GetComponent<LimbBodyComponent>(human).Zones, Has.Count.EqualTo(LimbWoundRules.BodyZoneCount));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EdgeStrikeSeversTheLeftHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var body = server.EntMan.System<SharedBodySystem>();
            var before = body.GetBodyChildrenOfType(human, BodyPartType.Hand).Count();
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var strike = new LimbStrike(LimbZone.HandLeft, LimbMark.Edge, 40f, 0f, 0f, false, false, false, false, false, 1f, Sharp: true);
            var result = wounds.Strike(human, strike);

            Assert.Multiple(() =>
            {
                Assert.That(before, Is.EqualTo(2));
                Assert.That(result.Severed, Is.True);
                Assert.That(body.GetBodyChildrenOfType(human, BodyPartType.Hand).Count(), Is.EqualTo(1));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PointStrikeRupturesTheHeart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var strike = new LimbStrike(LimbZone.Torso, LimbMark.Point, 99f, 0.9f, 40f, false, false, false, false, false, 1f);
            wounds.Strike(human, strike);
            var organs = server.EntMan.GetComponent<LimbBodyComponent>(human).Organs;

            Assert.Multiple(() =>
            {
                Assert.That(organs[(int) LimbOrgan.Heart].Ruptured, Is.True);
                Assert.That(organs[(int) LimbOrgan.Lungs].Ruptured, Is.True);
                Assert.That(organs[(int) LimbOrgan.Kidneys].Ruptured, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdrenalineRemovesTheLimp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            wounds.ApplyEdit(human, LimbZone.LegLeft, new LimbEdit { SetBone = true, Bone = LimbWoundRules.BreakBone });
            Assert.That(wounds.HasLimp(human), Is.True);

            var status = server.EntMan.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(human, "StatusEffectAdrenaline", out _, TimeSpan.FromSeconds(30)), Is.True);
            Assert.That(wounds.HasLimp(human), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GauzeBandagesTheBleedingZone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            wounds.SetTarget(human, LimbZone.ArmLeft);
            var strike = new LimbStrike(LimbZone.ArmLeft, LimbMark.Edge, 30f, 0f, 0f, false, false, false, false, false, 1f);
            wounds.Strike(human, strike);
            var before = wounds.BleedTotal(human);
            wounds.Bandage(human, LimbZone.ArmLeft);

            Assert.That(wounds.BleedTotal(human), Is.LessThan(before));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImplantInCombatSurgesAndACivilianDoesNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var fighter = server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            var civilian = server.EntMan.SpawnEntity("MobHuman", map.MapCoords);
            server.EntMan.EnsureComponent<CombatAdrenalineImplantComponent>(fighter);
            server.EntMan.System<SharedCombatModeSystem>().SetInCombatMode(fighter, true);

            var damage = server.EntMan.System<DamageableSystem>();
            var spec = new DamageSpecifier();
            spec.DamageDict["Blunt"] = FixedPoint2.New(30);
            damage.ChangeDamage(fighter, spec);
            damage.ChangeDamage(civilian, spec);

            var status = server.EntMan.System<StatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.TryGetStatusEffect(fighter, "StatusEffectCombatAdrenaline", out _), Is.True);
                Assert.That(status.TryGetStatusEffect(civilian, "StatusEffectCombatAdrenaline", out _), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LosingAnArmAndALegKeepsYouConscious()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var human = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            human = server.EntMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            foreach (var zone in new[] { LimbZone.ArmLeft, LimbZone.LegLeft })
            {
                var cut = false;
                for (var i = 0; i < 12 && !cut; i++)
                    cut = wounds.Strike(human, new LimbStrike(zone, LimbMark.Edge, 40f, 0f, 0f, false, false, false, false, false, 1f, Sharp: true)).Severed;
                Assert.That(cut, Is.True, zone.ToString());
            }
        });

        await server.WaitRunTicks(600);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.System<MobStateSystem>().IsAlive(human), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ADeadArmTakesAndPassesNoDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var dead = server.EntMan.SpawnEntity("MobHuman", at);
            var alive = server.EntMan.SpawnEntity("MobHuman", at);
            var wounds = server.EntMan.System<LimbWoundSystem>();
            var damage = server.EntMan.System<DamageableSystem>();
            var now = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime;
            wounds.ApplyEdit(dead, LimbZone.ArmLeft, new LimbEdit { SetAtrophy = true });

            var hit = new DamageSpecifier();
            hit.DamageDict["Slash"] = FixedPoint2.New(20);
            foreach (var body in new[] { dead, alive })
            {
                var limbs = server.EntMan.GetComponent<LimbBodyComponent>(body);
                limbs.ArmorZone = LimbZone.ArmLeft;
                limbs.FireOriginTime = now;
                damage.ChangeDamage(body, hit);
                limbs.ArmorZone = null;
            }

            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(dead).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(server.EntMan.GetComponent<DamageableComponent>(alive).TotalDamage, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(wounds.BleedTotal(dead), Is.EqualTo(0f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FireDoesNotDeleteClothes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var suit = EntityUid.Invalid;
        var vest = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var human = server.EntMan.SpawnEntity("MobHuman", at);
            var inventory = server.EntMan.System<InventorySystem>();
            suit = server.EntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", at);
            vest = server.EntMan.SpawnEntity("ClothingOuterArmorBasic", at);
            Assert.That(inventory.TryEquip(human, suit, "jumpsuit", silent: true, force: true), Is.True);
            Assert.That(inventory.TryEquip(human, vest, "outerClothing", silent: true, force: true), Is.True);

            var limbs = server.EntMan.GetComponent<LimbBodyComponent>(human);
            var zones = limbs.Zones;
            var torso = zones[LimbWoundRules.Index(LimbZone.Torso)];
            torso.Fire = 10f;
            zones[LimbWoundRules.Index(LimbZone.Torso)] = torso;
            server.EntMan.EnsureComponent<LimbActiveComponent>(human);
        });

        await server.WaitRunTicks(300);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.Deleted(suit), Is.False, "the jumpsuit does not burn away");
                Assert.That(server.EntMan.Deleted(vest), Is.False, "the armor vest does not burn");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RigKeepsLimbsOn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var at = new MapCoordinates(Vector2.Zero, map.MapId);
            var human = server.EntMan.SpawnEntity("MobHuman", at);
            var wounds = server.EntMan.System<LimbWoundSystem>();
            Assert.That(wounds.SeverProof(human, LimbZone.LegLeft), Is.False);

            var suit = server.EntMan.SpawnEntity("ClothingOuterRIGSecurity", at);
            Assert.That(server.EntMan.System<InventorySystem>().TryEquip(human, suit, "outerClothing", silent: true, force: true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(wounds.SeverProof(human, LimbZone.LegLeft), Is.True);
                Assert.That(wounds.SeverProof(human, LimbZone.HandRight), Is.True);
                Assert.That(wounds.SeverProof(human, LimbZone.Head), Is.False, "the helmet is still in the suit");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CoversMatchTheClothes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var factory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            EntProtoId gas = "ClothingMaskGas";
            EntProtoId breath = "ClothingMaskBreath";
            EntProtoId helmet = "ClothingHeadHelmetBasic";
            EntProtoId swat = "ClothingHeadHelmetSwat";
            EntProtoId vestId = "ClothingOuterArmorBulletproof";
            AssertCover(proto, factory, gas, true, false, false);
            Assert.That(proto.Index(breath).TryGetComponent<LimbCoverComponent>(out _, factory), Is.False);
            AssertCover(proto, factory, helmet, false, true, false);
            AssertCover(proto, factory, swat, true, true, false);
            AssertCover(proto, factory, vestId, false, false, false);
            var vest = Cover(proto, factory, vestId);
            Assert.That(vest.Zones, Does.Contain(LimbZone.Torso));
            Assert.That(vest.WoundMultiplier, Is.EqualTo(0.55f));
            EntProtoId gloveId = "ClothingHandsGlovesCombat";
            EntProtoId suitId = "ClothingOuterSuitAtmosFire";
            EntProtoId shieldId = "RiotShield";
            EntProtoId metalId = "RiotBulletShield";
            var gloves = Cover(proto, factory, gloveId);
            Assert.That(gloves.Grip, Is.True);
            var suit = Cover(proto, factory, suitId);
            Assert.That(suit.StopsFire, Is.True);
            var shield = Cover(proto, factory, shieldId);
            Assert.That(shield.OnlyWhenBlocking, Is.True);
            Assert.That(shield.Ricochet, Is.False);
            var metal = Cover(proto, factory, metalId);
            Assert.That(metal.Ricochet, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AGhostKeepsNoLimbAim()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var ghost = server.EntMan.SpawnEntity("MobObserver", new MapCoordinates(Vector2.Zero, map.MapId));
            var wounds = server.EntMan.System<LimbWoundSystem>();
            wounds.SetTarget(ghost, LimbZone.Head);
            wounds.SetAiming(ghost, true);
            Assert.That(server.EntMan.HasComponent<LimbTargetComponent>(ghost), Is.False);
            Assert.That(server.EntMan.HasComponent<LimbAimComponent>(ghost), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertCover(IPrototypeManager proto, IComponentFactory factory, EntProtoId id, bool face, bool ricochet, bool grip)
    {
        var cover = Cover(proto, factory, id);
        Assert.Multiple(() =>
        {
            Assert.That(cover.Face, Is.EqualTo(face), id.Id);
            Assert.That(cover.Ricochet, Is.EqualTo(ricochet), id.Id);
            Assert.That(cover.Grip, Is.EqualTo(grip), id.Id);
        });
    }

    private static LimbCoverComponent Cover(IPrototypeManager proto, IComponentFactory factory, EntProtoId id)
    {
        LimbCoverComponent cover = null;
        Assert.That(proto.Index(id).TryGetComponent(out cover, factory), Is.True, id.Id);
        return cover;
    }
}
