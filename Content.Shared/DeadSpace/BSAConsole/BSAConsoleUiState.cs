// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.BSAConsole;

[Serializable, NetSerializable]
public sealed class BSAConsoleUiState(
    bool isConnected,
    string? bsaName,
    bool isOnCooldown,
    float cooldownRemaining,
    float cooldownDuration,
    BSAConsoleViewMode currentViewMode,
    bool hasDisk,
    string? targetMapName,
    List<BSAGridEntry> allGrids,
    string? selectedGridName,
    NetEntity? selectedGridUid,
    bool selectedGridHasNavMap,
    bool hasPendingShot,
    float pendingShotTimeLeft,
    float pendingShotDelay,
    BSARadarState? radarState) : BoundUserInterfaceState
{
    public bool IsConnected = isConnected;
    public string? BSAName = bsaName;
    public bool IsOnCooldown = isOnCooldown;
    public float CooldownRemaining = cooldownRemaining;
    public float CooldownDuration = cooldownDuration;
    public BSAConsoleViewMode CurrentViewMode = currentViewMode;

    public bool HasDisk = hasDisk;
    public string? TargetMapName = targetMapName;

    public List<BSAGridEntry> AllGrids = allGrids;
    public string? SelectedGridName = selectedGridName;
    public NetEntity? SelectedGridUid = selectedGridUid;
    public bool SelectedGridHasNavMap = selectedGridHasNavMap;

    public bool HasPendingShot = hasPendingShot;
    public float PendingShotTimeLeft = pendingShotTimeLeft;
    public float PendingShotDelay = pendingShotDelay;

    public BSARadarState? RadarState = radarState;
}

[Serializable, NetSerializable]
public sealed class BSAGridEntry(NetEntity gridUid, string name, bool isDisk)
{
    public NetEntity GridUid { get; } = gridUid;
    public string Name { get; } = name;
    public bool IsDisk { get; } = isDisk;
}

[Serializable, NetSerializable]
public sealed class BSARadarState(int mapId, Vector2 center, List<BSARadarGridState> grids)
{
    public int MapId { get; } = mapId;
    public Vector2 Center { get; } = center;
    public List<BSARadarGridState> Grids { get; } = grids;
}

[Serializable, NetSerializable]
public sealed class BSARadarGridState(
    NetEntity gridUid,
    Vector2 center,
    Vector2 halfExtents,
    float rotation,
    bool selected)
{
    public NetEntity GridUid { get; } = gridUid;
    public Vector2 Center { get; } = center;
    public Vector2 HalfExtents { get; } = halfExtents;
    public float Rotation { get; } = rotation;
    public bool Selected { get; } = selected;
}
