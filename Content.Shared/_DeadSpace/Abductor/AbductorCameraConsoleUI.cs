using Robust.Shared.Serialization;
using static Content.Shared.Pinpointer.SharedNavMapSystem;

namespace Content.Shared.DeadSpace.Abductor;

[Serializable, NetSerializable]
public sealed class AbductorCameraConsoleBuiState : BoundUserInterfaceState
{
    public Dictionary<int, StationBeacons> Stations { get; set; } = new();
}

[Serializable, NetSerializable]
public sealed class AbductorConsoleBuiState : BoundUserInterfaceState
{
    public NetEntity? Target { get; set; }
    public string? TargetName { get; set; }
    public string? VictimName { get; set; }
    public bool AlienPadFound { get; set; }
    public bool ExperimentatorFound { get; set; }
    public bool ArmorFound { get; set; }
    public bool ArmorLocked { get; set; }
    public AbductorArmorModeType CurrentArmorMode { get; set; }
}

[Serializable, NetSerializable]
public sealed class StationBeacons
{
    public int StationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<NavMapBeacon> Beacons { get; set; } = new();
}

[Serializable, NetSerializable]
public sealed class AbductorBeaconChosenBuiMsg : BoundUserInterfaceMessage
{
    public NavMapBeacon Beacon { get; set; }
}

[Serializable, NetSerializable]
public sealed class AbductorAttractBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AbductorCompleteExperimentBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AbductorVestModeChangeBuiMsg : BoundUserInterfaceMessage
{
    public AbductorArmorModeType Mode { get; set; }
}

[Serializable, NetSerializable]
public sealed class AbductorLockBuiMsg : BoundUserInterfaceMessage
{
}