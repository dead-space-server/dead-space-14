using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Humanoid;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbSpeciesRulesTest
{
    [Test]
    public void APlantHasNoHeartAndJellyIsOnlyACore()
    {
        var plant = LimbSpeciesRules.Mask(new[] { LimbOrgan.Brain, LimbOrgan.Eyes });
        var jelly = LimbSpeciesRules.Mask(new[] { LimbOrgan.Core });

        Assert.Multiple(() =>
        {
            Assert.That((plant & LimbOrganMask.Heart), Is.EqualTo(LimbOrganMask.None));
            Assert.That((plant & LimbOrganMask.Lungs), Is.EqualTo(LimbOrganMask.None));
            Assert.That((plant & LimbOrganMask.Stomach), Is.EqualTo(LimbOrganMask.None));
            Assert.That((plant & LimbOrganMask.Brain), Is.EqualTo(LimbOrganMask.Brain));
            Assert.That((jelly & LimbOrganMask.Core), Is.EqualTo(LimbOrganMask.Core));
            Assert.That((jelly & LimbOrganMask.Heart), Is.EqualTo(LimbOrganMask.None));
        });
    }

    [Test]
    public void AMissingHeartIsNotStabbed()
    {
        var organs = LimbOrganRules.CreateAll();
        var plant = LimbSpeciesRules.Mask(new[] { LimbOrgan.Brain, LimbOrgan.Eyes });
        LimbOrganRules.Apply(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(organs), LimbZone.Torso, LimbMark.Point, 40f, 1f, plant);

        Assert.That(organs[(int) LimbOrgan.Heart].Damage, Is.EqualTo(0f));
    }

    [Test]
    public void WaterCutsABodyThatMeltsAndCloseBuckshotStripsSkin()
    {
        var state = new LimbZoneState();
        var melt = new LimbStrike(LimbZone.ArmLeft, LimbMark.Liquid, 20f, 1f, 0f, false, false, false, false, false, 1f, LiquidSever: 1.5f);
        LimbWoundRules.Apply(ref state, melt);

        Assert.Multiple(() =>
        {
            Assert.That(state.Sever, Is.GreaterThan(0f));
            Assert.That(state.SkinBare, Is.False);
            Assert.That(LimbWoundRules.StripsSkin(true, 0.2f, 0f), Is.True);
            Assert.That(LimbWoundRules.StripsSkin(true, 4f, 0f), Is.False);
            Assert.That(LimbWoundRules.StripsSkin(false, 0.2f, LimbWoundRules.RawSkin), Is.True);
            Assert.That(LimbSpeciesRules.Look(true, Color.Red), Is.EqualTo(LimbSpeciesRules.Bare));
            Assert.That(LimbSpeciesRules.Look(false, Color.Red), Is.EqualTo(Color.Red));
            Assert.That(LimbWoundRules.BlastStrips(80f, 1f), Is.True);
            Assert.That(LimbWoundRules.BlastStrips(80f, 0.7f), Is.False);
            Assert.That(LimbWoundRules.BlastStrips(80f, 1.4f), Is.True);
        });
    }

    [Test]
    public void AHeadKeepsHairAndLosesEyesWhenBothAreGone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbLookRules.Fits(LimbZone.Head, HumanoidVisualLayers.Hair, false), Is.True);
            Assert.That(LimbLookRules.Fits(LimbZone.Head, HumanoidVisualLayers.HeadTop, false), Is.True);
            Assert.That(LimbLookRules.Fits(LimbZone.Head, HumanoidVisualLayers.Eyes, false), Is.False);
            Assert.That(LimbLookRules.Fits(LimbZone.Head, HumanoidVisualLayers.Eyes, true), Is.True);
            Assert.That(LimbLookRules.Fits(LimbZone.ArmLeft, HumanoidVisualLayers.Hair, true), Is.False);
            Assert.That(LimbLookRules.Fits(LimbZone.ArmLeft, HumanoidVisualLayers.LArm, true), Is.True);
        });
    }
}
