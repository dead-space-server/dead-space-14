using System.Collections.Generic;
using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbCatalogTest
{
    [Test]
    public void AChestStabIsAHeartRowAndACritHook()
    {
        var hooks = Hooks(LimbZone.Torso, LimbMark.Point, projectile: true);

        Assert.Multiple(() =>
        {
            Assert.That(hooks, Does.Contain(LimbHooks.Organ));
            Assert.That(hooks, Does.Contain(LimbHooks.Crit));
            Assert.That(hooks, Does.Not.Contain(LimbHooks.Air));
            Assert.That(hooks, Does.Not.Contain(LimbHooks.Blind));
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
