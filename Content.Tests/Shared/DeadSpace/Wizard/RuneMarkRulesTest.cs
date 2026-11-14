// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Wizard.Runes;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Wizard;

[TestFixture]
public sealed class RuneMarkRulesTest
{
    private static RuneMarkResult Hit(string element, RuneMark current, bool lying = false, int roll = 0)
    {
        return RuneMarkRules.Resolve(element, current, lying, roll);
    }

    [Test]
    public void WaterDousesFireAndLeavesTargetWet()
    {
        var result = Hit(RuneMarkRules.Water, RuneMark.Burning);
        Assert.Multiple(() =>
        {
            Assert.That(result.Scale, Is.EqualTo(RuneMarkRules.Weak));
            Assert.That(result.Next, Is.EqualTo(RuneMark.Wet));
            Assert.That(result.Extinguish, Is.True);
        });
    }

    [Test]
    public void FireIsWeakOnWetAndStrongOnFrozenOrPoisoned()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Hit(RuneMarkRules.Fire, RuneMark.Wet).Scale, Is.EqualTo(RuneMarkRules.Weak));
            Assert.That(Hit(RuneMarkRules.Fire, RuneMark.Frozen).Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(Hit(RuneMarkRules.Fire, RuneMark.Poisoned).Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(Hit(RuneMarkRules.Fire, RuneMark.Wet).Next, Is.EqualTo(RuneMark.Burning));
        });
    }

    [Test]
    public void LightningReadsWaterAndMudButKeepsThem()
    {
        var wet = Hit(RuneMarkRules.Lightning, RuneMark.Wet);
        var dirty = Hit(RuneMarkRules.Lightning, RuneMark.Dirty);
        Assert.Multiple(() =>
        {
            Assert.That(wet.Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(wet.Next, Is.EqualTo(RuneMark.Wet));
            Assert.That(dirty.Scale, Is.EqualTo(RuneMarkRules.Weak));
            Assert.That(dirty.Next, Is.EqualTo(RuneMark.Dirty));
            Assert.That(Hit(RuneMarkRules.Lightning, RuneMark.None).Next, Is.EqualTo(RuneMark.None));
        });
    }

    [Test]
    public void EarthSmothersFireWithoutBonusAndCrushesTheFallen()
    {
        var burning = Hit(RuneMarkRules.Earth, RuneMark.Burning);
        Assert.Multiple(() =>
        {
            Assert.That(burning.Scale, Is.EqualTo(1f));
            Assert.That(burning.Extinguish, Is.True);
            Assert.That(burning.Next, Is.EqualTo(RuneMark.Dirty));
            Assert.That(Hit(RuneMarkRules.Earth, RuneMark.None, lying: true).Scale, Is.EqualTo(RuneMarkRules.Strong));
        });
    }

    [Test]
    public void IceFreezesWaterAndPutsOutFire()
    {
        var burning = Hit(RuneMarkRules.Ice, RuneMark.Burning);
        Assert.Multiple(() =>
        {
            Assert.That(Hit(RuneMarkRules.Ice, RuneMark.Wet).Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(Hit(RuneMarkRules.Ice, RuneMark.Wet).Next, Is.EqualTo(RuneMark.Frozen));
            Assert.That(burning.Scale, Is.EqualTo(1f));
            Assert.That(burning.Extinguish, Is.True);
        });
    }

    [Test]
    public void AirBlowsMarksAwayButFansTheFlame()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Hit(RuneMarkRules.Air, RuneMark.Wet).Next, Is.EqualTo(RuneMark.None));
            Assert.That(Hit(RuneMarkRules.Air, RuneMark.Dirty).Next, Is.EqualTo(RuneMark.None));
            Assert.That(Hit(RuneMarkRules.Air, RuneMark.Frozen).Next, Is.EqualTo(RuneMark.None));
            Assert.That(Hit(RuneMarkRules.Air, RuneMark.Burning).Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(Hit(RuneMarkRules.Air, RuneMark.Burning).Next, Is.EqualTo(RuneMark.Burning));
            Assert.That(Hit(RuneMarkRules.Air, RuneMark.Poisoned).Next, Is.EqualTo(RuneMark.Poisoned));
        });
    }

    [Test]
    public void BloodPoisonAndDeathFeedOnWounds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Hit(RuneMarkRules.Poison, RuneMark.Bleeding).Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(Hit(RuneMarkRules.Poison, RuneMark.Wet).Scale, Is.EqualTo(RuneMarkRules.Weak));
            Assert.That(Hit(RuneMarkRules.Blood, RuneMark.Bleeding).Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(Hit(RuneMarkRules.Blood, RuneMark.Wet).Scale, Is.EqualTo(1f));
            Assert.That(Hit(RuneMarkRules.Death, RuneMark.Poisoned).Scale, Is.EqualTo(RuneMarkRules.Strong));
            Assert.That(Hit(RuneMarkRules.Death, RuneMark.Bleeding).Next, Is.EqualTo(RuneMark.Withering));
            Assert.That(Hit(RuneMarkRules.Death, RuneMark.Burning).Scale, Is.EqualTo(1f));
        });
    }

    [Test]
    public void LifeCleansesWithoutScaling()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Hit(RuneMarkRules.Life, RuneMark.Poisoned).Next, Is.EqualTo(RuneMark.None));
            Assert.That(Hit(RuneMarkRules.Life, RuneMark.Withering).Next, Is.EqualTo(RuneMark.None));
            Assert.That(Hit(RuneMarkRules.Life, RuneMark.Bleeding).Next, Is.EqualTo(RuneMark.None));
            Assert.That(Hit(RuneMarkRules.Life, RuneMark.Wet).Next, Is.EqualTo(RuneMark.Wet));
            Assert.That(Hit(RuneMarkRules.Life, RuneMark.Poisoned).Scale, Is.EqualTo(1f));
        });
    }

    [Test]
    public void VoidIgnoresMarksAndDisplaces()
    {
        var result = Hit(RuneMarkRules.Void, RuneMark.Wet);
        Assert.Multiple(() =>
        {
            Assert.That(result.Scale, Is.EqualTo(1f));
            Assert.That(result.Next, Is.EqualTo(RuneMark.Displaced));
        });
    }

    [Test]
    public void ChaosRerollsAnyMarkIntoOneOfFour()
    {
        for (var roll = -5; roll < 10; roll++)
        {
            var marked = Hit(RuneMarkRules.Chaos, RuneMark.Withering, roll: roll);
            var clean = Hit(RuneMarkRules.Chaos, RuneMark.None, roll: roll);
            Assert.Multiple(() =>
            {
                Assert.That(marked.Scale, Is.EqualTo(RuneMarkRules.Strong));
                Assert.That(clean.Scale, Is.EqualTo(1f));
                Assert.That(RuneMarkRules.ChaosMarks, Does.Contain(marked.Next));
                Assert.That(RuneMarkRules.ChaosMarks, Does.Contain(clean.Next));
            });
        }
    }
}
