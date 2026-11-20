using Content.Shared.Atmos;
using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.BluespaceMiner;

[RegisterComponent, NetworkedComponent]
public sealed partial class BluespaceMinerComponent : Component
{
    [DataField]
    public float MinTemperature = 20f;

    [DataField]
    public float MaxTemperature = 80f;

    [DataField]
    public float MinPressure = 100f;

    [DataField]
    public float MaxPressure = 150f;

    [DataField]
    public float TeleportRange = 1.5f;

    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, float> MaterialWeights = new()
    {
        { "Glass", 24f },
        { "Steel", 24f },
        { "Silver", 15f },
        { "Gold", 8f },
    };

    [DataField]
    public Gas ReleasedGas = Gas.CarbonDioxide;

    [DataField]
    public float GasMolesPerSecond = 50f;

    [DataField]
    public float GasTemperature = 300f;

    [DataField]
    public float CycleInterval = 1f;

    [ViewVariables]
    public float Accumulator;

    [DataField]
    public int SheetsPerBatch = 1;
}