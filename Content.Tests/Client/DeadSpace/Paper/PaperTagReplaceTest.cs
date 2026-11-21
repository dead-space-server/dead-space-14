// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Paper;
using NUnit.Framework;
using static Content.Shared.Paper.PaperComponent;

namespace Content.Tests.Client.DeadSpace.Paper;

[TestFixture]
public sealed class PaperTagReplaceTest
{
    [Test]
    public void ReplacesOnlyTheNthCheckTag()
    {
        var text = "a [check] b [check] c [check]";
        var result = PaperSystem.ReplaceNthCheckTag(text, 1, PaperCheckState.Check);

        Assert.That(result, Is.EqualTo("a [check] b [check=1] c [check]"));
    }

    [Test]
    public void FilledCheckboxCanBeChangedAgain()
    {
        var text = "a [check=1] b [check] c [check=2]";
        var result = PaperSystem.ReplaceNthCheckTag(text, 0, PaperCheckState.Cross);

        Assert.That(result, Is.EqualTo("a [check=2] b [check] c [check=2]"));
    }

    [Test]
    public void OutOfRangeIndexReturnsOriginalText()
    {
        var text = "one [check] two";

        Assert.That(PaperSystem.ReplaceNthCheckTag(text, 5, PaperCheckState.Check), Is.EqualTo(text));
    }

    [Test]
    public void TextWithoutCheckTagIsUnchanged()
    {
        var text = "nothing to replace here";

        Assert.That(PaperSystem.ReplaceNthCheckTag(text, 0, PaperCheckState.Check), Is.EqualTo(text));
    }

    [Test]
    public void ReplacesTagAdjacentToText()
    {
        var result = PaperSystem.ReplaceNthCheckTag("[check=2]answer", 0, PaperCheckState.Blank);

        Assert.That(result, Is.EqualTo("[check=0]answer"));
    }

    [Test]
    public void SimilarTagNamesAreNotCounted()
    {
        var result = PaperSystem.ReplaceNthCheckTag("[checkeraaa] [check]", 0, PaperCheckState.Check);

        Assert.That(result, Is.EqualTo("[checkeraaa] [check=1]"));
    }

    [Test]
    public void FreezeKeepsFilledMarksAndPrintsEmptySquares()
    {
        var result = PaperSystem.FreezeCheckTags("x [check] y [check=1] z [check=2]");

        Assert.That(result, Is.EqualTo("x ☐ y ☑ z ☒"));
    }

    [Test]
    public void FreezeLeavesNonCheckTagsAlone()
    {
        var text = "[checkeraaa] un[closed";

        Assert.That(PaperSystem.FreezeCheckTags(text), Is.EqualTo(text));
    }
}
