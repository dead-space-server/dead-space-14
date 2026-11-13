using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Banner;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BannerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? OffenseAction;

    [DataField, AutoNetworkedField]
    public EntityUid? DefenseAction;

    [DataField, AutoNetworkedField]
    public EntityUid? ConcentrateFireAction;

    [DataField]
    public EntProtoId OffenseActionId = "ActionBannerOffense";

    [DataField]
    public EntProtoId DefenseActionId = "ActionBannerDefense";

    [DataField]
    public EntProtoId ConcentrateFireActionId = "ActionBannerConcentrateFire";

    [DataField]
    public float SpeedMultiplier = 1.15f;

    [DataField]
    public TimeSpan OffenseDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public float DamageCoefficient = 0.75f;

    [DataField]
    public TimeSpan DefenseDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public float FireRateBonus = 0.5f;

    [DataField]
    public TimeSpan ConcentrateFireDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public float Range = 7f;

    [DataField]
    public EntProtoId OffenseStatusEffect = "StatusEffectBannerOffense";
}
