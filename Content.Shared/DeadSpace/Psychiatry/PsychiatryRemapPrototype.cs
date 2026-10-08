// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Psychiatry;

[Prototype]
public sealed partial class PsychiatryRemapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Sprite = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public List<PsychiatryRemapPool> Pools = new();

    [DataField]
    public int Weight = 1;

    [DataField]
    public bool Prefer;

    [DataField]
    public bool Wall;
}
