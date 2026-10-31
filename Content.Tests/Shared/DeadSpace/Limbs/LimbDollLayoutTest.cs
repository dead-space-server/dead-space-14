using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class LimbDollLayoutTest
{
    [Test]
    public void EveryPartOwnsItsMiddleAndAMissHitsNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbDollLayout.Hit(new Vector2(15f, 6f)), Is.EqualTo(LimbZone.Eyes));
            Assert.That(LimbDollLayout.Hit(new Vector2(15f, 8f)), Is.EqualTo(LimbZone.Mouth));
            Assert.That(LimbDollLayout.Hit(new Vector2(12f, 4f)), Is.EqualTo(LimbZone.Head));
            Assert.That(LimbDollLayout.Hit(new Vector2(15f, 14f)), Is.EqualTo(LimbZone.Torso));
            Assert.That(LimbDollLayout.Hit(new Vector2(15f, 20f)), Is.EqualTo(LimbZone.Groin));
            Assert.That(LimbDollLayout.Hit(new Vector2(9f, 16f)), Is.EqualTo(LimbZone.ArmRight));
            Assert.That(LimbDollLayout.Hit(new Vector2(21f, 16f)), Is.EqualTo(LimbZone.ArmLeft));
            Assert.That(LimbDollLayout.Hit(new Vector2(11f, 26f)), Is.EqualTo(LimbZone.LegRight));
            Assert.That(LimbDollLayout.Hit(new Vector2(18f, 26f)), Is.EqualTo(LimbZone.LegLeft));

            foreach (var shape in LimbDollLayout.Shapes)
                Assert.That(LimbDollLayout.TryPart(shape.Zone, out _), Is.True, shape.Zone.ToString());

            Assert.That(LimbDollLayout.Hit(new Vector2(-1f, -1f)), Is.Null);
        });
    }

    [Test]
    public void SouthSpriteSideOwnsItsLimb()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(8.5f, 14f)), Is.EqualTo(LimbZone.ArmRight));
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(21.3f, 14f)), Is.EqualTo(LimbZone.ArmLeft));
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(8.5f, 19.5f)), Is.EqualTo(LimbZone.HandRight));
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(21.5f, 19.5f)), Is.EqualTo(LimbZone.HandLeft));
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(12.5f, 25.5f)), Is.EqualTo(LimbZone.LegRight));
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(17.5f, 25.5f)), Is.EqualTo(LimbZone.LegLeft));
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(11.6f, 30f)), Is.EqualTo(LimbZone.FootRight));
            Assert.That(LimbDollLayout.SpriteHit(new Vector2(18.4f, 30f)), Is.EqualTo(LimbZone.FootLeft));
        });
    }
}
