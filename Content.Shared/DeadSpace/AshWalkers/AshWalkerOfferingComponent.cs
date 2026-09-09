using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent]
public sealed partial class AshWalkerOfferingComponent : Component
{
    [DataField]
    public string Solution = "food";

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> RequiredReagents = new();
}
