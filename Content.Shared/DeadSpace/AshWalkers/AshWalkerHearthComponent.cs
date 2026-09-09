using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(SharedAshWalkerHearthSystem))]
public sealed partial class AshWalkerHearthComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<StackPrototype> Fuel = "WoodPlank";

    [DataField, AutoNetworkedField]
    public float FuelPerItem = 120f;

    [DataField, AutoNetworkedField]
    public float MaxFuel = 600f;

    [DataField, AutoNetworkedField]
    public float FuelRemaining;

    [DataField]
    public float HeatingPower = 2400f;

    [DataField]
    public float MaxTemperature = 450f;

    [ViewVariables]
    public bool Burning;
}
