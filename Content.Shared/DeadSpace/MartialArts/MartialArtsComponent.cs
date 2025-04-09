using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.MartialArts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MartialArtsComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public MartialArtsForms MartialArtsForm = MartialArtsForms.Arkalyse;

    [DataField]
    public bool IsDamageAttack = false;

    [DataField]
    public ProtoId<CombatTechPrototype> TechDataOne;

    [DataField]
    public ProtoId<CombatTechPrototype> TechDataTwo;

    [DataField]
    public ProtoId<CombatTechPrototype> TechDataThree;

    [DataField]
    public int TypeAtack = 0;
}
public enum MartialArtsForms
{
    CorporateJudo,
    Arkalyse,
    SmokingCarp,
}

