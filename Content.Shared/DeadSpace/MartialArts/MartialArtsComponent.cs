using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.MartialArts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MartialArtsComponent : Component
{
    [DataField]
    public string? ActionAtackOne;

    [DataField]
    public string? ActionAtackTwo;

    [DataField]
    public string? ActionAtackThree;

    [DataField]
    public string? ActionAtackFour;

    [DataField]
    [AutoNetworkedField]
    public MartialArtsForms? MartialArtsForm;
}
public enum MartialArtsForms
{
    CorporateJudo,
    Arkalyse,
    SmokingCarp,
}

