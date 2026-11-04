// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbDollHud : Control
{
    public readonly LimbDollView Doll;

    public LimbDollHud()
    {
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = false;
        VerticalExpand = false;
        Doll = new LimbDollView();
        MinSize = Doll.MinSize;
        SetSize = Doll.MinSize;
        AddChild(Doll);
    }
}
