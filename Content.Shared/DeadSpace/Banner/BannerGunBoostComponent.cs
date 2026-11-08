using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Banner;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBannerSystem))]
public sealed partial class BannerGunBoostComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float FireRateBonus = 0.5f;
}
