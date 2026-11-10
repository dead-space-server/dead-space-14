// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration.Managers;
using Content.Server.Atmos.Monitor.Components;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.DeviceLinkVis;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.DeviceLinkVis;

public sealed class DeviceLinkVisSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLinks = default!;
    [Dependency] private readonly SharedDeviceListSystem _deviceLists = default!;

    private readonly HashSet<ICommonSession> _watchers = new();
    private readonly List<ICommonSession> _remove = new();
    private readonly List<EntityUid> _sinks = new();
    private readonly List<EntityUid> _devices = new();
    private readonly Dictionary<MapId, List<DeviceLinkVisLink>> _cache = new();

    private float _accum;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DeviceLinkVisEnableEvent>(OnEnable);
        _player.PlayerStatusChanged += OnPlayerStatus;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatus;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_watchers.Count == 0)
            return;

        _accum += frameTime;
        if (_accum < 0.5f)
            return;

        _accum = 0f;
        SendAll();
    }

    private void OnEnable(DeviceLinkVisEnableEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!ev.Enabled || !CanSee(session))
        {
            _watchers.Remove(session);
            return;
        }

        _watchers.Add(session);
        Send(session);
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs ev)
    {
        if (ev.NewStatus == SessionStatus.Disconnected)
            _watchers.Remove(ev.Session);
    }

    private void Send(ICommonSession session)
    {
        var map = MapOf(session);
        RaiseNetworkEvent(new DeviceLinkVisStateEvent { Links = Build(map) }, session);
    }

    private void SendAll()
    {
        _cache.Clear();
        _remove.Clear();

        foreach (var session in _watchers)
        {
            if (!CanSee(session))
            {
                _remove.Add(session);
                continue;
            }

            var map = MapOf(session);
            if (!_cache.TryGetValue(map, out var links))
            {
                links = Build(map);
                _cache.Add(map, links);
            }

            RaiseNetworkEvent(new DeviceLinkVisStateEvent { Links = links }, session);
        }

        foreach (var session in _remove)
            _watchers.Remove(session);
    }

    private List<DeviceLinkVisLink> Build(MapId map)
    {
        var links = new List<DeviceLinkVisLink>();
        if (map == MapId.Nullspace)
            return links;

        var seen = new HashSet<(NetEntity Source, NetEntity Sink)>();
        var sources = EntityQueryEnumerator<DeviceLinkSourceComponent, TransformComponent>();
        while (sources.MoveNext(out var uid, out var source, out var xform))
        {
            if (!DeviceLinkVisMap.IsOnMap(EntityManager, uid, xform, map))
                continue;

            _deviceLinks.CopyLinkedSinks(source, _sinks);
            if (_sinks.Count == 0)
                continue;

            var sourceNet = GetNetEntity(uid);
            var sourcePos = GetNetCoordinates(xform.Coordinates);
            foreach (var sink in _sinks)
            {
                if (!TryComp(sink, out TransformComponent? sinkXform) || !DeviceLinkVisMap.IsOnMap(EntityManager, sink, sinkXform, map))
                    continue;

                var sinkNet = GetNetEntity(sink);
                if (!seen.Add((sourceNet, sinkNet)))
                    continue;

                links.Add(new DeviceLinkVisLink
                {
                    Source = sourceNet,
                    Sink = sinkNet,
                    SourcePos = sourcePos,
                    SinkPos = GetNetCoordinates(sinkXform.Coordinates),
                });
            }
        }

        AddPanels<AirAlarmComponent>(map, links, seen);
        AddPanels<FireAlarmComponent>(map, links, seen);
        return links;
    }

    private void AddPanels<TComp>(MapId map, List<DeviceLinkVisLink> links, HashSet<(NetEntity Source, NetEntity Sink)> seen)
        where TComp : IComponent
    {
        var query = EntityQueryEnumerator<TComp, DeviceListComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var list, out var xform))
        {
            if (!DeviceLinkVisMap.IsOnMap(EntityManager, uid, xform, map))
                continue;

            _deviceLists.CopyAllowDevices(list, _devices);
            if (_devices.Count == 0)
                continue;

            var sourceNet = GetNetEntity(uid);
            var sourcePos = GetNetCoordinates(xform.Coordinates);
            foreach (var device in _devices)
            {
                if (device == uid || !TryComp(device, out TransformComponent? deviceXform) || !DeviceLinkVisMap.IsOnMap(EntityManager, device, deviceXform, map))
                    continue;

                var sinkNet = GetNetEntity(device);
                if (!seen.Add((sourceNet, sinkNet)))
                    continue;

                links.Add(new DeviceLinkVisLink
                {
                    Source = sourceNet,
                    Sink = sinkNet,
                    SourcePos = sourcePos,
                    SinkPos = GetNetCoordinates(deviceXform.Coordinates),
                });
            }
        }
    }

    private MapId MapOf(ICommonSession session)
    {
        if (session.AttachedEntity is not { } uid || !TryComp(uid, out TransformComponent? xform))
            return MapId.Nullspace;

        return DeviceLinkVisMap.Resolve(EntityManager, uid, xform);
    }

    private bool CanSee(ICommonSession session)
    {
        return _admin.HasAdminFlag(session, AdminFlags.Admin)
            || _admin.HasAdminFlag(session, AdminFlags.Mapping)
            || _admin.HasAdminFlag(session, AdminFlags.Debug);
    }
}
