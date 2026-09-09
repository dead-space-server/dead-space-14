// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Atmos.Piping;

[RegisterComponent]
public sealed partial class GasTemperaturePumpComponent : Component
{
    [DataField("inlet")]
    public string InletName = "inlet";

    [DataField("outlet")]
    public string OutletName = "outlet";

    [DataField]
    public float TransferFraction = 0.375f;
}
