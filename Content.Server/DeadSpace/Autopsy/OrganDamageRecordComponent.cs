// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Autopsy;

[RegisterComponent]
public sealed partial class OrganDamageRecordComponent : Component
{
    public string? OwnerName;

    public List<OrganDamageRecord> Records = new();

    public List<string> Traces = new();
}

[DataDefinition]
public sealed partial class OrganDamageRecord
{
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = default!;

    [DataField]
    public FixedPoint2 Amount;

    [DataField]
    public TimeSpan Time;

    [DataField]
    public string Source = string.Empty;
}
