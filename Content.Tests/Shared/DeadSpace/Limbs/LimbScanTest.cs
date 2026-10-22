using System.Collections.Generic;
using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbScanTest
{
    [Test]
    public void IntactBodyIsHealthy()
    {
        var issues = new List<LimbScanIssue>();
        LimbScan.Build(LimbZoneState.CreateAll(), LimbOrganRules.CreateAll(), out var summary, issues);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Healthy, Is.True);
            Assert.That(issues, Is.Empty);
        });
    }

    [Test]
    public void SummaryCountsWhatIsWrong()
    {
        var zones = LimbZoneState.CreateAll();
        zones[(int) LimbZone.HandLeft] = new LimbZoneState { Severed = true, Scar = 1 };
        zones[(int) LimbZone.LegRight] = new LimbZoneState { Bone = LimbWoundRules.BreakBone };
        zones[(int) LimbZone.ArmLeft] = new LimbZoneState { Flesh = LimbWoundRules.StrainFlesh, Embedded = true };
        zones[(int) LimbZone.Torso] = new LimbZoneState { Skin = LimbWoundRules.RawSkin, Fire = 1f };
        zones[(int) LimbZone.Head] = new LimbZoneState { Bone = LimbWoundRules.StrainBone };

        var organs = LimbOrganRules.CreateAll();
        organs[(int) LimbOrgan.Heart] = new LimbOrganState { Ruptured = true, Damage = 40f };

        var issues = new List<LimbScanIssue>();
        LimbScan.Build(zones, organs, out var summary, issues);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Healthy, Is.False);
            Assert.That(summary.Missing, Is.EqualTo(1));
            Assert.That(summary.Fractures, Is.EqualTo(1));
            Assert.That(summary.Wounds, Is.EqualTo(1));
            Assert.That(summary.Embedded, Is.EqualTo(1));
            Assert.That(summary.RawSkin, Is.EqualTo(1));
            Assert.That(summary.Burning, Is.EqualTo(1));
            Assert.That(summary.Scars, Is.EqualTo(0));
            Assert.That(issues, Has.None.Matches<LimbScanIssue>(issue => issue.Kind == LimbIssueKind.Scar));
            Assert.That(summary.Ruptures, Is.EqualTo(1));
            Assert.That(issues, Has.Some.Matches<LimbScanIssue>(issue => issue.Organ && issue.Index == (byte) LimbOrgan.Heart));
            Assert.That(issues, Has.None.Matches<LimbScanIssue>(issue => !issue.Organ && issue.Index == (byte) LimbZone.HandLeft && issue.Kind == LimbIssueKind.OpenWound));
        });
    }
}
