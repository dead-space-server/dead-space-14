using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     SaveGamemode: сохранение и восстановление инициализированной карты.
/// </summary>
public sealed partial class SaveSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    private static readonly ResPath SaveDir = new("/Saves/GameMode");

    /// <summary>
    ///     Возвращает путь к файлу сохранения для заданного слота.
    /// </summary>
    public static ResPath GetSavePath(string slot)
    {
        return SaveDir / slot / "map.yml";
    }

    /// <summary>
    ///     Сохраняет инициализированную карту по указанному пути.
    /// </summary>
    /// <param name="force">
    ///     Принудительное сохранение: явно снимает ожидание pre-map-init сущностей
    ///     (<see cref="SerializationOptions.ExpectPreInit"/> = false), поэтому
    ///     сохранение уже инициализированной карты не пишет ошибок в лог.
    ///     При false используется поведение движка по умолчанию.
    /// </param>
    public bool TrySaveMap(MapId mapId, ResPath path, bool force = false)
    {
        if (mapId == MapId.Nullspace)
        {
            Log.Error($"SaveSystem: попытка сохранить Nullspace-карту в '{path}'");
            return false;
        }

        if (!_mapSystem.MapExists(mapId))
        {
            Log.Error($"SaveSystem: карта {mapId} не существует");
            return false;
        }

        if (!_mapSystem.TryGetMap(mapId, out var mapUid))
        {
            Log.Error($"SaveSystem: не удалось получить entity карты {mapId}");
            return false;
        }

        var mapName = EntityManager.TryGetComponent<MetaDataComponent>(mapUid.Value, out var metaData)
            ? metaData.EntityName
            : "<no MetaData>";

        Log.Info($"SaveSystem: попытка сохранить карту MapId={mapId}, UID={mapUid.Value}, Name='{mapName}' в '{path}'");

        if (_mapSystem.IsInitialized(mapId) && !force)
        {
            Log.Error($"SaveSystem: карта {mapId} уже инициализирована. Используйте force=true для принудительного сохранения");
            return false;
        }

        var options = force
            ? SerializationOptions.Default with { ExpectPreInit = false }
            : (SerializationOptions?) null;

        if (!_mapLoader.TrySaveMap(mapId, path, options))
        {
            Log.Error($"SaveSystem: сериализация карты {mapId} не удалась, файл '{path}' не тронут");
            return false;
        }

        Log.Info($"SaveSystem: карта MapId={mapId}, UID={mapUid.Value}, Name='{mapName}' сохранена в '{path}'");
        return true;
    }

    /// <summary>
    ///     Сохраняет инициализированную карту в слот /Saves/GameMode/{slot}/map.yml.
    /// </summary>
    public bool TrySaveSlot(MapId mapId, string slot, bool force = false)
    {
        return TrySaveMap(mapId, GetSavePath(slot), force);
    }

    /// <summary>
    ///     Загружает карту из сохранения. Возвращает true, если файл существует и загрузился.
    /// </summary>
    public bool TryLoadSlot(string slot, out MapId mapId)
    {
        mapId = MapId.Nullspace;

        var path = GetSavePath(slot);
        var rooted = path.ToRootedPath();

        if (!_res.UserData.Exists(rooted))
        {
            Log.Warning($"SaveSystem: сохранение '{slot}' не найдено по пути {rooted}");
            return false;
        }

        var opts = DeserializationOptions.Default with
        {
            InitializeMaps = true,
            PauseMaps = false,
        };

        if (!_mapLoader.TryLoadMap(path, out var map, out _, opts))
            return false;

        if (map is { } mapUid)
            mapId = mapUid.Comp.MapId;

        return mapId != MapId.Nullspace;
    }

    /// <summary>
    ///     Удаляет папку сохранения слота, если она существует.
    /// </summary>
    public bool TryDeleteSlot(string slot)
    {
        var path = (SaveDir / slot).ToRootedPath();

        if (!_res.UserData.Exists(path))
            return false;

        try
        {
            _res.UserData.Delete(path);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: не удалось удалить сохранение '{slot}':\n{e}");
            return false;
        }
    }

    /// <summary>
    ///     Проверяет, существует ли сохранение для слота.
    /// </summary>
    public bool SlotExists(string slot)
    {
        return _res.UserData.Exists(GetSavePath(slot).ToRootedPath());
    }
}
