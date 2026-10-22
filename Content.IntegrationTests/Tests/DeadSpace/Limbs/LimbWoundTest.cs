using System.Linq;
using System.Numerics;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbWoundTest
{
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
            var strike = new LimbStrike(LimbZone.Torso, LimbMark.Point, 40f, 0.9f, 40f, true, false, false, false, false, 1f);
            wounds.Strike(human, strike);
            var heart = server.EntMan.GetComponent<LimbBodyComponent>(human).Organs[(int) LimbOrgan.Heart];

            Assert.Multiple(() =>
            {
                Assert.That(heart.Ruptured, Is.True);
                Assert.That(server.EntMan.GetComponent<LimbBodyComponent>(human).Organs[(int) LimbOrgan.Lungs].Ruptured, Is.False);
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
            var strike = new LimbStrike(LimbZone.LegLeft, LimbMark.Blunt, LimbWoundRules.BreakBone, 0f, 0f, false, false, false, false, false, 1f);
            wounds.Strike(human, strike);
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
            spec.DamageDict["Blunt"] = FixedPoint2.New(25);
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
