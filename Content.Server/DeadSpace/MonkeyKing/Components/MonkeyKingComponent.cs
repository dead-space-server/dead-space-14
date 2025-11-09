// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.MonkeyKing.Components;

[RegisterComponent]
public sealed partial class MonkeyKingComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId ActionArmy = "ActionArmy";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionArmyEntity;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId ActionKingBuff = "ActionKingBuff";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionKingBuffEntity;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId ActionGiveIntelligence = "ActionGiveIntelligence";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionGiveIntelligenceEntity;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntProtoId ServantMonkeyProto = "MobMonkeyServant";

    [DataField]
    public SoundSpecifier? ArmySound = null;

    [DataField]
    public SoundSpecifier? KingBuffSound = null;

    [DataField]
    public SoundSpecifier? GiveIntelligenceSound = null;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public List<EntProtoId> WeaponList = new List<EntProtoId>
    {
        "RollingPin",
        "Cane",
        "WeaponRevolverInspector",
        "Stunbaton",
        "GrenadeDummy",
        "SpearBone",
        "Spear",
        "BaseBallBat",
        "KitchenKnife",
        "RitualDagger",
        "Scalpel",
        "Wrench",
        "WelderMini",
        "Saw",
        "Nettle",
        "WeaponPistolMk58",
        "MopItem",
        "ToolboxSyndicate",
        "Bola",
        "Shovel",
        "ThrowingKnife",
        "Shiv",
        "WeaponLaserSvalinn",
        "FireExtinguisher",
        "HydroponicsToolHatchet",
        "Shovel",
        "Crowbar",
        "OxygenTankFilled"
    };

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float RangeBuff = 15f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float BuffDuration = 10f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float SpeedBuff = 1.5f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float GetDamageBuff = 0.5f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float GiveIntelligenceDuration = 2f;
}
