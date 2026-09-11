using Content.Server.StationEvents.Events;

namespace Content.Server.StationEvents.Components;

[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm] // DS14
[RegisterComponent, Access(typeof(RandomSentienceRule))]
public sealed partial class RandomSentienceRuleComponent : Component
{
    [DataField]
    public int MinSentiences = 1;

    [DataField]
    public int MaxSentiences = 1;
}
