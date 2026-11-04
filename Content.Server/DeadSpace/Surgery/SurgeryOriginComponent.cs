// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Surgery;

[RegisterComponent, Access(typeof(SurgeryGraftSystem))]
public sealed partial class SurgeryOriginComponent : Component
{
    [DataField]
    public string Species = string.Empty;

    [DataField]
    public string Dna = string.Empty;
}
