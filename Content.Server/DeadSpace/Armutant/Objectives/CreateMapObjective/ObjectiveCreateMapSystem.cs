using System.Numerics;
using Content.Server.DeadSpace.Armutant.Base.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;
using Robust.Shared.Console;
using Robust.Shared.Random;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Map.Components;

namespace Content.Server.DeadSpace.Armutant.Objectives.CreateMapObjective;

public sealed class ObjectiveCreateMapSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IConsoleHost _consoleHost = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveCreateMapComponent, UseInHandEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, ObjectiveCreateMapComponent component, UseInHandEvent args)
    {
        var user = args.User;
        if (!_entities.HasComponent<ArmutantArmsComponent>(user))
            return;

        int randomMapId;
        MapId mapId;
        do
        {
            randomMapId = _random.Next(10000, 99999);
            mapId = new MapId(randomMapId);
        }
        while (_map.MapExists(mapId));

        component.MapId = randomMapId;

        CreateMap(user, component);
    }

    public void CreateMap(EntityUid teleporter, ObjectiveCreateMapComponent component)
    {
        var initialMapId = new MapId(component.MapId);
        if (_map.MapExists(initialMapId))
            return;

        if (string.IsNullOrWhiteSpace(component.MapPath))
            return;

        var resPath = new ResPath(component.MapPath);

        Entity<MapComponent>? mapEntity;
        if (!_mapLoader.TryLoadMap(resPath, out mapEntity, out _))
            return;

        var loadedMapId = mapEntity.Value.Comp.MapId;

        _consoleHost.ExecuteCommand($"mapinit {loadedMapId}");

        var mapEntityUid = _map.GetMapEntityId(loadedMapId);
        if (!_entities.EntityExists(mapEntityUid))
            return;

        if (!_entities.TryGetComponent(teleporter, out TransformComponent? transform))
            return;

        transform.Coordinates = new EntityCoordinates(mapEntityUid, Vector2.Zero);

        SpawnAttachedTo(component.SelfEffect, Transform(teleporter).Coordinates);
    }
}
