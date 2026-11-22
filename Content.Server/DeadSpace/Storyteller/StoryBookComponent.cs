// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Storyteller;

namespace Content.Server.DeadSpace.Storyteller;

[RegisterComponent]
public sealed partial class StoryBookComponent : Component
{
    [ViewVariables]
    public string Title = "";

    [ViewVariables]
    public List<StoryPage> Pages = new();
}

public sealed class StoryPage
{
    public string Caption = "";
    public List<StoryCue> Cues = new();

    public StoryPage Clone()
    {
        return new StoryPage { Caption = Caption, Cues = new List<StoryCue>(Cues) };
    }
}
