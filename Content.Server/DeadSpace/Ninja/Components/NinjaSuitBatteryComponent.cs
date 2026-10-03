// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaSuitBatteryComponent : Component
{
    [DataField]
    public float AutoRechargeRate;

    [DataField]
    public TimeSpan AutoRechargePauseTime;

    [DataField]
    public TimeSpan? NextAutoRecharge;
}