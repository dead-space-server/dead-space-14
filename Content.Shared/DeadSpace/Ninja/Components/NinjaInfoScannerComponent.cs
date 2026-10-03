using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaInfoScannerComponent : Component
{
    [DataField]
    public string ContainerId = "entity_storage";

    [DataField, AutoNetworkedField]
    public bool IsScanning;

    [DataField, AutoNetworkedField]
    public EntityUid? ScanningEntity;

    [DataField, AutoNetworkedField]
    public float ScanTime = 10f;

    [DataField]
    public TimeSpan? ScanEndTime;

    [DataField]
    public EntityUid? ScanActor;

    [DataField]
    public string ScanReagent = "Nocturine";

    [DataField]
    public float ScanReagentAmount = 10f;

    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "NinjaScannerPort";
}

[Serializable, NetSerializable]
public enum NinjaInfoScannerVisualState : byte
{
    Open,
    Closed,
    Scan
}

[Serializable, NetSerializable]
public enum NinjaInfoScannerVisuals : byte
{
    VisualState
}