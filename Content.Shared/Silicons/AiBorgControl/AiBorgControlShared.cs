using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Whitelist;

namespace Content.Shared.Silicons.AiBorgControl;

[RegisterComponent, NetworkedComponent]
public sealed partial class BorgRemoteModuleComponent : Component
{
    [DataField]
    public EntityWhitelist? ChassisWhitelist;

    [DataField]
    public EntityWhitelist? ChassisBlacklist;
}

[Serializable, NetSerializable]
public enum AiBorgControlUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum AiBorgStatus : byte
{
    Ready,
    Controlled,
    NoPower,
    Dead,
    Emagged
}

[Serializable, NetSerializable]
public readonly record struct AiBorgControlEntry(
    NetEntity Borg,
    string Name,
    string ChassisName,
    float HpPercent,
    float Charge,
    int ModuleCount,
    AiBorgStatus Status);

[Serializable, NetSerializable]
public sealed class AiBorgControlState(List<AiBorgControlEntry> borgs) : BoundUserInterfaceState
{
    public readonly List<AiBorgControlEntry> Borgs = borgs;
}

[Serializable, NetSerializable]
public sealed class AiBorgControlTakeMessage(NetEntity borg) : BoundUserInterfaceMessage
{
    public readonly NetEntity Borg = borg;
}

public sealed partial class AiBorgReturnToCoreEvent : InstantActionEvent;
