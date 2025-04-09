using Content.Shared.DeadSpace.MartialArts;
using Robust.Shared.GameStates;

namespace Content.Server.DeadSpace.MartialArts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MartialArtsTrainingCarpComponent : Component
{
    [DataField]
    public float? LearnTime;

    [DataField]
    public float AddAtackRate = 1.0f;

    [DataField]
    [AutoNetworkedField]
    public MartialArtsForms MartialArtsForm;

    [DataField]
    public string? ItemAfterLerning;

    [DataField]
    public string? ActionAtackOne;

    [DataField]
    public string? ActionAtackTwo;

    [DataField]
    public string? ActionAtackThree;

    [DataField]
    public string? ActionAtackFour;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MartialArtsTrainingArkalyseComponent : Component
{
    [DataField]
    public float? LearnTime;

    [DataField]
    public float AddAtackRate = 1.0f;

    [DataField]
    [AutoNetworkedField]
    public MartialArtsForms MartialArtsForm;

    [DataField]
    public string? ItemAfterLerning;

    [DataField]
    public string? ActionAtackOne;

    [DataField]
    public string? ActionAtackTwo;

    [DataField]
    public string? ActionAtackThree;

    [DataField]
    public string? ActionAtackFour;
}
