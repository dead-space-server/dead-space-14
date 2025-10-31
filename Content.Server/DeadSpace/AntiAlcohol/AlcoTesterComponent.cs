using Robust.Shared.GameStates;

namespace Content.Server.DeadSpace.AntiAlcohol;

[RegisterComponent]
public sealed partial class AlcoTesterComponent : Component
{
    [DataField] public string EthanolId = "Ethanol";
    [DataField] public float ScanDelaySec = 2.0f;
}
