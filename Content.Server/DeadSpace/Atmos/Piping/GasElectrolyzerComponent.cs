// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;

namespace Content.Server.DeadSpace.Atmos.Piping;

[RegisterComponent]
public sealed partial class GasElectrolyzerComponent : Component
{
    [DataField("inlet")]
    public string InletName = "inlet";

    [DataField("outlet")]
    public string OutletName = "outlet";

    [DataField]
    public float TransferRate = 20f;

    [DataField]
    public float MaxOutletPressure = Atmospherics.MaxOutputPressure;

    [DataField]
    public Dictionary<Gas, GasElectrolyzerReaction> Reactions = new();

    [DataField]
    public bool Enabled = true;
}

[DataDefinition]
public sealed partial class GasElectrolyzerReaction
{
    [DataField(required: true)]
    public Dictionary<Gas, float> Products = new();

    [DataField]
    public float Energy = 0f;
}
