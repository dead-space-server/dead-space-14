// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Wizard.Runes;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Wizard;

[TestFixture]
public sealed class RuneIncantationTest
{
    [Test]
    public void QuietSpellStaysLowercaseAndGrey()
    {
        var markup = RuneIncantation.Markup("игнис", 0, 0);
        Assert.Multiple(() =>
        {
            Assert.That(markup, Does.Contain("игнис"));
            Assert.That(markup, Does.Contain("[italic]"));
            Assert.That(markup, Does.Contain("#9a9aa8"));
        });
    }

    [Test]
    public void CapsGrowTowardsTheEnd()
    {
        var shaped = RuneIncantation.Shape("игнис глобус", 2, 0);
        Assert.Multiple(() =>
        {
            Assert.That(shaped, Does.StartWith("игнис"));
            Assert.That(shaped, Does.EndWith("УС"));
        });
    }

    [Test]
    public void LoudestSpellIsRedBigAndShouted()
    {
        var markup = RuneIncantation.Markup("разорвись", 5, 0);
        Assert.Multiple(() =>
        {
            Assert.That(markup, Does.Contain("РАЗОРВИСЬ!!"));
            Assert.That(markup, Does.Contain("#ff1a1a"));
            Assert.That(markup, Does.Contain("size=19"));
            Assert.That(markup, Does.Contain("[bold]"));
        });
    }

    [Test]
    public void AbsurdSpellAlternatesCase()
    {
        Assert.That(RuneIncantation.Shape("хонк", 1, 2), Is.EqualTo("хОнК"));
    }

    [Test]
    public void PeakAbsurdityIsRainbowAndWaves()
    {
        var markup = RuneIncantation.Markup("бальдердаш", 1, 5);
        Assert.Multiple(() =>
        {
            Assert.That(markup.Split("[color=").Length - 1, Is.EqualTo(10));
            Assert.That(markup, Does.Contain("[" + RuneIncantation.WaveTag + "]"));
        });
    }

    [Test]
    public void BracketsAreEscaped()
    {
        var markup = RuneIncantation.Markup("[bold]", 1, 0);
        Assert.That(markup, Does.Contain("\\[bold]"));
    }

    [Test]
    public void LouderReachesFurther()
    {
        Assert.That(RuneIncantation.HearingRange(5), Is.GreaterThan(RuneIncantation.HearingRange(1)));
    }
}
