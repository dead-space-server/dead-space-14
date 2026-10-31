using Content.Shared.DeadSpace.Body.Limbs;
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
            Assert.That(Target(LimbZone.Groin, LimbMark.Point), Is.EqualTo(LimbOrgan.Intestines));
            Assert.That(Target(LimbZone.Groin, LimbMark.Edge), Is.EqualTo(LimbOrgan.Intestines));
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
            Assert.That(burst.Ruptured, Is.True);
            Assert.That(burst.Organ, Is.EqualTo(LimbOrgan.Heart));
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
            Assert.That(lungs[(int) LimbOrgan.Lungs].Ruptured, Is.True);
            Assert.That(lungs[(int) LimbOrgan.Heart].Ruptured, Is.False);
            Assert.That(brain[(int) LimbOrgan.Brain].Ruptured, Is.True);
            Assert.That(brain[(int) LimbOrgan.Eyes].Ruptured, Is.False);
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
            Assert.That(drink.Ruptured, Is.True);
            Assert.That(swallowed[(int) LimbOrgan.Liver].Ruptured, Is.True);
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
        var burst = LimbOrganRules.Apply(organs, LimbZone.Eyes, LimbMark.Point, 40f, 1f);
        var again = LimbOrganRules.Apply(organs, LimbZone.Eyes, LimbMark.Point, 40f, 1f);

        Assert.Multiple(() =>
        {
            Assert.That(burst.Ruptured, Is.True);
            Assert.That(burst.EyeDamage, Is.EqualTo(LimbOrganRules.EyeRuptureDamage));
            Assert.That(again.Ruptured, Is.False);
            Assert.That(again.EyeDamage, Is.EqualTo(0));
        });
    }

    [Test]
    public void RuptureKeepsBleedingAndCryoDoesNotSealIt()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Point, 40f, 1f);
        LimbOrganRules.Apply(organs, LimbZone.Torso, LimbMark.Edge, 80f, 1f);
        LimbOrganRules.Apply(organs, LimbZone.Head, LimbMark.Point, 40f, 1f);
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

    private static LimbOrgan Target(LimbZone zone, LimbMark mark)
    {
        Assert.That(LimbOrganRules.TryTarget(zone, mark, out var organ), Is.True);
        return organ;
    }
}
