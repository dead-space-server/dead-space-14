// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture]
public sealed class LimbImplantSlotsTest
{
    [Test]
    public void BackBelongsToTheTorsoAndEyesBelongToTheHead()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LimbImplantSlots.ZoneOf(LimbImplantSlots.Back), Is.EqualTo(LimbZone.Torso));
            Assert.That(LimbImplantSlots.IsBack(LimbImplantSlots.Back), Is.True);
            Assert.That(LimbImplantSlots.ZoneOf(LimbImplantSlots.Eyes), Is.EqualTo(LimbZone.Head));
            Assert.That(LimbImplantSlots.ZoneOf(LimbImplantSlots.HandLeft), Is.EqualTo(LimbZone.HandLeft));
            Assert.That(LimbImplantSlots.ContainerId(LimbImplantSlots.Eyes), Is.EqualTo("limb-implant-Eyes"));
        });
    }
}
