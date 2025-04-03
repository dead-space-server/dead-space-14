using Content.Server.DeadSpace.Armutant.Objectives.CreateMapObjective;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Armutant.Objectives;

[RegisterComponent, Access(typeof(ObjectiveCreateMapSystem))]
public sealed partial class ObjectiveCreateMapComponent : Component
{
    /// <summary>
    /// Путь к карте, которая будет загружена
    /// </summary>
    [DataField]
    public string MapPath = "/Maps/DungeonArmutant/dungeon_1.yml";

    [DataField]
    public int MapId;

    [DataField]
    public EntProtoId? SelfEffect = "VoidTeleportSelfEffect";
}
