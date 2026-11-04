// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Surgery;

[Serializable, NetSerializable]
public enum SurgeryXrayTableUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SurgeryXrayTableState : BoundUserInterfaceState
{
    public NetEntity Patient;
    public string Name = "";
    public string Report = "";
}

[Serializable, NetSerializable]
public sealed class SurgeryXrayTablePrintMessage : BoundUserInterfaceMessage
{
    public byte Zone;
    public byte[] Image = Array.Empty<byte>();
}

[Serializable, NetSerializable]
public sealed class SurgeryXrayTableStartMessage : BoundUserInterfaceMessage
{
    public string Operation = "";
}
