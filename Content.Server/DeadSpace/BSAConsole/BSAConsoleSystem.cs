// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.UserInterface;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.BSAConsole;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.BSAConsole;

public sealed class BSAConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromMilliseconds(250);
    private const float RadarMaxRange = 512f;
    private const int MaxRadarGrids = 128;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BSAConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BSAConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<BSAConsoleComponent, AfterActivatableUIOpenEvent>(OnUiOpen);
        SubscribeLocalEvent<BSAConsoleComponent, BSAConsoleFireMessage>(OnFire);
        SubscribeLocalEvent<BSAConsoleComponent, BSAConsoleSwitchViewMessage>(OnSwitchView);
        SubscribeLocalEvent<BSAConsoleComponent, BSAConsoleSelectGridMessage>(OnSelectGrid);
        SubscribeLocalEvent<BSAConsoleComponent, BSAConsoleEjectDiskMessage>(OnEjectDisk);
        SubscribeLocalEvent<BSAConsoleComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<BSAConsoleComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<BSAConsoleComponent, DeviceListUpdateEvent>(OnDeviceListUpdated);
    }

    private void OnInit(EntityUid uid, BSAConsoleComponent comp, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, BSAConsoleComponent.DiskSlotId, comp.DiskSlot);
    }

    private void OnRemove(EntityUid uid, BSAConsoleComponent comp, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, comp.DiskSlot);
    }

    private void OnUiOpen(EntityUid uid, BSAConsoleComponent comp, AfterActivatableUIOpenEvent args)
    {
        UpdateUiState(uid, comp);
    }

    private void OnFire(EntityUid uid, BSAConsoleComponent comp, BSAConsoleFireMessage msg)
    {
        if (comp.LinkedBSA is not { } bsaUid ||
            !TryComp<BluespaceArtilleryComponent>(bsaUid, out var bsa) ||
            !bsa.IsReady ||
            bsa.HasPendingShot)
        {
            return;
        }

        var now = (float) _timing.CurTime.TotalSeconds;
        if (now < bsa.CooldownEnd || !float.IsFinite(msg.X) || !float.IsFinite(msg.Y))
            return;

        var target = new Vector2(msg.X, msg.Y);
        if (!TryResolveShotTarget(uid, comp, target, out var mapId, out var targetGrid, out var gridLocalPosition))
            return;

        bsa.IsReady = false;
        bsa.HasPendingShot = true;
        bsa.PendingShotEnd = now + bsa.PendingShotDelay;
        bsa.PendingShotMapId = (int) mapId;
        bsa.PendingShotWorldPosition = target;
        bsa.PendingShotGridUid = targetGrid;
        bsa.PendingShotGridLocalPosition = gridLocalPosition;
        Dirty(bsaUid, bsa);

        UpdateUiState(uid, comp);
    }

    private void OnSwitchView(EntityUid uid, BSAConsoleComponent comp, BSAConsoleSwitchViewMessage msg)
    {
        if (msg.ViewMode == BSAConsoleViewMode.Grid)
            return;

        if (msg.ViewMode == BSAConsoleViewMode.MassScannerDisk && !TryGetDiskMapId(comp, out _))
            return;

        if (msg.ViewMode != BSAConsoleViewMode.MassScannerLocal &&
            msg.ViewMode != BSAConsoleViewMode.MassScannerDisk)
        {
            return;
        }

        comp.CurrentViewMode = msg.ViewMode;
        comp.SelectedGridName = null;
        comp.SelectedGridUid = null;
        UpdateUiState(uid, comp);
    }

    private void OnSelectGrid(EntityUid uid, BSAConsoleComponent comp, BSAConsoleSelectGridMessage msg)
    {
        if (!TryGetEntity(msg.GridUid, out var gridUid) ||
            !HasComp<MapGridComponent>(gridUid.Value) ||
            !IsAllowedGrid(uid, comp, gridUid.Value))
        {
            return;
        }

        comp.SelectedGridUid = gridUid.Value;
        comp.SelectedGridName = MetaData(gridUid.Value).EntityName;
        comp.CurrentViewMode = BSAConsoleViewMode.Grid;
        UpdateUiState(uid, comp);
    }

    private void OnEjectDisk(EntityUid uid, BSAConsoleComponent comp, BSAConsoleEjectDiskMessage msg)
    {
        _itemSlots.TryEject(uid, comp.DiskSlot, msg.Actor, out _);
    }

    private void OnContainerInserted(EntityUid uid, BSAConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.DiskSlot.ID)
            return;

        comp.HasDisk = true;
        comp.TargetMapUid = null;
        comp.TargetMapName = null;
        comp.SelectedGridName = null;
        comp.SelectedGridUid = null;
        comp.CurrentViewMode = BSAConsoleViewMode.MassScannerLocal;

        if (TryComp<ShuttleDestinationCoordinatesComponent>(args.Entity, out var diskCoords) &&
            diskCoords.Destination is { } destination &&
            ResolveTargetMapId(destination) is { } mapId &&
            _mapManager.MapExists(mapId))
        {
            comp.TargetMapUid = destination;
            comp.TargetMapName = MetaData(destination).EntityName;
        }

        UpdateUiState(uid, comp);
    }

    private void OnContainerRemoved(EntityUid uid, BSAConsoleComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.DiskSlot.ID)
            return;

        ClearDiskData(uid, comp);
    }

    private void ClearDiskData(EntityUid uid, BSAConsoleComponent comp)
    {
        comp.TargetMapUid = null;
        comp.TargetMapName = null;
        comp.HasDisk = false;
        comp.SelectedGridName = null;
        comp.SelectedGridUid = null;
        comp.CurrentViewMode = BSAConsoleViewMode.MassScannerLocal;
        UpdateUiState(uid, comp);
    }

    private void OnDeviceListUpdated(EntityUid uid, BSAConsoleComponent comp, DeviceListUpdateEvent args)
    {
        TryFindBSA(uid, comp);
    }

    private void TryFindBSA(EntityUid uid, BSAConsoleComponent comp)
    {
        comp.LinkedBSA = null;

        if (TryComp<DeviceListComponent>(uid, out var deviceList))
        {
            foreach (var device in _deviceList.GetAllDevices(uid, deviceList))
            {
                if (!HasComp<BluespaceArtilleryComponent>(device))
                    continue;

                comp.LinkedBSA = device;
                break;
            }
        }

        UpdateUiState(uid, comp);
    }

    public override void Update(float frameTime)
    {
        var now = (float) _timing.CurTime.TotalSeconds;
        var artilleryQuery = EntityQueryEnumerator<BluespaceArtilleryComponent>();
        while (artilleryQuery.MoveNext(out var artilleryUid, out var artillery))
        {
            if (artillery.HasPendingShot)
            {
                if (now >= artillery.PendingShotEnd)
                    FirePendingShot(artilleryUid, artillery, now);

                continue;
            }

            if (!artillery.IsReady && now >= artillery.CooldownEnd)
            {
                artillery.IsReady = true;
                Dirty(artilleryUid, artillery);
            }
        }

        var consoleQuery = EntityQueryEnumerator<BSAConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var console))
        {
            if (!_ui.IsUiOpen(consoleUid, BSAConsoleUiKey.Key) || _timing.CurTime < console.NextUiUpdate)
                continue;

            UpdateUiState(consoleUid, console);
        }
    }

    private void FirePendingShot(EntityUid artilleryUid, BluespaceArtilleryComponent artillery, float now)
    {
        var mapId = new MapId(artillery.PendingShotMapId);
        var position = artillery.PendingShotWorldPosition;
        var validTarget = mapId != MapId.Nullspace && _mapManager.MapExists(mapId);

        if (validTarget && artillery.PendingShotGridUid is { } gridUid)
        {
            if (!TryComp(gridUid, out TransformComponent? gridXform) || gridXform.MapID != mapId)
            {
                validTarget = false;
            }
            else
            {
                position = Vector2.Transform(
                    artillery.PendingShotGridLocalPosition,
                    _transform.GetWorldMatrix(gridUid));

                var mapPosition = new MapCoordinates(position, mapId);
                validTarget = _mapManager.TryFindGridAt(mapPosition, out var foundGrid, out _) && foundGrid == gridUid;
            }
        }

        if (validTarget)
        {
            _explosion.QueueExplosion(
                new MapCoordinates(position, mapId),
                "Radioactive",
                2000f,
                5f,
                100f,
                artilleryUid);
        }

        artillery.HasPendingShot = false;
        artillery.PendingShotEnd = 0f;
        artillery.PendingShotMapId = -1;
        artillery.PendingShotGridUid = null;
        artillery.PendingShotGridLocalPosition = Vector2.Zero;
        artillery.PendingShotWorldPosition = Vector2.Zero;
        artillery.CooldownEnd = now + artillery.CooldownDuration;
        artillery.IsReady = false;
        Dirty(artilleryUid, artillery);
    }

    private bool TryResolveShotTarget(
        EntityUid consoleUid,
        BSAConsoleComponent console,
        Vector2 position,
        out MapId mapId,
        out EntityUid? targetGrid,
        out Vector2 gridLocalPosition)
    {
        targetGrid = null;
        gridLocalPosition = Vector2.Zero;

        if (!TryResolveRadarView(consoleUid, console, out mapId, out var center, out var selectedGrid))
            return false;

        if (console.CurrentViewMode != BSAConsoleViewMode.Grid)
            return Vector2.DistanceSquared(position, center) <= RadarMaxRange * RadarMaxRange;

        if (selectedGrid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        gridLocalPosition = Vector2.Transform(position, _transform.GetInvWorldMatrix(gridUid));
        if (!float.IsFinite(gridLocalPosition.X) ||
            !float.IsFinite(gridLocalPosition.Y) ||
            !grid.LocalAABB.Contains(gridLocalPosition) ||
            !_mapManager.TryFindGridAt(new MapCoordinates(position, mapId), out var foundGrid, out _) ||
            foundGrid != gridUid)
        {
            return false;
        }

        targetGrid = gridUid;
        return true;
    }

    private void UpdateUiState(EntityUid uid, BSAConsoleComponent comp)
    {
        if (!_ui.IsUiOpen(uid, BSAConsoleUiKey.Key))
            return;

        comp.NextUiUpdate = _timing.CurTime + UiUpdateInterval;

        var hasValidDiskTarget = TryGetDiskMapId(comp, out _);
        if (comp.HasDisk && !hasValidDiskTarget)
        {
            comp.TargetMapUid = null;
            comp.TargetMapName = null;
        }

        if (comp.CurrentViewMode == BSAConsoleViewMode.MassScannerDisk && !hasValidDiskTarget)
            comp.CurrentViewMode = BSAConsoleViewMode.MassScannerLocal;

        if (comp.SelectedGridUid is { } selectedUid && !IsAllowedGrid(uid, comp, selectedUid))
        {
            comp.SelectedGridUid = null;
            comp.SelectedGridName = null;
        }

        if (comp.CurrentViewMode == BSAConsoleViewMode.Grid && comp.SelectedGridUid == null)
            comp.CurrentViewMode = BSAConsoleViewMode.MassScannerLocal;

        var now = (float) _timing.CurTime.TotalSeconds;
        var bsaUid = comp.LinkedBSA;
        BluespaceArtilleryComponent? bsa = null;
        var isConnected = bsaUid is { } linkedBsa &&
            TryComp(linkedBsa, out bsa);

        string? bsaName = null;
        var isOnCooldown = false;
        var cooldownRemaining = 0f;
        var cooldownDuration = 0f;
        var hasPendingShot = false;
        var pendingShotTimeLeft = 0f;
        var pendingShotDelay = 0f;

        if (isConnected && bsaUid is { } connectedBsa && bsa != null)
        {
            bsaName = MetaData(connectedBsa).EntityName;
            cooldownDuration = bsa.CooldownDuration;
            hasPendingShot = bsa.HasPendingShot;
            pendingShotDelay = bsa.PendingShotDelay;
            pendingShotTimeLeft = hasPendingShot ? MathF.Max(0f, bsa.PendingShotEnd - now) : 0f;
            isOnCooldown = !hasPendingShot && !bsa.IsReady && bsa.CooldownEnd > now;
            cooldownRemaining = isOnCooldown ? bsa.CooldownEnd - now : 0f;
        }

        var state = new BSAConsoleUiState(
            isConnected,
            bsaName,
            isOnCooldown,
            cooldownRemaining,
            cooldownDuration,
            comp.CurrentViewMode,
            comp.HasDisk,
            comp.TargetMapName,
            BuildUnifiedGridList(uid, comp),
            comp.SelectedGridName,
            comp.SelectedGridUid is { } selectedGrid ? GetNetEntity(selectedGrid) : null,
            comp.SelectedGridUid is { } navMapGrid && HasComp<NavMapComponent>(navMapGrid),
            hasPendingShot,
            pendingShotTimeLeft,
            pendingShotDelay,
            BuildRadarState(uid, comp));

        _ui.SetUiState(uid, BSAConsoleUiKey.Key, state);
    }

    private BSARadarState? BuildRadarState(EntityUid uid, BSAConsoleComponent comp)
    {
        if (!TryResolveRadarView(uid, comp, out var mapId, out var center, out var selectedGrid))
            return null;

        var grids = new List<BSARadarGridState>();
        foreach (var grid in _mapManager.GetAllGrids(mapId))
        {
            if (TerminatingOrDeleted(grid.Owner))
                continue;

            var localBounds = grid.Comp.LocalAABB;
            var worldCenter = Vector2.Transform(localBounds.Center, _transform.GetWorldMatrix(grid.Owner));
            var radius = localBounds.Size.Length() * 0.5f;
            var selected = selectedGrid == grid.Owner;

            if (!selected && Vector2.Distance(worldCenter, center) > RadarMaxRange + radius)
                continue;

            grids.Add(new BSARadarGridState(
                GetNetEntity(grid.Owner),
                worldCenter,
                localBounds.Size * 0.5f,
                (float) _transform.GetWorldRotation(grid.Owner).Theta,
                selected));
        }

        grids.Sort((left, right) =>
            Vector2.DistanceSquared(left.Center, center).CompareTo(Vector2.DistanceSquared(right.Center, center)));

        if (grids.Count > MaxRadarGrids)
            grids.RemoveRange(MaxRadarGrids, grids.Count - MaxRadarGrids);

        return new BSARadarState((int) mapId, center, grids);
    }

    private List<BSAGridEntry> BuildUnifiedGridList(EntityUid uid, BSAConsoleComponent comp)
    {
        var result = new List<BSAGridEntry>();
        var added = new HashSet<EntityUid>();
        var localMapId = Transform(uid).MapID;

        CollectGridEntries(localMapId, false, result, added);

        if (TryGetDiskMapId(comp, out var diskMapId) && diskMapId != localMapId)
            CollectGridEntries(diskMapId, true, result, added);

        result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private void CollectGridEntries(
        MapId mapId,
        bool isDisk,
        List<BSAGridEntry> result,
        HashSet<EntityUid> added)
    {
        if (mapId == MapId.Nullspace || !_mapManager.MapExists(mapId))
            return;

        foreach (var grid in _mapManager.GetAllGrids(mapId))
        {
            if (TerminatingOrDeleted(grid.Owner) || !added.Add(grid.Owner))
                continue;

            var name = MetaData(grid.Owner).EntityName;
            if (!string.IsNullOrWhiteSpace(name))
                result.Add(new BSAGridEntry(GetNetEntity(grid.Owner), name, isDisk));
        }
    }

    private bool TryResolveRadarView(
        EntityUid uid,
        BSAConsoleComponent comp,
        out MapId mapId,
        out Vector2 center,
        out EntityUid? selectedGrid)
    {
        selectedGrid = null;
        center = Vector2.Zero;

        switch (comp.CurrentViewMode)
        {
            case BSAConsoleViewMode.MassScannerLocal:
                mapId = Transform(uid).MapID;
                center = _transform.GetWorldPosition(uid);
                break;

            case BSAConsoleViewMode.MassScannerDisk:
                if (!TryGetDiskMapId(comp, out mapId))
                    return false;

                center = GetMapCenter(mapId, comp.TargetMapUid);
                break;

            case BSAConsoleViewMode.Grid:
                if (comp.SelectedGridUid is not { } gridUid ||
                    !TryComp<MapGridComponent>(gridUid, out var grid) ||
                    !IsAllowedGrid(uid, comp, gridUid))
                {
                    mapId = MapId.Nullspace;
                    return false;
                }

                mapId = Transform(gridUid).MapID;
                center = Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(gridUid));
                selectedGrid = gridUid;
                break;

            default:
                mapId = MapId.Nullspace;
                return false;
        }

        return mapId != MapId.Nullspace && _mapManager.MapExists(mapId);
    }

    private Vector2 GetMapCenter(MapId mapId, EntityUid? fallback)
    {
        Entity<MapGridComponent>? largest = null;
        var largestArea = -1f;

        foreach (var grid in _mapManager.GetAllGrids(mapId))
        {
            if (TerminatingOrDeleted(grid.Owner))
                continue;

            var area = grid.Comp.LocalAABB.Width * grid.Comp.LocalAABB.Height;
            if (area <= largestArea)
                continue;

            largest = grid;
            largestArea = area;
        }

        if (largest is { } largestGrid)
        {
            return Vector2.Transform(
                largestGrid.Comp.LocalAABB.Center,
                _transform.GetWorldMatrix(largestGrid.Owner));
        }

        if (fallback is { } fallbackUid && TryComp(fallbackUid, out TransformComponent? _))
            return _transform.GetWorldPosition(fallbackUid);

        return Vector2.Zero;
    }

    private bool IsAllowedGrid(EntityUid consoleUid, BSAConsoleComponent comp, EntityUid gridUid)
    {
        if (TerminatingOrDeleted(gridUid) || !HasComp<MapGridComponent>(gridUid))
            return false;

        var gridMapId = Transform(gridUid).MapID;
        if (gridMapId == Transform(consoleUid).MapID)
            return true;

        return TryGetDiskMapId(comp, out var diskMapId) && gridMapId == diskMapId;
    }

    private bool TryGetDiskMapId(BSAConsoleComponent comp, out MapId mapId)
    {
        mapId = MapId.Nullspace;
        if (!comp.HasDisk || comp.TargetMapUid is not { } targetMapUid)
            return false;

        if (ResolveTargetMapId(targetMapUid) is not { } resolved || !_mapManager.MapExists(resolved))
            return false;

        mapId = resolved;
        return true;
    }

    private MapId? ResolveTargetMapId(EntityUid mapUid)
    {
        if (TerminatingOrDeleted(mapUid))
            return null;

        if (TryComp<MapComponent>(mapUid, out var mapComp))
            return mapComp.MapId;

        return TryComp(mapUid, out TransformComponent? xform) && xform.MapID != MapId.Nullspace
            ? xform.MapID
            : null;
    }
}
