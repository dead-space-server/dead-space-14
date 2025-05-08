// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.Drug.Components;

[RegisterComponent]
public sealed partial class DrugTestStickComponent : Component
{
    [DataField("dna"), ViewVariables(VVAccess.ReadOnly)]
    public string DNA = String.Empty;

    [DataField]
    public int DependencyLevel = 0;

    [DataField]
    public float AddictionLevel = 0;

    [DataField]
    public float Tolerance = 0;

    [DataField]
    public float WithdrawalLevel = 0;

    [DataField]
    public float ThresholdTime = 0;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool IsUsed = false;

}
