using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Armutant;

[RegisterComponent]
public sealed partial class ArmutantComponent : Component
{
    [DataField]
    public ArmutantArms? SelectedArm;

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
    public SoundSpecifier? SoundEffect;

    [DataField]
    public float MaxDashRange = 10.0f;

    [DataField]
    public float CollisionRadiusDash = 1.5f;

    [DataField]
    public string TypeDamage = "Slash";

    [DataField]
    public int AmountDamage = 25;

    [DataField]
    public float KnockbackForce = 10f;

    [DataField]
    public EntProtoId? SelfEffect;

    [DataField]
    public EntProtoId? TalonBladePrototype;

    [DataField]
    public SoundSpecifier? MeatSound;
}
[RegisterComponent]
public sealed partial class ArmutantFistActionComponent : Component
{
    [DataField]
    public SoundSpecifier? SoundEffect;

    [DataField]
    public int TimeRecovery = 15;

    [DataField]
    public float BuffSpeedSprint = 5.5f;

    [DataField]
    public float BuffSpeedWalk = 3.0f;

    [DataField]
    public float AmountReagent = 10.0f;

    [DataField]
    public string Reagent = "Shrapnel";

    [DataField]
    public float StunTime = 2.0f;

    [DataField]
    public EntProtoId? SelfEffect;

    [DataField]
    public EntProtoId? TargetEffect;

    [DataField]
    public string HandEffect = "TentacleArmsHand"; // TryCreateBeam принимает только строковые значения, странно конечно
}
public enum ArmutantArms
{
    BladeArm,
    FistArm,
    ShieldArm,
    GunArm,
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
