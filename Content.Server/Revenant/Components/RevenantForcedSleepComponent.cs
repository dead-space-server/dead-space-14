// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;

namespace Content.Server.Revenant.Components;

[RegisterComponent]
public sealed partial class RevenantForcedSleepComponent : Component
{

    [ViewVariables(VVAccess.ReadOnly)]
    public float Accumulator = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SleepDelay = 30f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float PopupDelay = 10f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float PopupStep = 10f;

    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 DurationOfSleep = new Vector2(5, 10);
}
