using System.Runtime.InteropServices;
using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbHealSpreadTest
{
    [Test]
    public void HealthySelectionFallsBackToTheUrgentWound()
    {
        var zones = LimbZoneState.CreateAll();
        var head = LimbWoundRules.Index(LimbZone.Head);
        var leg = LimbWoundRules.Index(LimbZone.LegLeft);
        var headState = zones[head];
        headState.Health = LimbWoundRules.HeadHealth;
        zones[head] = headState;
        var legState = zones[leg];
        legState.Health = 10f;
        zones[leg] = legState;

        var picked = LimbWoundRules.TreatIndex(CollectionsMarshal.AsSpan(zones), head);

        Assert.That(picked, Is.EqualTo(leg));
    }

    [Test]
    public void AWoundedSelectionStays()
    {
        var zones = LimbZoneState.CreateAll();
        var groin = LimbWoundRules.Index(LimbZone.Groin);
        var groinState = zones[groin];
        groinState.Health = 20f;
        zones[groin] = groinState;

        Assert.That(LimbWoundRules.TreatIndex(CollectionsMarshal.AsSpan(zones), groin), Is.EqualTo(groin));
    }

    [Test]
    public void InjectedHealSplitsAcrossWoundedLimbsOnly()
    {
        var heat = new float[] { 0f, 9f, 0f, 3f };
        LimbWoundRules.SpreadOne(heat, -6f);

        Assert.Multiple(() =>
        {
            Assert.That(heat[0], Is.EqualTo(0f));
            Assert.That(heat[1], Is.EqualTo(6f));
            Assert.That(heat[2], Is.EqualTo(0f));
            Assert.That(heat[3], Is.EqualTo(0f));
        });
    }

    [Test]
    public void TwoWoundedLimbsShareTheDoseEvenly()
    {
        var slash = new float[] { 8f, 4f };
        LimbWoundRules.SpreadOne(slash, -4f);

        Assert.Multiple(() =>
        {
            Assert.That(slash[0], Is.EqualTo(6f));
            Assert.That(slash[1], Is.EqualTo(2f));
        });
    }

    [Test]
    public void BulletDamageEasesOffWithDistanceAndLasersDoNotUseIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.BulletDistance(1f), Is.EqualTo(1f));
            Assert.That(LimbWoundRules.BulletDistance(LimbWoundRules.BulletFarRange), Is.EqualTo(LimbWoundRules.BulletFarKeep));
            Assert.That(LimbWoundRules.BulletDistance(8f), Is.LessThan(1f));
            Assert.That(LimbWoundRules.BulletDistance(8f), Is.GreaterThan(LimbWoundRules.BulletFarKeep));
        });
    }

    [Test]
    public void AimedBuckshotCanTakeTheHead()
    {
        Assert.That(LimbWoundRules.TryPelletVolley(LimbWoundRules.PelletSeverCount, 0.2f, false, true, true, LimbZone.Head, 0f, out var zone), Is.True);
        Assert.That(zone, Is.EqualTo(LimbZone.Head));
    }

    [Test]
    public void ClosedFractureArrivesBeforeTheLimbIsGone()
    {
        Assert.That(LimbWoundRules.BreakBone, Is.LessThan(LimbWoundRules.LimbHealth));
        Assert.That(LimbWoundRules.BreakBone, Is.GreaterThan(LimbWoundRules.StrainBone));
    }
}
