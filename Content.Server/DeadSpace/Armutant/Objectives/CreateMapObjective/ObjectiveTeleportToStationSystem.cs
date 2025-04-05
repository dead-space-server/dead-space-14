using Content.Server.Station.Components;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Server.DeadSpace.Armutant.Objectives.CreateMapObjective;

public sealed class ObjectiveTeleportToStationSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveTeleportToStationComponent, UseInHandEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, ObjectiveTeleportToStationComponent component, UseInHandEvent args)
    {
        if (!component.UsingItem)
        {
            var user = args.User;

            if (component.StationGridId == null || !_entities.EntityExists(component.StationGridId.Value))
            {
                component.StationGridId = FindStationGrid();
                if (component.StationGridId == null)
                    return;
            }

            var stationGridId = component.StationGridId.Value;

            if (!_entities.TryGetComponent<TransformComponent>(user, out var userTransform))
                return;

            if (!_entities.TryGetComponent<TransformComponent>(stationGridId, out var gridTransform))
                return;

            userTransform.Coordinates = gridTransform.Coordinates;

            component.UsingItem = true;

            if (_net.IsServer && component.SelfEffect is not null)
            {
                var effect = Spawn(component.SelfEffect, Transform(args.User).Coordinates);
                _transformSystem.SetParent(effect, args.User);
            }
        }
        else
            return;
    }

    /// <summary>
    /// FindStationGrid находит грид с компонентом BecomesStationComponent
    /// </summary>
    private EntityUid? FindStationGrid()
    {
        foreach (var mapId in _mapManager.GetAllMapIds())
        {
            foreach (var gridEntity in _mapManager.GetAllGrids(mapId))
            {
                if (_entities.HasComponent<BecomesStationComponent>(gridEntity.Owner))
                    return gridEntity.Owner;
            }
        }
        return null;
    }
}
