using System.Numerics;
using Content.Server.Station.Components;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Armutant.Objectives.CreateMapObjective;

public sealed class ObjectiveTeleportToStationSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveTeleportToStationComponent, UseInHandEvent>(OnInteractUsing);
    }
    private void OnInteractUsing(Entity<ObjectiveTeleportToStationComponent> uid, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (uid.Comp.UsingItem)
            return;

        if (uid.Comp.StationGridId == null || !_entities.EntityExists(uid.Comp.StationGridId.Value))
        {
            uid.Comp.StationGridId = FindStationGrid();
            if (uid.Comp.StationGridId == null)
                return;
        }

        var stationGrid = uid.Comp.StationGridId.Value;

        if (!_entities.TryGetComponent(args.User, out TransformComponent? userXform))
            return;

        var offset = new Vector2(
            _random.NextFloat(-25f, 25f),
            _random.NextFloat(-25f, 25f)
        );

        userXform.Coordinates = new EntityCoordinates(stationGrid, offset);

        uid.Comp.UsingItem = true;
        args.Handled = true;

        if (_net.IsServer && uid.Comp.SelfEffect is not null)
        {
            var effect = Spawn(uid.Comp.SelfEffect, Transform(args.User).Coordinates);
            _transformSystem.SetParent(effect, args.User);
        }
    }
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
