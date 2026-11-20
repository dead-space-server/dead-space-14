// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Surgery;

[Prototype]
public sealed partial class SurgeryDecayPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public float Organ = 900f;

    [DataField]
    public float Brain = 600f;

    [DataField]
    public LocId RottenReason = "surgery-brain-rotten-reason";
}
