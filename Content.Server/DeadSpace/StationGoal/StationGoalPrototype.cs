// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Paper;

namespace Content.Server.DeadSpace.StationGoal;

[Prototype("stationGoal")]
public sealed partial class StationGoalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField(required: true)]
    public ResPath Text = default!;

    [DataField]
    public int? ModifyStationBalance;
    [DataField]
    public List<StampDisplayInfo>? ExtraStamps;
}
