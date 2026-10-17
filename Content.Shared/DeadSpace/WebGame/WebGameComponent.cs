// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.WebGame;

[RegisterComponent]
public sealed partial class WebGameComponent : Component
{
    [DataField]
    public List<WebGameEntry> Games = new();
}

[DataDefinition]
public sealed partial class WebGameEntry
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public string Url = string.Empty;
}
