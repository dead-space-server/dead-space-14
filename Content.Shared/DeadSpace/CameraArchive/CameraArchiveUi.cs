// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.CameraArchives;

[Serializable, NetSerializable]
public sealed class CameraArchiveEntry
{
    public NetEntity Camera;
    public string Name = string.Empty;
    public int Count;

    public bool Offline;

    public bool Covered;

    public bool Burned;
}

[Serializable, NetSerializable]
public sealed class CameraArchiveState : BoundUserInterfaceState
{
    public CameraArchiveEntry[] Cameras = [];
    public NetEntity Selected;
    public int FrameIndex;
    public int FrameCount;
    public TimeSpan FrameTime;
    public CameraArchive? Frame;
    public int Request;

    public bool Covered;

    public bool ServerDown;
}

[Serializable, NetSerializable]
public sealed class CameraArchivePrintMessage : BoundUserInterfaceMessage
{
    public const int MaxImageBytes = 1500000;

    public NetEntity Camera;
    public int FrameIndex;
    public byte[] Image = [];
}
