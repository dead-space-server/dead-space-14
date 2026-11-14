// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Hooligan.Objectives;

/// <summary>
/// Компонент подрыва для Хулигана
/// Хранит количество взорванных самодельных бомб и отделы, где они уже сработали
/// </summary>
[RegisterComponent, Access(typeof(HooliganBombConditionSystem))]
public sealed partial class HooliganBombConditionComponent : Component
{
    public int Count; // Сколько бомб взорвано в разных отделах

    public readonly HashSet<EntProtoId> DetonatedDepartments = new(); // Отделы, где бомбы уже сработали

    [DataField]
    public float Range = 30f; // Расстояние от точки отдела, на котором взрыв ещё засчитывается

    [DataField]
    public List<EntProtoId> DepartmentBeacons = new()
    {
        "DefaultStationBeaconCommand",
        "DefaultStationBeaconSecurity",
        "DefaultStationBeaconMedical",
        "DefaultStationBeaconScience",
        "DefaultStationBeaconSupply",
        "DefaultStationBeaconEngineering",
        "DefaultStationBeaconService",
        "DefaultStationBeaconAI",
    }; // Корневые маяки отделов
}
