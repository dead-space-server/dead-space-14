using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Banner;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBannerSystem))]
public sealed partial class BannerDefenseBuffComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float Coefficient = 0.75f;
}
