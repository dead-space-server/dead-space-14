// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Holosign;

[RegisterComponent]
public sealed partial class LimitedHolosignProjectorComponent : Component
{
    [DataField(required: true)]
    public EntProtoId SignProto;

    [DataField]
    public int MaxBarriers = 6;

    [ViewVariables]
    public List<EntityUid> Barriers = new();
}
