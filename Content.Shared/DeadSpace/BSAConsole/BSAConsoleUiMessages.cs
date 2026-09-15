// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.BSAConsole;

[Serializable, NetSerializable]
public enum BSAConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum BSAConsoleViewMode : byte
{
    MassScannerLocal,
    MassScannerDisk,
    Grid,
}

[Serializable, NetSerializable]
public sealed class BSAConsoleFireMessage(float x, float y) : BoundUserInterfaceMessage
{
    public float X { get; } = x;
    public float Y { get; } = y;
}

[Serializable, NetSerializable]
public sealed class BSAConsoleSwitchViewMessage(BSAConsoleViewMode viewMode) : BoundUserInterfaceMessage
{
    public BSAConsoleViewMode ViewMode { get; } = viewMode;
}

[Serializable, NetSerializable]
public sealed class BSAConsoleSelectGridMessage(NetEntity gridUid) : BoundUserInterfaceMessage
{
    public NetEntity GridUid { get; } = gridUid;
}

[Serializable, NetSerializable]
public sealed class BSAConsoleEjectDiskMessage : BoundUserInterfaceMessage;
