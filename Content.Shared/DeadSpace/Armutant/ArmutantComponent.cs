using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Armutant;

[RegisterComponent, NetworkedComponent]
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
    public Dictionary<string, EntityUid> EquipmentArmor = new();

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
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArmutantActionComponent : Component
{
    [DataField]
    public ArmutantArms List = default!;

    [DataField]
    public EntProtoId? ExitToStasisEffect;

    [DataField]
    public SoundSpecifier? MeatSound;

    [DataField]
    [AutoNetworkedField]
    public float TimeInStasis = 30.0f;
}
[RegisterComponent, NetworkedComponent]
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
[RegisterComponent, NetworkedComponent]
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
[RegisterComponent, NetworkedComponent]
public sealed partial class ArmutantShieldActionComponent : Component
{
    [DataField]
    public SoundSpecifier? SoundEffect = new SoundPathSpecifier("/Audio/_DeadSpace/Armutant/sound_effects_meteorimpact.ogg");

    [DataField]
    public SoundSpecifier? ReflectSound = new SoundPathSpecifier("/Audio/_DeadSpace/Armutant/sound_effetc_reflect.ogg");

    [DataField]
    public EntProtoId ArmorPrototype = "ClothingOuterArmorArmutant";

    [DataField]
    public EntProtoId ArmorHelmetPrototype = "ClothingHeadHelmetArmutant";

    [DataField]
    public float Range = 3.0f;

    [DataField]
    public float ShortRange = 0.5f;

    [DataField]
    public EntProtoId? SelfEffect = "EffectSelfStun";

    [DataField]
    public EntProtoId? EffectTarget = "EffectStun";

    [DataField]
    public float KnockbackForce = 15.0f;

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(1.0);

    [DataField]
    public TimeSpan ActiveTime = TimeSpan.FromSeconds(15.0);
}
[RegisterComponent, NetworkedComponent]
public sealed partial class ArmutantGunActionComponent : Component
{
    [DataField]
    public bool Enabled;

    [DataField]
    public Vector2 Zoom = new(1.8f, 1.8f);

    [DataField]
    public Vector2 Offset;

    [DataField]
    public EntProtoId? SpawnPrototype = "GunSmokeInstantEffect";
}
public enum ArmutantArms
{
    BladeArm,
    FistArm,
    ShieldArm,
    GunArm,
}
