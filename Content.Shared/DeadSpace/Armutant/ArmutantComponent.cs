using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Armutant;

[RegisterComponent, NetworkedComponent]
public sealed partial class ArmutantComponent : Component
{
    [DataField]
    public TimeSpan? TimeToExitStasis;

    [DataField]
    public EntProtoId? ExitToStasisEffect;

    [DataField]
    public ArmutantArms? SelectedArm;

    [DataField]
    public SoundSpecifier? MeatSound = new SoundPathSpecifier("/Audio/_DeadSpace/Armutant/sound_effects_meat.ogg");

    [DataField]
    public EntProtoId BladeArmPrototype = "BladeArmutant";

    [DataField]
    public EntProtoId FistArmPrototype = "FistArmutant";

    [DataField]
    public EntProtoId ShieldArmPrototype = "ShieldArmutant";

    [DataField]
    public EntProtoId GunArmPrototype = "GunArmutant";

    [DataField]
    public Dictionary<string, EntityUid?> Equipment = new();

    [DataField]
    public Dictionary<string, EntityUid> EquipmentArmor = new();

    [DataField]
    public bool IsInStasis = false;

    [DataField]
    public int DamageAmountGib = 1000;

    [DataField]
    public string DamageTypeGib = "Blunt";

    public readonly List<EntProtoId> ArmutantAbility = new()
    {
        "ActionToggleBlade",
        "ActionToggleShield",
        "ActionToggleFist",
        "ActionToggleGun",
        "ActionToggleEnterStasis",
    };

    public readonly List<EntProtoId> ArmutantAbilityBlade = new()
    {
        "ActionToggleSpawnTalon",
        "ActionToggleDash",
        "ActionToggleSporeBlade",
    };

    public readonly List<EntProtoId> ArmutantAbilityFist = new()
    {
        "ActionToggleFistVoidHold",
        "ActionToggleFistMendSelf",
        "ActionToggleFistBuffSpeed",
    };

    public readonly List<EntProtoId> ArmutantAbilityShield = new()
    {
        "ActionToggleShieldArmor",
        "ActionToggleShieldStun",
        "ActionToggleVoidShield",
    };

    public readonly List<EntProtoId> ArmutantAbilityGun = new()
    {
        "ActionToggleGunSmoke",
    };

    public readonly List<EntityUid> ArmutantActionEntities = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesBlade = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesFist = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesShield = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesGun = new() { };
}
[Serializable]
public enum ArmutantArms
{
    BladeArm,
    FistArm,
    ShieldArm,
    GunArm
}
