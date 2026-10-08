using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja;

[NetSerializable, Serializable]
public enum NinjaInfoScannerUiKey : byte
{
    Key
}

[NetSerializable, Serializable]
public sealed class NinjaInfoScannerState : BoundUserInterfaceState
{
    public NetEntity? ContainedEntity { get; }

    public NinjaInfoScannerState(NetEntity? containedEntity)
    {
        ContainedEntity = containedEntity;
    }
}

[NetSerializable, Serializable]
public sealed class NinjaInfoScannerScanMessage : BoundUserInterfaceMessage { }

[NetSerializable, Serializable]
public sealed class NinjaInfoScannerEjectMessage : BoundUserInterfaceMessage { }

[NetSerializable, Serializable]
public sealed class NinjaInfoScannerTeleportMessage : BoundUserInterfaceMessage { }