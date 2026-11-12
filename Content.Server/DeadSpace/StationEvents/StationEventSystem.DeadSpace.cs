// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Server.DeadSpace.CentComm;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.StationEvents.Events;

// DS14: station events stay on the station (map) the rule was targeted at, see GameRuleStationSystem.
public abstract partial class StationEventSystem<T>
{
    [Dependency] protected GameRuleStationSystem RuleStation = default!;

    /// <summary>
    /// Gets the station this event rule is targeted at, or a random event-eligible station if it has no target.
    /// </summary>
    protected bool TryGetEventStation(EntityUid rule,
        [NotNullWhen(true)] out Entity<StationDataComponent>? station,
        Func<EntityUid, bool>? filter = null)
    {
        RuleStation.EnsureEventTarget(rule);
        if (RuleStation.GetTargetStation(rule) is { } target)
        {
            station = null;
            if (!TryComp<StationDataComponent>(target, out var data) || filter != null && !filter(target))
                return false;

            station = (target, data);
            return true;
        }

        if (!Station.TryGetRandomStation<StationEventEligibleComponent>(out var random, filter))
        {
            station = null;
            return false;
        }

        station = (random.Value.Owner, random.Value.Comp1);
        return true;
    }

    /// <summary>
    /// Finds a random tile on the station this event rule is targeted at.
    /// </summary>
    protected bool TryFindEventTile(EntityUid rule,
        out Vector2i tile,
        [NotNullWhen(true)] out Entity<StationDataComponent>? targetStation,
        [NotNullWhen(true)] out Entity<MapGridComponent>? targetGrid,
        out EntityCoordinates targetCoords)
    {
        tile = default;
        targetGrid = null;
        targetCoords = EntityCoordinates.Invalid;

        return TryGetEventStation(rule, out targetStation)
               && Station.TryFindRandomTileOnStation(targetStation.Value, out tile, out targetGrid, out targetCoords);
    }

    /// <summary>
    /// Gets entities with <typeparamref name="TComponent"/> on the largest grid of the station this event rule is targeted at.
    /// </summary>
    protected HashSet<Entity<TComponent>> GetEventEntitiesOnStation<TComponent>(EntityUid rule,
        bool onlyAnchored,
        out Entity<StationDataComponent>? station)
        where TComponent : IComponent
    {
        HashSet<Entity<TComponent>> entities = [];

        if (!TryGetEventStation(rule, out station))
            return entities;

        if (Station.GetLargestGrid(station.Value.Owner) is not { } grid)
            return entities;

        var query = EntityQueryEnumerator<TComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (onlyAnchored && !xform.Anchored)
                continue;

            if (xform.GridUid != grid)
                continue;

            entities.Add((uid, component));
        }

        return entities;
    }

    /// <inheritdoc cref="GetEventEntitiesOnStation{TComponent}(EntityUid, bool, out Entity{StationDataComponent}?)"/>
    protected HashSet<Entity<TComponent>> GetEventEntitiesOnStation<TComponent>(EntityUid rule, bool onlyAnchored)
        where TComponent : IComponent
    {
        return GetEventEntitiesOnStation<TComponent>(rule, onlyAnchored, out _);
    }
}
