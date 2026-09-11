using Content.Server.StationEvents.Events;

namespace Content.Server.StationEvents.Components;

[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm] // DS14
[RegisterComponent, Access(typeof(FalseAlarmRule))]
public sealed partial class FalseAlarmRuleComponent : Component
{

}
