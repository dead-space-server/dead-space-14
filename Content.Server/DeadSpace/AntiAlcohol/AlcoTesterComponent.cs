using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.DeadSpace.AntiAlcohol;

[RegisterComponent]
public sealed partial class AlcoTesterComponent : Component
{
    [DataField] public ProtoId<ReagentPrototype> EthanolId = "Ethanol";
    [DataField] public float ScanDelaySec = 2.0f;
}
