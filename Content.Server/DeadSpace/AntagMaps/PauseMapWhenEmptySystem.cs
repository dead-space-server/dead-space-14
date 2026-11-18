using System.Collections.Generic;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Components;
using Content.Shared.DeadSpace.Administration;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.Ghost;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.AntagMaps;

public sealed partial class PauseMapWhenEmptySystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;

    private readonly HashSet<MapId> _occupied = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PauseMapWhenEmptyComponent, RuleLoadedGridsEvent>(OnLoaded);
    }

    private void OnLoaded(Entity<PauseMapWhenEmptyComponent> ent, ref RuleLoadedGridsEvent args)
    {
        ent.Comp.Map = args.Map;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _occupied.Clear();
        var actors = AllEntityQuery<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var xform))
        {
            if (HasComp<GhostComponent>(uid) || HasComp<AdminGhostVisibilityComponent>(uid))
                continue;

            _occupied.Add(xform.MapID);
        }

        var rules = EntityQueryEnumerator<PauseMapWhenEmptyComponent>();
        while (rules.MoveNext(out _, out var comp))
        {
            if (comp.Map is not { } mapId || mapId == MapId.Nullspace)
                continue;

            if (!_map.TryGetMap(mapId, out var mapUid) || mapUid is not { } map || Terminating(map))
                continue;

            if (!TryComp<MapComponent>(map, out var mapComp) || !mapComp.MapInitialized)
                continue;

            if (TryComp<PreparedRoundMapComponent>(map, out var prepared) && !prepared.Warmed)
                continue;

            if (IsEvacStationMap(map))
            {
                comp.Map = null;
                continue;
            }

            var pause = !_occupied.Contains(mapId);
            if (mapComp.MapPaused == pause)
                continue;

            _map.SetPaused(map, pause);
        }

        PauseWhenEmpty<LavalandMapComponent>();
        PauseWhenEmpty<LavalandChromiteCavesComponent>();
    }

    private void PauseWhenEmpty<T>() where T : IComponent
    {
        var maps = AllEntityQuery<T, MapComponent>();
        while (maps.MoveNext(out var uid, out _, out var mapComp))
        {
            if (!mapComp.MapInitialized || Terminating(uid))
                continue;

            var pause = !_occupied.Contains(mapComp.MapId);
            if (mapComp.MapPaused != pause)
                _map.SetPaused(uid, pause);
        }
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
