// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Surgery;

[TestFixture]
public sealed class SurgeryCutTest
{
    [Test]
    public void CauteryOverAnOpenMuscleSealsAPocket()
    {
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Cauterize,
            Zone = LimbZone.ArmLeft,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 0f,
            DeeperCut = true,
            Progress = 1f,
            ToolPrecision = 0.9f,
            CurrentBleed = 4f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Integrity, Is.GreaterThan(0.8f));
            Assert.That(result.Bleed, Is.GreaterThan(0f));
            Assert.That(result.Germs, Is.GreaterThan(0f));
            Assert.That(result.Key, Is.EqualTo("surgery-pocket"));
        });
    }

    [Test]
    public void HemostatDoesNotPullAnOrganFromAClosedCavity()
    {
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Extract,
            Zone = LimbZone.Torso,
            Kind = SurgeryLayerKind.Muscle,
            CavityReady = false,
            Progress = 1f,
        });

        Assert.That(result.RemoveOrgan, Is.False);
        Assert.That(result.Rejected, Is.True);
    }

    [Test]
    public void ASawOnSkinDoesNotOpenBone()
    {
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.SawBone,
            Zone = LimbZone.LegLeft,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 1f,
            Progress = 1f,
            ToolPrecision = 0.4f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Integrity, Is.EqualTo(1f));
            Assert.That(result.Flesh, Is.GreaterThan(0f));
            Assert.That(result.Key, Is.EqualTo("surgery-saw-flesh"));
        });
    }

    [Test]
    public void AShakyCutNicksTheNextLayer()
    {
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Incise,
            Zone = LimbZone.ArmRight,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 1f,
            Progress = 1f,
            Slips = 3,
            ToolPrecision = 0.4f,
        });

        Assert.That(result.NextLoss, Is.GreaterThan(0.2f));
    }

    [Test]
    public void RetractorOnClosedSkinDoesNotWound()
    {
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Retract,
            Zone = LimbZone.Torso,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 1f,
            Progress = 1f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.True);
            Assert.That(result.Flesh, Is.EqualTo(0f));
            Assert.That(result.Bleed, Is.EqualTo(0f));
            Assert.That(result.Integrity, Is.EqualTo(1f));
        });
    }

    [Test]
    public void RetractorOpensAVisibleCutWithoutWounding()
    {
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Retract,
            Zone = LimbZone.Torso,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 0.4f,
            Progress = 1f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.False);
            Assert.That(result.Retracted, Is.True);
            Assert.That(result.Flesh, Is.EqualTo(0f));
            Assert.That(result.Bleed, Is.EqualTo(0f));
        });
    }

    [Test]
    public void ClampDoesNotCut()
    {
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Clamp,
            Zone = LimbZone.Torso,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 1f,
            Progress = 1f,
            Slips = 5,
            CurrentBleed = 4f,
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Flesh, Is.EqualTo(0f));
            Assert.That(result.Germs, Is.EqualTo(0f));
            Assert.That(result.Integrity, Is.EqualTo(1f));
            Assert.That(result.Bleed, Is.LessThan(0f));
        });
    }

    [Test]
    public void AStrayCutCostsLimbHealth()
    {
        var stray = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Incise,
            Zone = LimbZone.Torso,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 1f,
            Progress = 0f,
            Slips = 1,
        });
        var clean = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = SurgeryVerbKind.Incise,
            Zone = LimbZone.Torso,
            Kind = SurgeryLayerKind.Skin,
            Integrity = 1f,
            Progress = 1f,
            Slips = 0,
        });

        Assert.Multiple(() =>
        {
            Assert.That(stray.Health, Is.EqualTo(-LimbWoundRules.HealthFor(LimbZone.Torso) * 0.4f).Within(0.01f));
            Assert.That(LimbWoundRules.HealthFor(LimbZone.Torso) + stray.Health, Is.LessThan(LimbWoundRules.HealthFor(LimbZone.Torso) * 0.75f));
            Assert.That(clean.Health, Is.EqualTo(0f));
        });
    }

    [Test]
    public void TheFloorLeavesALimpAndTheTableDoesNot()
    {
        var floor = SurgeryCut.Resolve(Bone(25f));
        var table = SurgeryCut.Resolve(Bone(0f));

        Assert.Multiple(() =>
        {
            Assert.That(40f + floor.Bone, Is.EqualTo(25f).Within(0.01f));
            Assert.That(40f + table.Bone, Is.EqualTo(0f).Within(0.01f));
        });
    }

    private static SurgeryRequest Bone(float floor)
    {
        return new SurgeryRequest
        {
            Verb = SurgeryVerbKind.SetBone,
            Zone = LimbZone.LegRight,
            Kind = SurgeryLayerKind.Bone,
            Integrity = 0f,
            Retracted = true,
            Progress = 1f,
            ToolPrecision = 1f,
            CurrentBone = 40f,
            BoneFloor = floor,
        };
    }
}
