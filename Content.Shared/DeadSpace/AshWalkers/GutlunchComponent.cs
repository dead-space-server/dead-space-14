using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent]
public sealed partial class GutlunchComponent : Component
{
    [DataField]
    public bool IsMale;

    [DataField(required: true)]
    public EntityWhitelist FoodWhitelist = new();

    [DataField]
    public ProtoId<ReagentPrototype> FoodReagent = "UncookedAnimalProteins";

    [DataField(required: true)]
    public Solution MilkPerNutrient = new();

    [DataField]
    public TimeSpan FeedDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public FixedPoint2 PartnerMilkCost = 25;

    [DataField]
    public float PopulationRadius = 32f;

    [DataField]
    public int PopulationLimit = 6;
}
