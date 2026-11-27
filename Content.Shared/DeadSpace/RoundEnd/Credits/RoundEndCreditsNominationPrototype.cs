// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.RoundEnd.Credits;

[Prototype]
public sealed partial class RoundEndCreditsNominationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public RoundEndCreditsStat Stat;

    [DataField]
    public float Minimum = 1f;

    [DataField]
    public RoundEndCreditsNominationKind Kind = RoundEndCreditsNominationKind.Fun;

    [DataField]
    public bool AntagOnly;

    [DataField]
    public bool CrewOnly;
}

[Prototype]
public sealed partial class RoundEndCreditsVoteNominationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;
}
