// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Atmos.Piping;

[RegisterComponent]
public sealed partial class GasHeatExchangerComponent : Component
{
    [DataField("pipe")]
    public string PipeName = "pipe";

    [DataField]
    public float TransferFraction = 0.25f;
}
