using System.Collections.Generic;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Components;
using Content.Shared.DeadSpace.Administration;
using Content.Shared.Ghost;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.AntagMaps;

public sealed partial class DeleteAbandonedAntagMapSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<MapId> _occupied = new();
    private readonly List<MapId> _delete = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeleteAbandonedAntagMapComponent, RuleLoadedGridsEvent>(OnLoaded);
    }

    private void OnLoaded(Entity<DeleteAbandonedAntagMapComponent> ent, ref RuleLoadedGridsEvent args)
    {
        ent.Comp.Map = args.Map;
        ent.Comp.SeenPlayer = false;
        ent.Comp.EmptySince = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        _occupied.Clear();
        var actors = AllEntityQuery<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var xform))
        {
            if (HasComp<GhostComponent>(uid) || HasComp<AdminGhostVisibilityComponent>(uid))
                continue;

            _occupied.Add(xform.MapID);
        }

        _delete.Clear();
        var rules = EntityQueryEnumerator<DeleteAbandonedAntagMapComponent>();
        while (rules.MoveNext(out _, out var comp))
        {
            if (comp.Map is not { } mapId || mapId == MapId.Nullspace)
                continue;

            if (!_map.TryGetMap(mapId, out var mapUid) || mapUid is not { } map || Terminating(map))
            {
                comp.Map = null;
                comp.EmptySince = null;
                continue;
            }

            if (!TryComp<MapComponent>(map, out var mapComp) || !mapComp.MapInitialized)
                continue;

            if (IsEvacStationMap(map))
            {
                comp.Map = null;
                comp.EmptySince = null;
                continue;
            }

            if (_occupied.Contains(mapId))
            {
                comp.SeenPlayer = true;
                comp.EmptySince = null;
                continue;
            }

            if (!comp.SeenPlayer)
                continue;

            comp.EmptySince ??= _timing.CurTime;
            if (_timing.CurTime - comp.EmptySince.Value < comp.Delay)
                continue;

            comp.Map = null;
            comp.EmptySince = null;
            _delete.Add(mapId);
        }

        foreach (var mapId in _delete)
            _map.QueueDeleteMap(mapId);
    }

    private bool IsEvacStationMap(EntityUid mapUid)
    {
        var children = Transform(mapUid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp<StationMemberComponent>(child, out var member))
                continue;

            if (HasComp<StationEmergencyShuttleComponent>(member.Station))
                return true;
        }

        return false;
    }
}
