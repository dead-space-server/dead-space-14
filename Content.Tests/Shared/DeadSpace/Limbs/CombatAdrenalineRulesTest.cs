using Content.Shared.DeadSpace.Body.Limbs;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Limbs;

[TestFixture, Parallelizable]
public sealed class CombatAdrenalineRulesTest
{
    [Test]
    public void SurgeNeedsTheImplantCombatAndAHeavyHit()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Start(false, 40f, false), Is.False);
            Assert.That(Start(true, 19f, false), Is.False);
            Assert.That(Start(true, 20f, false), Is.True);
            Assert.That(Start(true, 20f, false, false), Is.False);
            Assert.That(Start(true, 40f, true), Is.False);
        });
    }

    [Test]
    public void HitsDuringTheSurgeRefreshItAndAPauseLetsItEnd()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CombatAdrenalineRules.TryRefresh(true, 5f), Is.True);
            Assert.That(CombatAdrenalineRules.TryRefresh(true, 0f), Is.False);
            Assert.That(CombatAdrenalineRules.TryRefresh(false, 30f), Is.False);
            Assert.That(Start(true, 30f, false), Is.True);
        });
    }

    [Test]
    public void BothSeveredLegsDropTheFighter()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CombatAdrenalineRules.CanStayUp(false, false), Is.True);
            Assert.That(CombatAdrenalineRules.CanStayUp(true, false), Is.True);
            Assert.That(CombatAdrenalineRules.CanStayUp(false, true), Is.True);
            Assert.That(CombatAdrenalineRules.CanStayUp(true, true), Is.False);
        });
    }

    [Test]
    public void ImplantSurgeStaysUpOnlyWhileHitsContinueAndLegsRemain()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CombatAdrenalineRules.TryStart(true, CombatAdrenalineRules.HeavyDamage, false, true), Is.True);
            Assert.That(CombatAdrenalineRules.TryStart(false, CombatAdrenalineRules.HeavyDamage, false, true), Is.False);
            Assert.That(CombatAdrenalineRules.TryRefresh(true, 1f), Is.True);
            Assert.That(CombatAdrenalineRules.TryRefresh(true, 0f), Is.False);
            Assert.That(CombatAdrenalineRules.SurgeSeconds, Is.EqualTo(12f));
            Assert.That(CombatAdrenalineRules.CanStayUp(true, true), Is.False);
        });
    }

    [Test]
    public void PositiveDamageIgnoresHealing()
    {
        Assert.That(CombatAdrenalineRules.Positive(new[] { 15f, -10f, 6f, 0f }), Is.EqualTo(21f));
    }

    private static bool Start(bool implant, float damage, bool active, bool combatMode = true)
    {
        return CombatAdrenalineRules.TryStart(implant, damage, active, combatMode);
    }
}
