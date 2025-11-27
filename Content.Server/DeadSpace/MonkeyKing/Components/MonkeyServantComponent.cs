// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.MonkeyKing.Components;

[RegisterComponent]
public sealed partial class MonkeyServantComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float GetDamageMulty = 1f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float SpeedMulty = 1f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool IsBuffed = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUntilEndBuff = TimeSpan.Zero;
}
