using System.Runtime.InteropServices;
using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.Maths;
using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbWoundRulesTest
{
    [Test]
    public void CursorBandsPickZones()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.ZoneFromCursor(0f, 0.8f), Is.EqualTo(LimbZone.Head));
            Assert.That(LimbWoundRules.ZoneFromCursor(0f, 0f), Is.EqualTo(LimbZone.Torso));
            Assert.That(LimbWoundRules.ZoneFromCursor(0.2f, -0.5f), Is.EqualTo(LimbZone.LegRight));
            Assert.That(LimbWoundRules.ZoneFromCursor(-0.2f, -0.5f), Is.EqualTo(LimbZone.LegLeft));
            Assert.That(LimbWoundRules.ZoneFromCursor(0.1f, -0.9f), Is.EqualTo(LimbZone.FootRight));
            Assert.That(LimbWoundRules.ZoneFromCursor(-0.1f, -0.9f), Is.EqualTo(LimbZone.FootLeft));
            Assert.That(LimbWoundRules.ZoneFromCursor(0.8f, 0.2f), Is.EqualTo(LimbZone.HandRight));
            Assert.That(LimbWoundRules.ZoneFromCursor(-0.8f, 0.2f), Is.EqualTo(LimbZone.HandLeft));
            Assert.That(LimbWoundRules.ZoneFromCursor(0.8f, -0.1f), Is.EqualTo(LimbZone.ArmRight));
            Assert.That(LimbWoundRules.ZoneFromCursor(-0.8f, -0.1f), Is.EqualTo(LimbZone.ArmLeft));
        });
    }

    [Test]
    public void SprintWhileMovingDropsAim()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.AimIsPrecise(false, true), Is.True);
            Assert.That(LimbWoundRules.AimIsPrecise(true, false), Is.True);
            Assert.That(LimbWoundRules.AimIsPrecise(true, true), Is.False);
        });
    }

    [Test]
    public void MarkFollowsTheLargestAmount()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.MarkFromAmounts(1f, 0f, 5f, 0f, 0f), Is.EqualTo(LimbMark.Blunt));
            Assert.That(LimbWoundRules.MarkFromAmounts(8f, 2f, 1f, 0f, 0f), Is.EqualTo(LimbMark.Edge));
            Assert.That(LimbWoundRules.MarkFromAmounts(1f, 9f, 1f, 0f, 0f), Is.EqualTo(LimbMark.Point));
            Assert.That(LimbWoundRules.MarkFromAmounts(0f, 0f, 0f, 4f, 1f), Is.EqualTo(LimbMark.Heat));
            Assert.That(LimbWoundRules.MarkFromAmounts(0f, 0f, 0f, 1f, 6f), Is.EqualTo(LimbMark.Liquid));
        });
    }

    [Test]
    public void EyesConnectOnlySometimesAndThenBlind()
    {
        var state = new LimbZoneState();
        var miss = LimbWoundRules.Apply(ref state, Strike(LimbZone.Eyes, LimbMark.Point, 10f, 0.9f));
        var hit = LimbWoundRules.Apply(ref state, Strike(LimbZone.Eyes, LimbMark.Point, 10f, 0.1f));

        Assert.Multiple(() =>
        {
            Assert.That(miss.Applied, Is.False);
            Assert.That(miss.EyeDamage, Is.EqualTo(0));
            Assert.That(hit.EyeDamage, Is.EqualTo(1));
            Assert.That(state.Flesh, Is.EqualTo(0f));
        });
    }

    [Test]
    public void FaceCoverBlocksEyesMouthAndSplash()
    {
        var state = new LimbZoneState();
        var eyes = LimbWoundRules.Apply(ref state, Strike(LimbZone.Eyes, LimbMark.Point, 10f, 0.1f, face: true));
        var mouth = LimbWoundRules.Apply(ref state, Strike(LimbZone.Mouth, LimbMark.Liquid, 10f, 0.1f, face: true));
        var splash = LimbWoundRules.Apply(ref state, Strike(LimbZone.Head, LimbMark.Liquid, 10f, 0.1f, face: true));

        Assert.Multiple(() =>
        {
            Assert.That(eyes.Blocked, Is.True);
            Assert.That(eyes.EyeDamage, Is.EqualTo(0));
            Assert.That(mouth.Blocked, Is.True);
            Assert.That(mouth.Ingest, Is.False);
            Assert.That(splash.Blocked, Is.True);
            Assert.That(state.Skin, Is.EqualTo(0f));
        });
    }

    [Test]
    public void OpenFaceLetsSplashInAndMouthIngests()
    {
        var state = new LimbZoneState();
        var splash = LimbWoundRules.Apply(ref state, Strike(LimbZone.Head, LimbMark.Liquid, 10f, 0f));
        var mouth = LimbWoundRules.Apply(ref state, Strike(LimbZone.Mouth, LimbMark.Liquid, 10f, 0.1f));

        Assert.Multiple(() =>
        {
            Assert.That(splash.Applied, Is.True);
            Assert.That(state.Skin, Is.EqualTo(10f));
            Assert.That(mouth.Ingest, Is.True);
        });
    }

    [Test]
    public void KnifeFillsAHandAndDoesNotSeverIt()
    {
        var state = new LimbZoneState();
        var cut = LimbWoundRules.Apply(ref state, Strike(LimbZone.HandLeft, LimbMark.Edge, 80f, 0f));
        Assert.Multiple(() =>
        {
            Assert.That(cut.Severed, Is.False);
            Assert.That(state.Severed, Is.False);
            Assert.That(state.Sever, Is.EqualTo(0f));
            Assert.That(state.Flesh, Is.EqualTo(80f));
        });
    }

    [Test]
    public void ASharpKatanaSeversTheHand()
    {
        var state = new LimbZoneState();
        var cut = LimbWoundRules.Apply(ref state, Strike(LimbZone.HandLeft, LimbMark.Edge, 16f, 0f, cut: 2.5f, sharp: true));
        Assert.Multiple(() =>
        {
            Assert.That(cut.Severed, Is.True);
            Assert.That(state.Severed, Is.True);
            Assert.That(state.Bleed, Is.EqualTo(LimbWoundRules.StumpBleed));
        });
    }

    [Test]
    public void AHeavyCutTakesTheArmOnlyOnTheRoll()
    {
        var missed = new LimbZoneState();
        var landed = new LimbZoneState();
        var hand = new LimbZoneState();
        var miss = LimbWoundRules.Apply(ref missed, Strike(LimbZone.ArmLeft, LimbMark.Edge, 30f, 0.9f, cut: 2.7f, sharp: true, severChance: 0.35f));
        var hit = LimbWoundRules.Apply(ref landed, Strike(LimbZone.ArmLeft, LimbMark.Edge, 30f, 0.1f, cut: 2.7f, sharp: true, severChance: 0.35f));
        var palm = LimbWoundRules.Apply(ref hand, Strike(LimbZone.HandLeft, LimbMark.Edge, 30f, 0.9f, cut: 2.7f, sharp: true, severChance: 0.35f));

        Assert.Multiple(() =>
        {
            Assert.That(miss.Severed, Is.False);
            Assert.That(missed.Sever, Is.LessThan(LimbWoundRules.LimbSever));
            Assert.That(hit.Severed, Is.True);
            Assert.That(palm.Severed, Is.True);
        });
    }

    [Test]
    public void ArmTakesLongerThanAHandAndTorsoNeverComesOff()
    {
        var arm = new LimbZoneState();
        var torso = new LimbZoneState();
        var armEarly = LimbWoundRules.Apply(ref arm, Strike(LimbZone.ArmLeft, LimbMark.Edge, 79f, 0f, sharp: true));
        var armCut = LimbWoundRules.Apply(ref arm, Strike(LimbZone.ArmLeft, LimbMark.Edge, 1f, 0f, sharp: true));
        var torsoCut = LimbWoundRules.Apply(ref torso, Strike(LimbZone.Torso, LimbMark.Edge, 500f, 0f, sharp: true));

        Assert.Multiple(() =>
        {
            Assert.That(armEarly.Severed, Is.False);
            Assert.That(armCut.Severed, Is.True);
            Assert.That(torsoCut.Severed, Is.False);
            Assert.That(torso.Severed, Is.False);
        });
    }

    [Test]
    public void HeadComesOffOnlyAfterALongCut()
    {
        var state = new LimbZoneState();
        var early = LimbWoundRules.Apply(ref state, Strike(LimbZone.Head, LimbMark.Edge, 199f, 0f, sharp: true));
        var cut = LimbWoundRules.Apply(ref state, Strike(LimbZone.Head, LimbMark.Edge, 1f, 0f, sharp: true));

        Assert.Multiple(() =>
        {
            Assert.That(early.Severed, Is.False);
            Assert.That(cut.Severed, Is.True);
        });
    }

    [Test]
    public void PartHealthDecidesLifeAndDistanceSpoilsASmallTarget()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.Life(LimbWoundRules.HeadHealth, LimbWoundRules.TorsoHealth), Is.EqualTo(LimbLife.Alive));
            Assert.That(LimbWoundRules.Life(LimbWoundRules.HeadHealth, 0f), Is.EqualTo(LimbLife.Crit));
            Assert.That(LimbWoundRules.Life(0f, LimbWoundRules.TorsoHealth), Is.EqualTo(LimbLife.Dead));
            Assert.That(LimbWoundRules.TorsoHealth, Is.GreaterThan(LimbWoundRules.HeadHealth));
            Assert.That(LimbWoundRules.HeadHealth, Is.GreaterThan(LimbWoundRules.LimbHealth));
            Assert.That(LimbWoundRules.LimbHealth, Is.GreaterThan(LimbWoundRules.ExtremityHealth));
            Assert.That(LimbWoundRules.ExtremityHealth, Is.GreaterThan(30f));
            var zones = LimbZoneState.CreateAll();
            Assert.That(zones[LimbWoundRules.Index(LimbZone.Head)].Health, Is.EqualTo(LimbWoundRules.HeadHealth));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.Torso)].Health, Is.EqualTo(LimbWoundRules.TorsoHealth));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.Groin)].Health, Is.EqualTo(LimbWoundRules.GroinHealth));
            Assert.That(LimbWoundRules.Tone(LimbWoundRules.GroinHealth, LimbWoundRules.GroinHealth), Is.EqualTo(0));
            Assert.That(LimbWoundRules.Tone(10f, LimbWoundRules.GroinHealth), Is.EqualTo(4));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.ArmLeft)].Health, Is.EqualTo(LimbWoundRules.LimbHealth));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.HandLeft)].Health, Is.EqualTo(LimbWoundRules.ExtremityHealth));
            Assert.That(LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(LimbZone.Eyes), 0f, false), Is.LessThan(0.15f));
            Assert.That(LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(LimbZone.Eyes), 0f, true), Is.GreaterThan(0.5f));
            Assert.That(LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(LimbZone.Eyes), 3f, false), Is.GreaterThan(0.8f));
            Assert.That(LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(LimbZone.Head), 8f, false), Is.GreaterThan(0.85f));
            Assert.That(LimbWoundRules.SlipChance(LimbWoundRules.TargetSize(LimbZone.Torso), 8f, false), Is.EqualTo(0f));
            Assert.That(LimbWoundRules.SlipTo(LimbZone.Eyes), Is.EqualTo(LimbZone.Head));
            Assert.That(LimbWoundRules.SlipTo(LimbZone.Head), Is.EqualTo(LimbZone.Torso));
            Assert.That(LimbWoundRules.SlipTo(LimbZone.HandLeft), Is.EqualTo(LimbZone.ArmLeft));
        });
    }

    [Test]
    public void AShotLandsOnTheSpriteWhereItHit()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.ZoneFromHit(0f, -0.35f), Is.EqualTo(LimbZone.Groin));
            Assert.That(LimbWoundRules.ZoneFromHit(0f, -0.3f), Is.EqualTo(LimbZone.Groin));
            Assert.That(LimbWoundRules.ZoneFromHit(0f, 0f), Is.EqualTo(LimbZone.Torso));
            Assert.That(LimbWoundRules.ZoneFromHit(0f, 0.35f), Is.EqualTo(LimbZone.Head));
            Assert.That(LimbWoundRules.ZoneFromHit(-0.4f, 0f), Is.EqualTo(LimbZone.ArmLeft));
            Assert.That(LimbWoundRules.ZoneFromHit(0.4f, 0f), Is.EqualTo(LimbZone.ArmRight));
            Assert.That(LimbWoundRules.ZoneFromHit(-0.4f, -0.2f), Is.EqualTo(LimbZone.HandLeft));
            Assert.That(LimbWoundRules.ZoneFromHit(0.4f, -0.2f), Is.EqualTo(LimbZone.HandRight));
            Assert.That(LimbWoundRules.ZoneFromHit(0.08f, -0.7f), Is.EqualTo(LimbZone.FootRight));
            Assert.That(LimbWoundRules.ZoneFromHit(-0.08f, -0.7f), Is.EqualTo(LimbZone.FootLeft));
            Assert.That(LimbWoundRules.ZoneFromHit(-0.08f, -0.45f), Is.EqualTo(LimbZone.LegLeft));
            Assert.That(LimbWoundRules.ZoneFromHit(0.08f, -0.45f), Is.EqualTo(LimbZone.LegRight));
            Assert.That(LimbWoundRules.Pick(false, LimbZone.Head, LimbWoundRules.ZoneFromHit(0f, 0f)), Is.EqualTo(LimbZone.Torso));
            Assert.That(LimbWoundRules.Pick(true, LimbZone.Torso, LimbWoundRules.ZoneFromHit(-0.08f, -0.45f)), Is.EqualTo(LimbZone.LegLeft));
            Assert.That(LimbWoundRules.Pick(true, LimbZone.Head, LimbWoundRules.ZoneFromHit(0.08f, -0.7f)), Is.EqualTo(LimbZone.FootRight));
            Assert.That(LimbWoundRules.Pick(true, LimbZone.Eyes, LimbWoundRules.ZoneFromHit(0f, 0.35f)), Is.EqualTo(LimbZone.Eyes));
            Assert.That(LimbWoundRules.Pick(true, LimbZone.Mouth, LimbWoundRules.ZoneFromHit(0f, 0f)), Is.EqualTo(LimbZone.Mouth));
            Assert.That(LimbWoundRules.Pick(true, LimbZone.Eyes, LimbWoundRules.ZoneFromHit(0.08f, -0.7f)), Is.EqualTo(LimbZone.FootRight));
        });
    }

    [Test]
    public void BurnsShareDegreesForHeatAndAcid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.BurnDegree(15f), Is.EqualTo(0));
            Assert.That(LimbWoundRules.BurnDegree(LimbWoundRules.BurnFirst), Is.EqualTo(1));
            Assert.That(LimbWoundRules.BurnDegree(LimbWoundRules.BurnSecond), Is.EqualTo(2));
            Assert.That(LimbWoundRules.BurnDegree(LimbWoundRules.RawSkin), Is.EqualTo(3));
        });
    }

    [Test]
    public void ScrewdriverDoesNotCutOffAHand()
    {
        var state = new LimbZoneState();
        var result = LimbWoundRules.Apply(ref state, Strike(LimbZone.HandLeft, LimbMark.Point, 6f, 0f));
        for (var i = 1; i < 20; i++)
            result = LimbWoundRules.Apply(ref state, Strike(LimbZone.HandLeft, LimbMark.Point, 6f, 0f));

        Assert.Multiple(() =>
        {
            Assert.That(result.Severed, Is.False);
            Assert.That(state.Sever, Is.EqualTo(0f));
            Assert.That(state.Flesh, Is.EqualTo(120f));
        });
    }

    [Test]
    public void PointDoesNotSawALimbOffQuickly()
    {
        var state = new LimbZoneState();
        LimbWoundRules.Apply(ref state, Strike(LimbZone.HandLeft, LimbMark.Point, 40f, 0f));

        Assert.Multiple(() =>
        {
            Assert.That(state.Severed, Is.False);
            Assert.That(state.Sever, Is.EqualTo(0f));
            Assert.That(state.Flesh, Is.EqualTo(40f));
        });
    }

    [Test]
    public void BurnsKeepHurtingAndARuinedPairOfArmsCannotGrasp()
    {
        var fine = new LimbZoneState();
        var burned = new LimbZoneState { Skin = LimbWoundRules.RawSkin };
        var gone = new LimbZoneState { Severed = true };

        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.BurnPain(LimbWoundRules.BurnSecond), Is.GreaterThan(0f));
            Assert.That(LimbWoundRules.BurnPain(LimbWoundRules.RawSkin), Is.GreaterThan(LimbWoundRules.BurnPain(LimbWoundRules.BurnSecond)));
            Assert.That(LimbWoundRules.BurnPain(0f), Is.EqualTo(0f));
            Assert.That(LimbWoundRules.CanGrasp(burned, fine, fine, fine), Is.True);
            Assert.That(LimbWoundRules.CanGrasp(burned, fine, burned, fine), Is.False);
            Assert.That(LimbWoundRules.CanGrasp(gone, fine, burned, fine), Is.False);
        });
    }

    [Test]
    public void EyesAndMouthAndAKnifeOnBone()
    {
        var state = new LimbZoneState();
        LimbWoundRules.Apply(ref state, Strike(LimbZone.Head, LimbMark.Edge, 200f, 0f, cut: 0.4f));

        Assert.Multiple(() =>
        {
            Assert.That(state.Severed, Is.False);
            Assert.That(LimbWoundRules.Vision(2, 0f), Is.EqualTo(9));
            Assert.That(LimbWoundRules.Vision(1, 0f), Is.EqualTo(4));
            Assert.That(LimbWoundRules.Vision(0, LimbWoundRules.BurnSecond), Is.EqualTo(3));
            Assert.That(LimbWoundRules.Vision(1, LimbWoundRules.RawSkin), Is.EqualTo(9));
            Assert.That(LimbWoundRules.RibBreath(0f, 0f), Is.EqualTo(0f));
            Assert.That(LimbWoundRules.RibBreath(LimbWoundRules.StrainBone, 0f), Is.EqualTo(0.35f));
            Assert.That(LimbWoundRules.RibBreath(LimbWoundRules.BreakBone, 0f), Is.EqualTo(0.9f));
            Assert.That(LimbWoundRules.RibBreath(LimbWoundRules.BreakBone, LimbWoundRules.StrainFlesh), Is.EqualTo(1.4f));
            Assert.That(LimbWoundRules.Garble("привет", 4f), Is.EqualTo("привет"));
            Assert.That(LimbWoundRules.Garble("привет", 10f), Is.Not.EqualTo("привет"));
            Assert.That(LimbWoundRules.Garble("привет", 60f), Is.Not.EqualTo("привет"));
        });
    }

    [Test]
    public void ScarLowersTheNextSever()
    {
        var state = new LimbZoneState { Scar = 4 };
        var cut = LimbWoundRules.Apply(ref state, Strike(LimbZone.HandLeft, LimbMark.Edge, 10f, 0f, sharp: true));

        Assert.That(cut.Severed, Is.True);
    }

    [Test]
    public void BluntBreaksBoneAndDropsABareHand()
    {
        var bare = new LimbZoneState();
        var gloved = new LimbZoneState();
        var bareHit = LimbWoundRules.Apply(ref bare, Strike(LimbZone.HandRight, LimbMark.Blunt, 10f, 0.5f));
        var glovedHit = LimbWoundRules.Apply(ref gloved, Strike(LimbZone.HandRight, LimbMark.Blunt, 10f, 0.5f, grip: true));
        var leg = new LimbZoneState();
        LimbWoundRules.Apply(ref leg, Strike(LimbZone.LegLeft, LimbMark.Blunt, LimbWoundRules.BreakBone, 0f));

        Assert.Multiple(() =>
        {
            Assert.That(bareHit.DropItem, Is.True);
            Assert.That(glovedHit.DropItem, Is.False);
            Assert.That(LimbWoundRules.Limp(leg, false), Is.True);
            Assert.That(LimbWoundRules.Limp(leg, true), Is.False);
        });
    }

    [Test]
    public void SplintHoldsUntilTheNextBluntHit()
    {
        var state = new LimbZoneState();
        LimbWoundRules.Apply(ref state, Strike(LimbZone.LegRight, LimbMark.Blunt, LimbWoundRules.BreakBone, 0f));
        LimbWoundRules.Splint(ref state);

        Assert.That(LimbWoundRules.Limp(state, false), Is.False);

        LimbWoundRules.Apply(ref state, Strike(LimbZone.LegRight, LimbMark.Blunt, 1f, 0f));
        Assert.That(LimbWoundRules.Limp(state, false), Is.True);
    }

    [Test]
    public void AdrenalineDoesNotReplaceAMissingLeg()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.MoveSpeed(true, 0), Is.EqualTo(LimbWoundRules.LimpSpeed));
            Assert.That(LimbWoundRules.MoveSpeed(false, 1), Is.EqualTo(LimbWoundRules.MissingLegSpeed));
            Assert.That(LimbWoundRules.MoveSpeed(false, 2), Is.EqualTo(LimbWoundRules.MissingLegSpeed * LimbWoundRules.MissingLegSpeed));
            Assert.That(LimbWoundRules.SlowHands(new LimbZoneState { Bone = LimbWoundRules.BreakBone }, new LimbZoneState(), false), Is.True);
            Assert.That(LimbWoundRules.SlowHands(new LimbZoneState { Bone = LimbWoundRules.BreakBone }, new LimbZoneState(), true), Is.False);
        });
    }

    [Test]
    public void BandageCutsBleedAndDoesNotCloseTheWound()
    {
        var state = new LimbZoneState();
        var hit = LimbWoundRules.Apply(ref state, Strike(LimbZone.ArmLeft, LimbMark.Edge, 30f, 0f));
        var before = state.Bleed;
        LimbWoundRules.Bandage(ref state);
        var after = LimbWoundRules.Apply(ref state, Strike(LimbZone.ArmLeft, LimbMark.Edge, 10f, 0f));

        Assert.Multiple(() =>
        {
            Assert.That(hit.BleedAdded, Is.GreaterThan(0f));
            Assert.That(state.Flesh, Is.EqualTo(40f));
            Assert.That(state.Bleed, Is.LessThan(before));
            Assert.That(after.BleedAdded, Is.EqualTo(10f * LimbWoundRules.BleedPerFlesh * LimbWoundRules.BandageBleedFactor).Within(0.001f));
        });
    }

    [Test]
    public void OintmentLowersSkin()
    {
        var state = new LimbZoneState();
        LimbWoundRules.Apply(ref state, Strike(LimbZone.Torso, LimbMark.Liquid, 40f, 0f));
        LimbWoundRules.Ointment(ref state, 15f);

        Assert.That(state.Skin, Is.EqualTo(25f));
    }

    [Test]
    public void CryoStepsABreakDownAndKeepsTheScar()
    {
        var state = new LimbZoneState { Flesh = LimbWoundRules.BreakFlesh, Bone = LimbWoundRules.BreakBone, Skin = LimbWoundRules.RawSkin };
        LimbWoundRules.Cryo(ref state);

        Assert.Multiple(() =>
        {
            Assert.That(state.Flesh, Is.EqualTo(LimbWoundRules.StrainFlesh));
            Assert.That(state.Bone, Is.EqualTo(LimbWoundRules.StrainBone));
            Assert.That(state.Scar, Is.EqualTo(1));
            Assert.That(state.Skin, Is.LessThan(LimbWoundRules.RawSkin));
        });

        LimbWoundRules.Cryo(ref state);
        Assert.Multiple(() =>
        {
            Assert.That(state.Flesh, Is.EqualTo(0f));
            Assert.That(state.Bone, Is.EqualTo(0f));
            Assert.That(state.Scar, Is.EqualTo(1));
        });
    }

    [Test]
    public void HelmetRicochetDependsOnTheRound()
    {
        var glanced = new LimbZoneState();
        var punched = new LimbZoneState();
        var pelletState = new LimbZoneState();
        var burned = new LimbZoneState();
        var cut = new LimbZoneState();
        var pistol = LimbWoundRules.Apply(ref glanced, Strike(LimbZone.Head, LimbMark.Point, 10f, 0.2f, projectile: true, pierce: 10f, ricochet: true));
        var heavy = LimbWoundRules.Apply(ref punched, Strike(LimbZone.Head, LimbMark.Point, 30f, 0.2f, projectile: true, pierce: 30f, ricochet: true));
        var pellet = LimbWoundRules.Apply(ref pelletState, Strike(LimbZone.Head, LimbMark.Point, 8f, 0.5f, projectile: true, pierce: 8f, ricochet: true));
        var laser = LimbWoundRules.Apply(ref burned, Strike(LimbZone.Head, LimbMark.Heat, 20f, 0.1f, projectile: true, ricochet: true));
        var knife = LimbWoundRules.Apply(ref cut, Strike(LimbZone.Head, LimbMark.Edge, 10f, 0.1f, ricochet: true));

        Assert.Multiple(() =>
        {
            Assert.That(pistol.Ricochet, Is.True);
            Assert.That(glanced.Flesh, Is.EqualTo(0f));
            Assert.That(heavy.Ricochet, Is.False);
            Assert.That(heavy.Applied, Is.True);
            Assert.That(pellet.Ricochet, Is.True);
            Assert.That(laser.Ricochet, Is.False);
            Assert.That(burned.Skin, Is.EqualTo(20f));
            Assert.That(knife.Ricochet, Is.False);
            Assert.That(cut.Flesh, Is.EqualTo(10f));
        });
    }

    [Test]
    public void RaisedShieldStopsTheHitAndMetalCanGlance()
    {
        var blocked = new LimbZoneState();
        var glanced = new LimbZoneState();
        var stop = LimbWoundRules.Apply(ref blocked, Strike(LimbZone.Torso, LimbMark.Edge, 40f, 0f, shield: true));
        var glance = LimbWoundRules.Apply(ref glanced, Strike(LimbZone.Torso, LimbMark.Point, 10f, 0.1f, projectile: true, pierce: 10f, ricochet: true, shield: true));

        Assert.Multiple(() =>
        {
            Assert.That(stop.Blocked, Is.True);
            Assert.That(blocked.Flesh, Is.EqualTo(0f));
            Assert.That(glance.Ricochet, Is.True);
            Assert.That(glanced.Flesh, Is.EqualTo(0f));
        });
    }

    [Test]
    public void GlovesSlowTheCut()
    {
        var state = new LimbZoneState();
        LimbWoundRules.Apply(ref state, Strike(LimbZone.HandLeft, LimbMark.Edge, 40f, 0f, multiplier: 0.6f, sharp: true));

        Assert.Multiple(() =>
        {
            Assert.That(state.Severed, Is.False);
            Assert.That(state.Sever, Is.EqualTo(24f));
        });
    }

    [Test]
    public void FireStartsOnTheOriginAndStepsToANeighbor()
    {
        var zones = LimbZoneState.CreateAll().ToArray();
        LimbWoundRules.Ignite(zones, LimbZone.FootLeft, 3f);
        var blocked = new bool[LimbWoundRules.BodyZoneCount];
        LimbWoundRules.SpreadFire(zones, blocked);

        Assert.Multiple(() =>
        {
            Assert.That(zones[LimbWoundRules.Index(LimbZone.FootLeft)].Fire, Is.GreaterThan(0f));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.LegLeft)].Fire, Is.EqualTo(LimbWoundRules.FireSpreadAmount));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.FootRight)].Fire, Is.EqualTo(0f));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.Torso)].Fire, Is.EqualTo(0f));
        });
    }

    [Test]
    public void FireDoesNotEnterAProtectedZone()
    {
        var zones = LimbZoneState.CreateAll().ToArray();
        LimbWoundRules.Ignite(zones, LimbZone.Torso, 3f);
        var blocked = new bool[LimbWoundRules.BodyZoneCount];
        blocked[LimbWoundRules.Index(LimbZone.Head)] = true;
        blocked[LimbWoundRules.Index(LimbZone.ArmLeft)] = true;
        blocked[LimbWoundRules.Index(LimbZone.ArmRight)] = true;
        blocked[LimbWoundRules.Index(LimbZone.LegLeft)] = true;
        LimbWoundRules.SpreadFire(zones, blocked);

        Assert.Multiple(() =>
        {
            Assert.That(zones[LimbWoundRules.Index(LimbZone.Head)].Fire, Is.EqualTo(0f));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.LegRight)].Fire, Is.EqualTo(LimbWoundRules.FireSpreadAmount));
        });
    }

    [Test]
    public void EdgesCoolFasterThanTheOrigin()
    {
        var zones = LimbZoneState.CreateAll().ToArray();
        zones[LimbWoundRules.Index(LimbZone.Torso)].Fire = 2f;
        zones[LimbWoundRules.Index(LimbZone.Head)].Fire = 0.4f;
        var blocked = new bool[LimbWoundRules.BodyZoneCount];
        blocked[LimbWoundRules.Index(LimbZone.Head)] = true;
        blocked[LimbWoundRules.Index(LimbZone.ArmLeft)] = true;
        blocked[LimbWoundRules.Index(LimbZone.ArmRight)] = true;
        blocked[LimbWoundRules.Index(LimbZone.LegLeft)] = true;
        blocked[LimbWoundRules.Index(LimbZone.LegRight)] = true;
        blocked[LimbWoundRules.Index(LimbZone.Groin)] = true;
        LimbWoundRules.SpreadFire(zones, blocked);

        Assert.Multiple(() =>
        {
            Assert.That(zones[LimbWoundRules.Index(LimbZone.Head)].Fire, Is.EqualTo(0f));
            Assert.That(zones[LimbWoundRules.Index(LimbZone.Torso)].Fire, Is.EqualTo(2f - LimbWoundRules.FireOriginDecay).Within(0.001f));
        });
    }

    [Test]
    public void WoundMultiplierAppliesToTheVest()
    {
        var state = new LimbZoneState();
        LimbWoundRules.Apply(ref state, Strike(LimbZone.Torso, LimbMark.Point, 20f, 0.9f, pierce: 20f, multiplier: 0.55f));

        Assert.That(state.Flesh, Is.EqualTo(11f));
    }

    [Test]
    public void BluntBreaksBoneAndAThroughBulletDoesNot()
    {
        var blunt = new LimbZoneState();
        var bullet = new LimbZoneState();
        LimbWoundRules.Apply(ref blunt, Strike(LimbZone.LegLeft, LimbMark.Blunt, 60f, 0.5f));
        LimbWoundRules.Apply(ref bullet, Strike(LimbZone.Torso, LimbMark.Point, 60f, 0.5f, projectile: true, pierce: 60f, secondary: 0.5f));

        Assert.Multiple(() =>
        {
            Assert.That(blunt.Bone, Is.EqualTo(60f));
            Assert.That(LimbWoundRules.Limp(blunt, false), Is.True);
            Assert.That(bullet.Bone, Is.EqualTo(0f));
            Assert.That(bullet.Flesh, Is.EqualTo(60f));
            Assert.That(bullet.Embedded, Is.False);
        });
    }

    [Test]
    public void BulletsRarelyStickAndAimedShotsStickOrShatterALittleMore()
    {
        var stuck = new LimbZoneState();
        var through = new LimbZoneState();
        var aimedStuck = new LimbZoneState();
        var aimedThrough = new LimbZoneState();
        var shattered = new LimbZoneState();
        LimbWoundRules.Apply(ref stuck, Strike(LimbZone.Torso, LimbMark.Point, 20f, 0.05f, projectile: true, pierce: 20f));
        LimbWoundRules.Apply(ref through, Strike(LimbZone.Torso, LimbMark.Point, 20f, 0.1f, projectile: true, pierce: 20f));
        LimbWoundRules.Apply(ref aimedStuck, Strike(LimbZone.Torso, LimbMark.Point, 20f, 0.1f, projectile: true, pierce: 20f, aimed: true, secondary: 0.5f));
        LimbWoundRules.Apply(ref aimedThrough, Strike(LimbZone.Torso, LimbMark.Point, 20f, 0.25f, projectile: true, pierce: 20f, aimed: true, secondary: 0.5f));
        LimbWoundRules.Apply(ref shattered, Strike(LimbZone.LegLeft, LimbMark.Point, 30f, 0.5f, projectile: true, pierce: 30f, aimed: true, secondary: 0.05f));

        Assert.Multiple(() =>
        {
            Assert.That(stuck.Embedded, Is.True);
            Assert.That(through.Embedded, Is.False);
            Assert.That(through.Bone, Is.EqualTo(0f));
            Assert.That(aimedStuck.Embedded, Is.True);
            Assert.That(aimedThrough.Embedded, Is.False);
            Assert.That(shattered.Bone, Is.EqualTo(30f));
            Assert.That(shattered.Embedded, Is.False);
        });
    }

    [Test]
    public void VestTurnsABulletIntoBehindArmorTrauma()
    {
        var state = new LimbZoneState();
        LimbWoundRules.Apply(ref state, Strike(LimbZone.Torso, LimbMark.Point, 40f, 0.01f, projectile: true, pierce: 40f, multiplier: 0.55f, behindArmor: true, secondary: 0.01f));

        Assert.Multiple(() =>
        {
            Assert.That(state.Embedded, Is.False);
            Assert.That(state.Flesh, Is.LessThan(20f));
            Assert.That(state.Bone, Is.EqualTo(40f * LimbWoundRules.BehindArmorBone));
        });
    }

    [Test]
    public void ShotgunTearsAtArmLengthAndNotAtFiveMeters()
    {
        var need = LimbWoundRules.PelletSeverCount;
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.PelletCounts(0.2f, false, true, true), Is.True);
            Assert.That(LimbWoundRules.PelletCounts(1f, false, true, true), Is.True);
            Assert.That(LimbWoundRules.PelletCounts(1.2f, false, true, true), Is.False);
            Assert.That(LimbWoundRules.PelletCounts(0.2f, true, true, true), Is.False);
            Assert.That(LimbWoundRules.PelletCounts(0.2f, false, true, false), Is.False);
            Assert.That(LimbWoundRules.PelletCounts(0.2f, false, false, true), Is.False);
        });

        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.TryPelletVolley(need, 0.2f, false, true, true, LimbZone.Torso, 0f, out var close), Is.True);
            Assert.That(close, Is.EqualTo(LimbZone.HandLeft));
            Assert.That(LimbWoundRules.TryPelletVolley(need - 1, 0.2f, false, true, true, LimbZone.ArmLeft, 0f, out _), Is.False);
            Assert.That(LimbWoundRules.TryPelletVolley(need, 5f, false, true, true, LimbZone.ArmLeft, 0f, out _), Is.False);
            Assert.That(LimbWoundRules.TryPelletVolley(need, 0.2f, true, true, true, LimbZone.Torso, 0f, out _), Is.False);
            Assert.That(LimbWoundRules.TryPelletVolley(need, 0.2f, false, true, true, LimbZone.Head, 0f, out var head), Is.True);
            Assert.That(head, Is.EqualTo(LimbZone.Head));
        });
    }

    [Test]
    public void ACloseBlastCanTakeAnUnarmoredLimb()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.BlastSeverChance(0.2f), Is.EqualTo(LimbWoundRules.BlastSeverNearChance));
            Assert.That(LimbWoundRules.BlastSeverChance(LimbWoundRules.BlastSeverFar), Is.EqualTo(0f));
            Assert.That(LimbWoundRules.BlastSeverChance(3f), Is.EqualTo(LimbWoundRules.BlastSeverNearChance * 0.5f).Within(0.001f));
            Assert.That(LimbWoundRules.TryBlastSever(0.2f, false, 0.5f, LimbZone.ArmLeft), Is.True);
            Assert.That(LimbWoundRules.TryBlastSever(0.2f, true, 0f, LimbZone.ArmLeft), Is.False);
            Assert.That(LimbWoundRules.TryBlastSever(0.2f, false, 0.95f, LimbZone.LegRight), Is.False);
            Assert.That(LimbWoundRules.TryBlastSever(6f, false, 0f, LimbZone.HandLeft), Is.False);
            Assert.That(LimbWoundRules.TryBlastSever(0.2f, false, 0f, LimbZone.Head), Is.False);
            Assert.That(LimbWoundRules.TryBlastSever(0.2f, false, 0f, LimbZone.Torso), Is.False);
        });
    }

    [Test]
    public void ARoundCanBreakBoneAndBleedInside()
    {
        var state = new LimbZoneState();
        var hit = LimbWoundRules.Apply(ref state, Strike(LimbZone.LegLeft, LimbMark.Point, 16f, 0.9f, projectile: true, pierce: 16f, roundBone: 42f, roundBleed: 0.2f));
        Assert.Multiple(() =>
        {
            Assert.That(hit.Severed, Is.False);
            Assert.That(state.Bone, Is.EqualTo(42f));
            Assert.That(state.Bleed, Is.EqualTo(0.2f));
            Assert.That(LimbWoundRules.Limp(state, false), Is.True);
        });
    }

    [Test]
    public void EveryAimBandCanBeHit()
    {
        Assert.Multiple(() =>
        {
            AssertOwns(LimbAim.Shot, LimbAim.Hip);
            AssertOwns(LimbAim.Doll, 0f);
        });
    }

    [Test]
    public void ShotsNeverPickEyesOrMouthByThemselves()
    {
        foreach (var band in LimbAim.Shot)
        {
            Assert.That(band.Zone, Is.Not.EqualTo(LimbZone.Eyes));
            Assert.That(band.Zone, Is.Not.EqualTo(LimbZone.Mouth));
        }
    }

    [Test]
    public void AimCoverFollowsTheNearTable()
    {
        foreach (var called in System.Enum.GetValues<LimbZone>())
        {
            foreach (var natural in System.Enum.GetValues<LimbZone>())
            {
                var listed = called == natural;
                foreach (var near in LimbAim.Near)
                {
                    if (near.Called == called && near.Natural == natural)
                        listed = true;
                }

                Assert.That(LimbWoundRules.AimCovers(called, natural), Is.EqualTo(listed), $"{called} {natural}");
            }
        }
    }

    [Test]
    public void LeftAndRightShotsMirror()
    {
        float[] across = { 0.1f, 0.4f, 0.6f };
        float[] up = { 0.4f, 0f, -0.2f, -0.45f, -0.7f };
        Assert.Multiple(() =>
        {
            foreach (var x in across)
            {
                foreach (var y in up)
                    Assert.That(Mirror(LimbWoundRules.ZoneFromHit(-x, y)), Is.EqualTo(LimbWoundRules.ZoneFromHit(x, y)));
            }
        });
    }

    [Test]
    public void AQuietBodyDoesNotAskForATick()
    {
        var zones = LimbZoneState.CreateAll();
        var organs = LimbOrganRules.CreateAll();
        var span = CollectionsMarshal.AsSpan(zones);
        var organSpan = CollectionsMarshal.AsSpan(organs);

        Assert.That(LimbWoundRules.Awake(span, organSpan, false), Is.False);
        Assert.That(LimbWoundRules.Awake(span, organSpan, true), Is.False);
        span[0].Bleed = LimbWoundRules.TickBleed;
        Assert.That(LimbWoundRules.Awake(span, organSpan, false), Is.False);
        span[0].Flesh = 1f;
        Assert.That(LimbWoundRules.Awake(span, organSpan, true), Is.False);
    }

    [Test]
    public void OpenWoundsAskForATick()
    {
        var zones = LimbZoneState.CreateAll();
        var organs = LimbOrganRules.CreateAll();
        var span = CollectionsMarshal.AsSpan(zones);
        var organSpan = CollectionsMarshal.AsSpan(organs);

        span[0].Bleed = 1f;
        Assert.That(LimbWoundRules.Awake(span, organSpan, false), Is.True);
        span[0].Bleed = 0f;
        span[0].Skin = LimbWoundRules.BurnSecond;
        Assert.That(LimbWoundRules.Awake(span, organSpan, false), Is.True);
        span[0].Skin = 0f;
        span[LimbWoundRules.Index(LimbZone.Torso)].Bone = LimbWoundRules.BreakBone;
        Assert.That(LimbWoundRules.Awake(span, organSpan, false), Is.True);
        span[LimbWoundRules.Index(LimbZone.Torso)].Bone = 0f;
        span[0].Fire = 1f;
        Assert.That(LimbWoundRules.Awake(span, organSpan, false), Is.True);
        span[0].Fire = 0f;
        organSpan[(int) LimbOrgan.Lungs].Damage = 15f;
        Assert.That(LimbWoundRules.Awake(span, organSpan, false), Is.True);
        organSpan[(int) LimbOrgan.Lungs].Damage = 0f;
        span[0].Skin = 1f;
        Assert.That(LimbWoundRules.Awake(span, organSpan, true), Is.True);
    }

    [Test]
    public void MedicineGoesToThePartAboutToBeLost()
    {
        var weakHead = LimbZoneState.CreateAll();
        var weak = CollectionsMarshal.AsSpan(weakHead);
        weak[LimbWoundRules.Index(LimbZone.Head)].Bleed = 1f;
        weak[LimbWoundRules.Index(LimbZone.ArmLeft)].Health = 20f;
        weak[LimbWoundRules.Index(LimbZone.ArmLeft)].Bleed = 10f;
        Assert.That(LimbWoundRules.PickTreat(weak), Is.EqualTo(LimbWoundRules.Index(LimbZone.ArmLeft)));

        var dyingHead = LimbZoneState.CreateAll();
        var dying = CollectionsMarshal.AsSpan(dyingHead);
        dying[LimbWoundRules.Index(LimbZone.Head)].Health = 6f;
        dying[LimbWoundRules.Index(LimbZone.Head)].Bleed = 8f;
        dying[LimbWoundRules.Index(LimbZone.ArmLeft)].Health = 40f;
        dying[LimbWoundRules.Index(LimbZone.ArmLeft)].Bleed = 2f;
        Assert.That(LimbWoundRules.PickTreat(dying), Is.EqualTo(LimbWoundRules.Index(LimbZone.Head)));
    }

    [Test]
    public void BleedTakesItsLimbFirstThenTheRestHarder()
    {
        var zones = LimbZoneState.CreateAll();
        var span = CollectionsMarshal.AsSpan(zones);
        var arm = LimbWoundRules.Index(LimbZone.ArmLeft);
        var head = LimbWoundRules.Index(LimbZone.Head);
        span[arm].Health = 20f;
        span[arm].Bleed = 10f;
        var headFull = span[head].Health;
        LimbWoundRules.DrainBleed(span);
        Assert.That(span[head].Health, Is.EqualTo(headFull));
        Assert.That(span[arm].Health, Is.EqualTo(20f - 10f * LimbWoundRules.BleedLocal));

        var guard = 0;
        while (!span[arm].Atrophied && guard < 30)
        {
            LimbWoundRules.DrainBleed(span);
            guard++;
        }

        Assert.That(span[arm].Atrophied, Is.True);
        Assert.That(span[head].Health, Is.EqualTo(headFull));
        var chest = span[LimbWoundRules.Index(LimbZone.Torso)].Health;
        LimbWoundRules.DrainBleed(span);
        Assert.That(span[LimbWoundRules.Index(LimbZone.Torso)].Health, Is.LessThan(chest));
        Assert.That(span[head].Health, Is.EqualTo(headFull));

        span[arm].Severed = true;
        span[arm].Bleed = LimbWoundRules.StumpBleed;
        LimbWoundRules.DrainBleed(span);
        Assert.That(span[head].Health, Is.EqualTo(headFull));
        LimbWoundRules.Bandage(ref span[arm]);
        Assert.That(span[arm].Bleed, Is.EqualTo(0f));
    }

    [Test]
    public void ASouthShotUsesTheAimAndASideShotUsesTheBullet()
    {
        var target = Vector2.Zero;
        var south = LimbAim.Landing(new Vector2(0f, -4f), new Vector2(0f, -0.35f), new Vector2(0f, -0.7f), target);
        var side = LimbAim.Landing(new Vector2(4f, 0f), new Vector2(0.35f, -0.05f), new Vector2(4f, 0f), target);
        var fromSouth = new Vector2(0.3f, -1.1f);
        var rim = new Vector2(0.15f, -0.35f);
        var right = LimbAim.Body(LimbAim.Landing(fromSouth, rim, new Vector2(-0.45f, 0.05f), target), Direction.South);
        var left = LimbAim.Body(LimbAim.Landing(fromSouth, rim, new Vector2(0.45f, 0.05f), target), Direction.South);
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.ZoneFromHit(south.X, south.Y), Is.EqualTo(LimbZone.FootLeft).Or.EqualTo(LimbZone.FootRight).Or.EqualTo(LimbZone.LegLeft).Or.EqualTo(LimbZone.LegRight));
            Assert.That(south.Y, Is.LessThan(-0.5f));
            Assert.That(LimbWoundRules.ZoneFromHit(side.X, side.Y), Is.EqualTo(LimbZone.ArmRight));
            Assert.That(LimbWoundRules.ZoneFromHit(right.X, right.Y), Is.EqualTo(LimbZone.ArmRight));
            Assert.That(LimbWoundRules.ZoneFromHit(left.X, left.Y), Is.EqualTo(LimbZone.ArmLeft));
            Assert.That(LimbWoundRules.HarvestSeconds(LimbZone.Head, 0.4f), Is.GreaterThan(LimbWoundRules.HarvestSeconds(LimbZone.HandLeft, 0.4f)));
            Assert.That(LimbWoundRules.HarvestSeconds(LimbZone.Head, 3f), Is.LessThan(LimbWoundRules.HarvestSeconds(LimbZone.Head, 0.4f)));
            Assert.That(LimbWoundRules.CorpseKelvin(0f) - Atmospherics.T0C, Is.EqualTo(37f).Within(0.01f));
            Assert.That(LimbWoundRules.CorpseKelvin(10f) - Atmospherics.T0C, Is.EqualTo(27f).Within(0.01f));
        });
    }

    [Test]
    public void ABlastEmbedsShrapnelAndBurnsBareSkin()
    {
        var state = new LimbZoneState { Health = LimbWoundRules.LimbHealth };
        var hit = LimbWoundRules.ApplyBlast(ref state, 12f, 12f, 12f, 0f);
        var covered = new LimbZoneState { Health = LimbWoundRules.LimbHealth, Severed = true };
        var skipped = LimbWoundRules.ApplyBlast(ref covered, 12f, 12f, 12f, 0f);
        var arm = new LimbZoneState { Embedded = true };
        var fine = new LimbZoneState();
        var leg = new LimbZoneState { Embedded = true };

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(state.Embedded, Is.True);
            Assert.That(state.Bleed, Is.GreaterThan(0f));
            Assert.That(state.Skin, Is.GreaterThanOrEqualTo(LimbWoundRules.BurnFirst));
            Assert.That(state.Bone, Is.EqualTo(12f));
            Assert.That(state.Health, Is.LessThan(LimbWoundRules.LimbHealth));
            Assert.That(skipped, Is.False);
            Assert.That(LimbWoundRules.CanGrasp(arm, fine, fine, fine), Is.True);
            Assert.That(LimbWoundRules.CanGrasp(arm, fine, arm, fine), Is.False);
            Assert.That(LimbWoundRules.Limp(leg, true), Is.True);
            Assert.That(LimbWoundRules.SlowHands(arm, fine, true), Is.True);
        });
    }

    [Test]
    public void AnArmCarriesItsHandAndALegCarriesItsFoot()
    {
        LimbZone child;
        Assert.Multiple(() =>
        {
            Assert.That(LimbWoundRules.TryCarried(LimbZone.ArmLeft, out child), Is.True);
            Assert.That(child, Is.EqualTo(LimbZone.HandLeft));
            Assert.That(LimbWoundRules.TryCarried(LimbZone.ArmRight, out child), Is.True);
            Assert.That(child, Is.EqualTo(LimbZone.HandRight));
            Assert.That(LimbWoundRules.TryCarried(LimbZone.LegLeft, out child), Is.True);
            Assert.That(child, Is.EqualTo(LimbZone.FootLeft));
            Assert.That(LimbWoundRules.TryCarried(LimbZone.LegRight, out child), Is.True);
            Assert.That(child, Is.EqualTo(LimbZone.FootRight));
            Assert.That(LimbWoundRules.TryCarried(LimbZone.HandLeft, out child), Is.False);
            Assert.That(LimbWoundRules.TryCarried(LimbZone.FootRight, out child), Is.False);
            Assert.That(LimbWoundRules.TryCarried(LimbZone.Torso, out child), Is.False);
            var fromTheWest = new Vector2(-0.5f, 0f);
            var facingYou = LimbAim.Body(fromTheWest, Direction.South);
            var turnedAround = LimbAim.Body(fromTheWest, Direction.North);
            Assert.That(LimbWoundRules.ZoneFromHit(facingYou.X, facingYou.Y), Is.EqualTo(LimbZone.ArmRight));
            Assert.That(LimbWoundRules.ZoneFromHit(turnedAround.X, turnedAround.Y), Is.EqualTo(LimbZone.ArmLeft));
            Assert.That(facingYou.Y, Is.EqualTo(turnedAround.Y));
            var half = new LimbZoneState { Health = LimbWoundRules.LimbHealth * 0.5f };
            Assert.That(LimbScan.Shade(LimbZone.ArmLeft, half), Is.EqualTo(LimbScan.Bad));
            Assert.That(LimbScan.Shade(LimbZone.HandLeft, new LimbZoneState { Severed = true }), Is.EqualTo(LimbScan.Missing));
        });
    }

    private static void AssertOwns(LimbAimBand[] bands, float lift)
    {
        float[] across = { -1.2f, -0.5f, -0.05f, 0.05f, 0.5f, 1.2f };
        float[] up = { -1.2f, -0.5f, -0.25f, 0.02f, 0.2f, 0.7f };
        foreach (var band in bands)
        {
            var found = false;
            foreach (var x in across)
            {
                foreach (var height in up)
                {
                    if (!band.Holds(x, height))
                        continue;

                    if (LimbAim.Zone(x, height - lift, bands, lift) != band.Zone)
                        continue;

                    found = true;
                }
            }

            Assert.That(found, Is.True, band.Zone.ToString());
        }
    }

    private static LimbZone Mirror(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.ArmLeft => LimbZone.ArmRight,
            LimbZone.ArmRight => LimbZone.ArmLeft,
            LimbZone.LegLeft => LimbZone.LegRight,
            LimbZone.LegRight => LimbZone.LegLeft,
            LimbZone.HandLeft => LimbZone.HandRight,
            LimbZone.HandRight => LimbZone.HandLeft,
            LimbZone.FootLeft => LimbZone.FootRight,
            LimbZone.FootRight => LimbZone.FootLeft,
            _ => zone,
        };
    }

    private static LimbStrike Strike(
        LimbZone zone,
        LimbMark mark,
        float amount,
        float roll,
        bool projectile = false,
        bool face = false,
        bool grip = false,
        bool ricochet = false,
        bool shield = false,
        float pierce = 0f,
        float multiplier = 1f,
        bool aimed = false,
        bool behindArmor = false,
        float secondary = 1f,
        float cut = 1f,
        bool sharp = false,
        bool pellet = false,
        float roundBone = 0f,
        float roundOrgan = 1f,
        float roundBleed = 0f,
        float severChance = 1f)
    {
        return new LimbStrike(zone, mark, amount, roll, pierce, projectile, face, grip, ricochet, shield, multiplier, aimed, behindArmor, secondary, Cut: cut, Sharp: sharp, Pellet: pellet, RoundBone: roundBone, RoundOrgan: roundOrgan, RoundBleed: roundBleed, SeverChance: severChance);
    }
}
