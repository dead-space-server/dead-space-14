// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Hooligan.Objectives;

/// <summary>
/// Компонент Вандализма для Хулигана
/// Хранит количество и место задания для рисования граффити
/// </summary>
[RegisterComponent, Access(typeof(HooliganVandalismConditionSystem))]
public sealed partial class HooliganVandalismConditionComponent : Component
{
    public int GraffitiCount; // Сколько граффити нарисовано

    public EntityUid? TargetLocation; // Место задания на граффити

    [DataField]
    public float Range = 8f; // Расстояние от точки на котором рисунок ещё засчитывается.

    [DataField]
    public float HighSecurityChance = 0.4f; // Шанс выбрать охраняемое место вместо обычного

    [DataField]
    public List<EntProtoId> HighSecurityBeacons = new()
    {
        "DefaultStationBeaconSecurity",
        "DefaultStationBeaconCommand",
        "DefaultStationBeaconCMORoom",
        "DefaultStationBeaconRDRoom",
        "DefaultStationBeaconCERoom",
        "DefaultStationBeaconQMRoom",
        "DefaultStationBeaconAI",
    }; // Маяки охраняемых мест и кабинетов глав
}
