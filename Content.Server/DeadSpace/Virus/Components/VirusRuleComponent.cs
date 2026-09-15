// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Virus.Systems;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.Virus.Prototypes;

namespace Content.Server.DeadSpace.Virus.Components;

[Content.Server.DeadSpace.CentComm.AllowedGameRuleOnCentComm]
[RegisterComponent, Access(typeof(VirusRule))]
public sealed partial class VirusRuleComponent : Component
{
    /// <summary>
    ///     Симптомы при случайном вирусе
    /// </summary>
    [DataField(required: true)]
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<DangerIndicatorSymptom, int> SymptomsByDanger;

    /// <summary>
    ///     Количество тел при случайном вирусе
    /// </summary>
    [DataField(required: true)]
    [ViewVariables(VVAccess.ReadOnly)]
    public int BodyCount;

    /// <summary>
    ///     Количество первичных заражённых
    /// </summary>
    [DataField(required: true)]
    [ViewVariables(VVAccess.ReadOnly)]
    public int NumberPrimaryPacienst;

    /// <summary>
    ///     Если вирус не случайный
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public VirusData? Data;
}
