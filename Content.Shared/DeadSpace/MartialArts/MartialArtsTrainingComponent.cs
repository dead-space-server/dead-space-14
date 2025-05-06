using Robust.Shared.GameStates;

namespace Content.Server.DeadSpace.MartialArts;

[RegisterComponent]
public sealed partial class MartialArtsTrainingCarpComponent : Component
{
    [DataField]
    public float AddAtackRate = 1.0f;

    [DataField]
    public MartialArtsForms MartialArtsForm { get; set; } = MartialArtsForms.SmokingCarp;

    [DataField]
    public string? ItemAfterLerning;
}

[RegisterComponent]
public sealed partial class MartialArtsTrainingArkalyseComponent : Component
{
    [DataField]
    public float AddAtackRate = 1.0f;

    [DataField]
    public MartialArtsForms MartialArtsForm { get; set; } = MartialArtsForms.Arkalyse;

    [DataField]
    public string? ItemAfterLerning;
}

public enum MartialArtsForms
{
    CorporateJudo,
    Arkalyse,
    SmokingCarp,
}
