using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Armutant;

[RegisterComponent]
public sealed partial class ArmutantComponent : Component
{
    [DataField]
    public ArmutantArms? SelectedArm;

    [DataField]
    public BladeArm? SelectAction;

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
    public ArmutantActionComponent? SelectedArmComp;

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
        "ActionToggleGunZoom",
        "ActionToggleGunSmoke",
    };

    public readonly List<EntityUid> ArmutantActionEntities = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesBlade = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesFist = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesShield = new() { };

    public readonly List<EntityUid> ArmutantActionEntitiesGun = new() { };
}
[RegisterComponent]
public sealed partial class ArmutantActionComponent : Component
{
    [DataField]
    public ArmutantArms List = default!;

    [DataField]
    public EntProtoId? ExitToStasisEffect;

    [DataField]
    public SoundSpecifier? MeatSound;

    [DataField]
    public float TimeInStasis = 30.0f;
}
[RegisterComponent]
public sealed partial class ArmutantBladeActionComponent : Component
{
    [DataField]
    public BladeArm List = default!;


}
public enum ArmutantArms
{
    BladeArm,
    FistArm,
    ShieldArm,
    GunArm,
}
public enum BladeArm
{
    Dash,
    SpawnTalon,
    Spore,
}
public enum FistArm
{
    VoidHold,
    MendSelf,
    BuffSpeed,
}
public enum ShieldArm
{
    Armor,
    Stun,
    VoidShield,
}
public enum GunArm
{
    Zoom,
    Smoke,
}
