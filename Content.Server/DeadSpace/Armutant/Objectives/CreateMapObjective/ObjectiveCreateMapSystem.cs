using System.Numerics;
using Content.Server.DeadSpace.Armutant.Abilities.Components;
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
        // Проверяем, существует ли карта с данным ID
        var initialMapId = new MapId(component.MapId);
        if (_map.MapExists(initialMapId))
            return;

        if (string.IsNullOrWhiteSpace(component.MapPath))
            return;

        var resPath = new ResPath(component.MapPath);

        // Объявляем переменную для сущности карты
        Entity<MapComponent>? mapEntity;
        if (!_mapLoader.TryLoadMap(resPath, out mapEntity, out _))
            return;

        var loadedMapId = mapEntity.Value.Comp.MapId;

        // Выполняем команду с использованием полученного MapId
        _consoleHost.ExecuteCommand($"mapinit {loadedMapId}");

        // Получаем EntityUid карты через MapManager
        var mapEntityUid = _map.GetMapEntityId(loadedMapId);
        if (!_entities.EntityExists(mapEntityUid))
            return;

        // Пытаемся получить TransformComponent сущности
        if (!_entities.TryGetComponent(teleporter, out TransformComponent? transform))
            return;

        // Устанавливаем координаты сущности на новую карту, в точке (0,0)
        transform.Coordinates = new EntityCoordinates(mapEntityUid, Vector2.Zero);

        // Спавн эффекта
        SpawnAttachedTo(component.SelfEffect, Transform(teleporter).Coordinates);
    }
}

