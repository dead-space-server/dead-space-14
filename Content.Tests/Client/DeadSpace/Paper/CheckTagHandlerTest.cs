// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.UserInterface.RichText;
using NUnit.Framework;
using Robust.Shared.Utility;

namespace Content.Tests.Client.DeadSpace.Paper;

[TestFixture]
public sealed class CheckTagHandlerTest
{
    [Test]
    public void InteractiveChecksAreMarkedOnlyInRenderedCopy()
    {
        var message = FormattedMessage.FromMarkupPermissive("a [check] b [check=1] c");
        var rendered = CheckTagHandler.WithInteractiveChecks(message);

        Assert.That(rendered.Count, Is.EqualTo(message.Count));
        Assert.That(rendered.Nodes[1].Attributes.ContainsKey("ds14-interactive"), Is.True);
        Assert.That(rendered.Nodes[3].Attributes.ContainsKey("ds14-interactive"), Is.True);
        Assert.That(rendered.Nodes[3].Value.LongValue, Is.EqualTo(1));

        Assert.That(message.Nodes[1].Attributes.ContainsKey("ds14-interactive"), Is.False);
        Assert.That(message.Nodes[3].Attributes.ContainsKey("ds14-interactive"), Is.False);
    }

    [Test]
    public void OtherTagsAreKeptAsIs()
    {
        var message = FormattedMessage.FromMarkupPermissive("[color=red]a [check] b[/color]");
        var rendered = CheckTagHandler.WithInteractiveChecks(message);

        Assert.That(rendered.Nodes[0].Name, Is.EqualTo("color"));
        Assert.That(rendered.Nodes[0].Attributes.ContainsKey("ds14-interactive"), Is.False);
        Assert.That(rendered.Nodes[^1].Name, Is.EqualTo("color"));
        Assert.That(rendered.Nodes[^1].Closing, Is.True);
    }
}
