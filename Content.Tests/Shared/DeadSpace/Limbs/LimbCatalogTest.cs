using System.Collections.Generic;
using System.Linq;
using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbCatalogTest
{
    [Test]
    public void AChestStabIsAHeartRowAndACritHook()
    {
        var hooks = Hooks(LimbZone.Torso, LimbMark.Point, projectile: false);

        Assert.Multiple(() =>
        {
            Assert.That(hooks, Does.Contain(LimbHooks.Organ));
            Assert.That(hooks, Does.Contain(LimbHooks.Crit));
            Assert.That(hooks, Does.Not.Contain(LimbHooks.Air));
            Assert.That(hooks, Does.Not.Contain(LimbHooks.Blind));
        });
    }

    [Test]
    public void AChestShotMissesTheHeartAndLungs()
    {
        var organs = LimbOrganRules.CreateAll().ToArray();
        foreach (var mark in new[] { LimbMark.Point, LimbMark.Edge, LimbMark.Blunt })
            LimbOrganRules.Apply(organs, LimbZone.Torso, mark, 200f, 1f, projectile: true);

        var shot = Hooks(LimbZone.Torso, LimbMark.Point, projectile: true);
        Assert.Multiple(() =>
        {
            Assert.That(organs[(int) LimbOrgan.Heart].Damage, Is.EqualTo(0f));
            Assert.That(organs[(int) LimbOrgan.Lungs].Damage, Is.EqualTo(0f));
            Assert.That(shot, Does.Not.Contain(LimbHooks.Crit));
            Assert.That(shot, Does.Not.Contain(LimbHooks.Whistle));
            Assert.That(Hooks(LimbZone.Torso, LimbMark.Edge, projectile: true), Does.Not.Contain(LimbHooks.Air));
        });
    }

    [Test]
    public void AHandCutDoesNotGrowAnOrgan()
    {
        Assert.That(Hooks(LimbZone.HandLeft, LimbMark.Edge, projectile: false), Is.Empty);
    }

    [Test]
    public void ArmorSlipAddsToTheMiss()
    {
        var bare = LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(LimbZone.Head), 0f, false);
        var covered = LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(LimbZone.Head), 0f, false, 0.4f);

        Assert.That(covered, Is.GreaterThan(bare));
    }

    private static List<string> Hooks(LimbZone zone, LimbMark mark, bool projectile)
    {
        var hooks = new List<string>();
        foreach (var row in LimbCatalog.Rows)
        {
            if (LimbCatalog.Fits(row, zone, mark, projectile))
                hooks.Add(row.Hook);
        }

        return hooks;
    }
}
