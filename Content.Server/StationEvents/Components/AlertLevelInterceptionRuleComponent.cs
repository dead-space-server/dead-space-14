using Content.Server.StationEvents.Events;
using Content.Server.AlertLevel;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Components;

[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm] // DS14
[RegisterComponent, Access(typeof(AlertLevelInterceptionRule))]
public sealed partial class AlertLevelInterceptionRuleComponent : Component
{
    /// <summary>
    /// Alert level to set the station to when the event starts.
    /// </summary>
    [DataField]
    public string AlertLevel = "blue";
}
