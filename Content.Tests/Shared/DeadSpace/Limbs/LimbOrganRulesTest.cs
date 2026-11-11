using System;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbOrganRulesTest
{
    [Test]
    public void ChestHitsPickOneOrgan()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Target(LimbZone.Groin, LimbMark.Point), Is.EqualTo(LimbOrgan.Kidneys));
            Assert.That(Target(LimbZone.Groin, LimbMark.Edge), Is.EqualTo(LimbOrgan.Kidneys));
            Assert.That(Target(LimbZone.Torso, LimbMark.Point), Is.EqualTo(LimbOrgan.Heart));
            Assert.That(Target(LimbZone.Torso, LimbMark.Edge), Is.EqualTo(LimbOrgan.Lungs));
            Assert.That(Target(LimbZone.Torso, LimbMark.Blunt), Is.EqualTo(LimbOrgan.Kidneys));
            Assert.That(Target(LimbZone.Head, LimbMark.Point), Is.EqualTo(LimbOrgan.Brain));
            Assert.That(Target(LimbZone.Head, LimbMark.Blunt), Is.EqualTo(LimbOrgan.Brain));
            Assert.That(Target(LimbZone.Eyes, LimbMark.Point), Is.EqualTo(LimbOrgan.Eyes));
            Assert.That(Target(LimbZone.Eyes, LimbMark.Blunt), Is.EqualTo(LimbOrgan.Eyes));
            Assert.That(Target(LimbZone.Mouth, LimbMark.Liquid), Is.EqualTo(LimbOrgan.Liver));
            Assert.That(LimbOrganRules.TryTarget(LimbZone.Torso, LimbMark.Heat, out _), Is.False);
            Assert.That(Target(LimbZone.Torso, LimbMark.Liquid), Is.EqualTo(LimbOrgan.Core));
            Assert.That(LimbOrganRules.TryTarget(LimbZone.HandLeft, LimbMark.Edge, out _), Is.False);
        });
    }

    [Test]
    public void EnoughPointRupturesTheHeartAndAShortfallDoesNot()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        var early = LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Point, 39f, 1f);
        Assert.That(early.Ruptured, Is.False);
        Assert.That(organs[(int) LimbOrgan.Heart].Damage, Is.EqualTo(39f));

        var burst = LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Point, 1f, 1f);
        Assert.Multiple(() =>
        {
            Assert.That(burst.Ruptured, Is.False);
            Assert.That(burst.Organ, Is.EqualTo(LimbOrgan.Heart));
            Assert.That(organs[(int) LimbOrgan.Heart].Damage, Is.EqualTo(40f));
            Assert.That(organs[(int) LimbOrgan.Lungs].Damage, Is.EqualTo(40f * 0.8f));
        });
    }

    [Test]
    public void SlashTearsLungsAndAStabTearsTheBrain()
    {
        var lungs = LimbOrganRules.CreateAll().ToArray();
        var brain = LimbOrganRules.CreateAll().ToArray();
        LimbOrganRules.Apply(lungs, LimbZone.Torso, LimbMark.Edge, 80f, 1f);
        LimbOrganRules.Apply(brain, LimbZone.Head, LimbMark.Point, 40f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(lungs[(int) LimbOrgan.Lungs].Ruptured, Is.False);
            Assert.That(lungs[(int) LimbOrgan.Lungs].Damage, Is.EqualTo(80f * 0.8f));
            Assert.That(brain[(int) LimbOrgan.Brain].Ruptured, Is.False);
            Assert.That(brain[(int) LimbOrgan.Brain].Damage, Is.EqualTo(40f * 1.25f));
        });
    }

    [Test]
    public void BluntBruisesKidneysHarderThanTheStomach()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        var hit = LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Blunt, 40f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(hit.Organ, Is.EqualTo(LimbOrgan.Kidneys));
            Assert.That(organs[(int) LimbOrgan.Kidneys].Ruptured, Is.False);
            Assert.That(organs[(int) LimbOrgan.Kidneys].Damage, Is.EqualTo(40f * 0.85f));
            Assert.That(organs[(int) LimbOrgan.Stomach].Damage, Is.EqualTo(40f * 0.4f));
            Assert.That(organs[(int) LimbOrgan.Liver].Damage, Is.EqualTo(0f));
        });
    }

    [Test]
    public void HeatDoesNotTouchTheLiverAndAcidOnTheSkinDoesNotEither()
    {
        var heat = LimbOrganRules.CreateAll().ToArray();
        var acid = LimbOrganRules.CreateAll().ToArray();
        var swallowed = LimbOrganRules.CreateAll().ToArray();
        LimbOrganRules.Apply(heat, LimbZone.Torso, LimbMark.Heat, 80f, 1f);
        LimbOrganRules.Apply(acid, LimbZone.Torso, LimbMark.Liquid, 80f, 1f);
        var drink = LimbOrganRules.Apply(swallowed, LimbZone.Mouth, LimbMark.Liquid, 40f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(heat[(int) LimbOrgan.Liver].Damage, Is.EqualTo(0f));
            Assert.That(acid[(int) LimbOrgan.Kidneys].Damage, Is.EqualTo(0f));
            Assert.That(drink.Ruptured, Is.False);
            Assert.That(swallowed[(int) LimbOrgan.Liver].Damage, Is.EqualTo(40f));
        });
    }

    [Test]
    public void ABatonToTheHeadConcusses()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        var light = LimbOrganRules.Apply(organs, LimbZone.Head, LimbMark.Blunt, 7f, 1f);
        var heavy = LimbOrganRules.Apply(organs, LimbZone.Head, LimbMark.Blunt, 25f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(light.Concussion, Is.False);
            Assert.That(heavy.Concussion, Is.True);
            Assert.That(organs[(int) LimbOrgan.Brain].Ruptured, Is.False);
            Assert.That(organs[(int) LimbOrgan.Brain].Damage, Is.EqualTo(32f));
        });
    }

    [Test]
    public void RupturedEyesBlindOnce()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        Span<float> rolls = new float[LimbOrganRules.Count];
        var burst = LimbOrganRules.RuptureHome(organs, LimbOrganMask.Standard, LimbZone.Head, 0.9f, 1f, rolls);
        var again = LimbOrganRules.RuptureHome(organs, LimbOrganMask.Standard, LimbZone.Head, 0.9f, 1f, rolls);

        Assert.Multiple(() =>
        {
            Assert.That(burst, Is.EqualTo(LimbOrganRules.EyeRuptureDamage));
            Assert.That(organs[(int) LimbOrgan.Eyes].Ruptured, Is.True);
            Assert.That(organs[(int) LimbOrgan.Brain].Ruptured, Is.True);
            Assert.That(again, Is.EqualTo(0));
        });
    }

    [Test]
    public void ASparedRuptureRollKeepsOrgansIntact()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        var spared = new float[LimbOrganRules.Count];
        Array.Fill(spared, 1f);
        var eyes = LimbOrganRules.RuptureHome(organs, LimbOrganMask.Standard, LimbZone.Torso, 0.9f, LimbOrganRules.RuptureChance, spared);

        Assert.Multiple(() =>
        {
            Assert.That(eyes, Is.EqualTo(0));
            Assert.That(organs[(int) LimbOrgan.Heart].Ruptured, Is.False);
            Assert.That(organs[(int) LimbOrgan.Lungs].Ruptured, Is.False);
            Assert.That(organs[(int) LimbOrgan.Stomach].Ruptured, Is.False);
            Assert.That(organs[(int) LimbOrgan.Liver].Ruptured, Is.False);
        });
    }

    [Test]
    public void RuptureKeepsBleedingAndCryoDoesNotSealIt()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Point, 40f, 1f);
        LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Edge, 80f, 1f);
        LimbOrganRules.Apply(organs, LimbZone.Head, LimbMark.Point, 40f, 1f);
        Span<float> rolls = new float[LimbOrganRules.Count];
        LimbOrganRules.RuptureHome(organs, LimbOrganMask.Standard, LimbZone.Torso, 0.9f, 1f, rolls);
        LimbOrganRules.RuptureHome(organs, LimbOrganMask.Standard, LimbZone.Head, 0.9f, 1f, rolls);
        var tick = LimbOrganRules.Tick(organs);
        LimbOrganRules.Cryo(organs);

        Assert.Multiple(() =>
        {
            Assert.That(tick.Bloodloss, Is.EqualTo(LimbOrganRules.HeartBleed));
            Assert.That(tick.Asphyxiation, Is.EqualTo(LimbOrganRules.Breath(96f, true) + LimbOrganRules.BrainAir));
            Assert.That(organs[(int) LimbOrgan.Heart].Ruptured, Is.True);
            Assert.That(organs[(int) LimbOrgan.Heart].Damage, Is.EqualTo(40f));
        });
    }

    [Test]
    public void CryoLowersABruisedOrgan()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Blunt, 20f, 1f);
        LimbOrganRules.Cryo(organs);

        Assert.Multiple(() =>
        {
            Assert.That(organs[(int) LimbOrgan.Kidneys].Ruptured, Is.False);
            Assert.That(organs[(int) LimbOrgan.Kidneys].Damage, Is.EqualTo(20f * 0.85f - 15f));
        });
    }

    [Test]
    public void PuttingAnOrganBackKeepsItsRupture()
    {
        var fresh = LimbOrganRules.Carry(0f, false, false, false, 0f);
        var crack = LimbOrganRules.Carry(12f, false, false, false, 0f);
        var returned = LimbOrganRules.Carry(12f, true, false, false, 0f);
        var torn = LimbOrganRules.Carry(12f, true, true, false, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(fresh.Ruptured, Is.False);
            Assert.That(fresh.Damage, Is.EqualTo(0f));
            Assert.That(crack.Ruptured, Is.False);
            Assert.That(crack.Damage, Is.EqualTo(12f));
            Assert.That(returned.Ruptured, Is.True);
            Assert.That(returned.Damage, Is.EqualTo(LimbOrganRules.Rupture));
            Assert.That(torn.Ruptured, Is.True);
        });
    }

    [Test]
    public void ARupturedBrainIsRepairedInItsPlace()
    {
        var organs = LimbOrganRules.CreateAll();
        organs[(int) LimbOrgan.Brain] = new LimbOrganState { Ruptured = true, Damage = 40f };
        organs[(int) LimbOrgan.Core] = new LimbOrganState { Ruptured = true, Damage = 40f };

        Assert.Multiple(() =>
        {
            Assert.That(SurgeryConditions.MindTorn(organs, LimbZone.Head, LimbOrganMask.Standard), Is.True);
            Assert.That(SurgeryConditions.MindTorn(organs, LimbZone.Torso, LimbOrganMask.Standard), Is.False);
            Assert.That(SurgeryConditions.MindTorn(organs, LimbZone.Torso, LimbOrganMask.Core), Is.True);
            Assert.That(SurgeryConditions.Has("MindRupture", new LimbZoneState(), true, false, mind: true), Is.True);
            Assert.That(SurgeryConditions.Has("MindRupture", new LimbZoneState(), true, false), Is.False);
        });
    }

    private static LimbOrgan Target(LimbZone zone, LimbMark mark)
    {
        Assert.That(LimbOrganRules.TryTarget(zone, mark, out var organ), Is.True);
        return organ;
    }
}
