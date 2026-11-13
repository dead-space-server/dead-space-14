using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
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
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    ///     Проб полей компонентов для чистки ссылок: без него пост-процессор не знает,
    ///     какие поля вообще могут содержать ссылки на сущности, и подмешивает ли прототип
    ///     данные компонента при загрузке.
    /// </summary>
    private ComponentFieldProbe? _fieldProbe;
    internal IComponentFieldProbe FieldProbe =>
        _fieldProbe ??= new ComponentFieldProbe(_componentFactory, _prototypeManager);

    private static readonly ResPath SaveDir = new("/Saves/GameMode");

    /// <summary>Имя флага «сущность уже прошла MapInit» в YAML.</summary>
    private const string MapInitKey = "mapInit";

    /// <summary>Имя компонента карты в YAML — у него поля инициализации тоже должны быть сняты.</summary>
    private const string MapComponentName = "Map";

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
    /// <remarks>
    ///     Карта сначала сериализуется в память, и только потом файл на диске подменяется готовым
    ///     документом (<see cref="TryWriteAtomically"/>). Движуковый
    ///     <see cref="MapLoaderSystem.TrySaveMap(Robust.Shared.GameObjects.EntityUid,ResPath,SerializationOptions?)"/>
    ///     открывает файл на запись ДО сериализации, поэтому любое исключение сериализатора оставляло
    ///     после себя пустой файл поверх предыдущего хорошего сохранения.
    /// </remarks>
    /// <param name="force">
    ///     Принудительное сохранение: карта уже инициализирована, ожидание pre-map-init сущностей
    ///     (<see cref="SerializationOptions.ExpectPreInit"/>) снимается.
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

        var mapName = TryComp(mapUid.Value, out MetaDataComponent? metaData)
            ? metaData.EntityName
            : "<no MetaData>";

        Log.Info($"SaveSystem: попытка сохранить карту MapId={mapId}, UID={mapUid.Value}, Name='{mapName}' в '{path}'");

        if (_mapSystem.IsInitialized(mapId) && !force)
        {
            Log.Error($"SaveSystem: карта {mapId} уже инициализирована. Используйте force=true для принудительного сохранения");
            return false;
        }

        var data = SerializeMap(mapId);
        if (data == null || !TryWriteAtomically(data, path))
            return false;

        Log.Info($"SaveSystem: карта MapId={mapId}, UID={mapUid.Value}, Name='{mapName}' сохранена в '{path}'");
        return true;
    }

    /// <summary>
    ///     Сериализует карту в память, ничего не записывая на диск.
    /// </summary>
    /// <remarks>
    ///     Ожидается post-mapinit состояние карты, а исключение в сериализаторе одного компонента не должно
    ///     ронять всё сохранение: движок по умолчанию пробрасывает его наружу
    ///     (<see cref="EntityExceptionBehaviour.Rethrow"/>), и одна битая сущность обрывала сохранение целиком.
    /// </remarks>
    /// <param name="mapId">Карта, которую нужно сериализовать в память.</param>
    public MappingDataNode? SerializeMap(MapId mapId)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid))
        {
            Log.Error($"SaveSystem: не удалось получить entity карты {mapId}");
            return null;
        }

        var options = SerializationOptions.Default with
        {
            ExpectPreInit = false,
            EntityExceptionBehaviour = EntityExceptionBehaviour.IgnoreEntityAndChildren,
        };

        try
        {
            var (node, category) = _mapLoader.SerializeEntitiesRecursive([mapUid.Value], options);

            if (category != FileCategory.Map)
            {
                Log.Error($"SaveSystem: ожидалась категория Map, получено {category} для карты {mapId}");
                return null;
            }

            return node;
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: сериализация карты {mapId} в память не удалась:\n{e}");
            return null;
        }
    }

    /// <summary>
    ///     Подменяет файл сохранения полностью готовым деревом: сначала временный файл в той же папке,
    ///     проверка что там есть содержимое, и только потом замена.
    /// </summary>
    /// <remarks>
    ///     Пока документ не записан и не проверен, старый файл остаётся на месте, а предыдущая версия
    ///     сохранения сохраняется рядом как <c>*.bak</c>. Пустой или битый дамп слот больше не затирает.
    /// </remarks>
    public bool TryWriteAtomically(MappingDataNode? data, ResPath path)
    {
        if (data == null || data.Count == 0)
        {
            Log.Error($"SaveSystem: нечего писать в '{path}', файл не тронут");
            return false;
        }

        // Карта без meta/entities — это либо пустое дерево, либо результат сорванной сериализации.
        if (!data.Has("meta") || !data.Has(MapStateFilter.EntitiesKey))
        {
            Log.Error($"SaveSystem: дерево для '{path}' не похоже на карту (нет meta/entities), файл не тронут");
            return false;
        }

        var rooted = path.ToRootedPath();
        var temp = rooted.WithName(rooted.Filename + ".tmp");

        try
        {
            _res.UserData.CreateDir(rooted.Directory);

            long length;
            using (var stream = _res.UserData.Open(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // leaveOpen: StreamWriter по умолчанию закрывает вместе с собой и базовый поток,
                // а длину файла нужно прочитать, когда данные уже записаны, но файл ещё открыт.
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024 * 1024, true))
                {
                    data.Write(writer);
                    writer.Flush();
                }

                length = stream.Length;
            }

            if (length == 0)
            {
                Log.Error($"SaveSystem: временный файл '{temp}' оказался пустым, '{rooted}' не тронут");
                TryDeleteFile(temp);
                return false;
            }

            return TryReplaceFromTemp(temp, rooted);
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: не удалось записать '{rooted}':\n{e}");
            TryDeleteFile(temp);
            return false;
        }
    }

    /// <summary>
    ///     Подменяет файл уже готовым временным: предыдущая версия уходит в <c>*.bak</c>,
    ///     временный файл становится основным.
    /// </summary>
    /// <remarks>
    ///     Порядок подобран так, чтобы основной файл не мог исчезнуть бесследно: сначала он переименовывается
    ///     в резервную копию, и только затем встает новый. Движковый Rename (File.Move) не умеет
    ///     перезаписывать, поэтому резервную копию предыдущего раунда удаляем заранее.
    /// </remarks>
    private bool TryReplaceFromTemp(ResPath temp, ResPath target)
    {
        var backup = target.WithName(target.Filename + ".bak");

        try
        {
            if (_res.UserData.Exists(backup))
                _res.UserData.Delete(backup);

            if (_res.UserData.Exists(target))
                _res.UserData.Rename(target, backup);

            _res.UserData.Rename(temp, target);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: не удалось подменить '{target}' файлом '{temp}':\n{e}");

            // Если основной файл уже уехал в резервную копию, а новый встать не смог — возвращаем как было.
            try
            {
                if (_res.UserData.Exists(backup) && !_res.UserData.Exists(target))
                    _res.UserData.Rename(backup, target);
            }
            catch (Exception restoreError)
            {
                Log.Error($"SaveSystem: не удалось вернуть '{target}' из '{backup}':\n{restoreError}");
            }

            return false;
        }
    }

    private void TryDeleteFile(ResPath path)
    {
        try
        {
            if (_res.UserData.Exists(path))
                _res.UserData.Delete(path);
        }
        catch
        {
            // Временный файл мог и не создаться.
        }
    }

    /// <summary>
    ///     Сохраняет инициализированную карту в слот /Saves/GameMode/{slot}/map.yml.
    /// </summary>
    public bool TrySaveSlot(MapId mapId, string slot, bool force = false)
    {
        return TrySaveMap(mapId, GetSavePath(slot), force);
    }

    /// <summary>
    ///     Рабочая конфигурация фильтрации: копия <see cref="MapFilterConfig.Default"/> (общий объект
    ///     править нельзя), в которой правила «компонент есть в прототипе» уже раскрыты в id прототипов.
    /// </summary>
    public MapFilterConfig CreateFilterConfig()
    {
        var config = MapFilterConfig.Default.Clone();
        ExpandPrototypeComponentRules(config);
        return config;
    }

    /// <summary>
    ///     Превращает <see cref="MapFilterConfig.RemovePrototypesWithComponents"/> в записи
    ///     <see cref="MapFilterConfig.RemovePrototypes"/>: пост-процессор работает с деревом YAML и
    ///     прототипы открыть не может. Смотреть нужно в прототип: в файл пишутся только отличающиеся
    ///     от него поля, и по файлу такая сущность неотличима от любой другой.
    /// </summary>
    public void ExpandPrototypeComponentRules(MapFilterConfig config)
    {
        if (config.RemovePrototypesWithComponents.Count == 0)
            return;

        var added = 0;
        foreach (var proto in _prototypeManager.EnumeratePrototypes<EntityPrototype>())
        {
            foreach (var component in config.RemovePrototypesWithComponents)
            {
                if (!proto.Components.ContainsKey(component))
                    continue;

                if (config.RemovePrototypes.Add(proto.ID))
                    added++;

                break;
            }
        }

        Log.Debug($"SaveSystem: правило «компонент в прототипе» раскрыто в {added} прототипов " +
                  $"по {config.RemovePrototypesWithComponents.Count} компонентам");
    }

    /// <summary>
    ///     Сохраняет карту в слот через пост-процессор <see cref="MapStateFilter"/>:
    ///     карта сериализуется в память, из неё выкидываются нежелательные сущности и runtime-поля,
    ///     вычищаются ссылки на удалённые сущности, и только потом результат пишется на диск.
    /// </summary>
    /// <param name="report">Статистика фильтрации, если она нужна вызывающему коду.</param>
    /// <param name="config">Конфигурация фильтра; по умолчанию <see cref="CreateFilterConfig"/>.</param>
    public bool TrySaveFilteredSlot(
        MapId mapId,
        string slot,
        out MapFilterReport report,
        MapFilterConfig? config = null)
    {
        report = new MapFilterReport();
        config ??= CreateFilterConfig();

        var path = GetSavePath(slot);

        if (mapId == MapId.Nullspace || !_mapSystem.MapExists(mapId))
        {
            Log.Error($"SaveSystem: не удалось получить карту {mapId} для фильтрованного сохранения в '{path}'");
            return false;
        }

        // Тот же режим, что и у force-сохранения: карта уже инициализирована.
        var data = SerializeMap(mapId);
        if (data == null)
        {
            Log.Error($"SaveSystem: фильтрованное сохранение '{slot}' отменено, файл '{path}' не тронут");
            return false;
        }

        try
        {
            report = MapStateFilter.Run(data, config, FieldProbe);
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: пост-обработка карты {mapId} не удалась, файл '{path}' не тронут:\n{e}");
            return false;
        }

        if (report.MissingEntitiesSection)
        {
            Log.Error($"SaveSystem: в дереве карты {mapId} нет секции 'entities', " +
                      $"сохранение '{slot}' отменено, файл '{path}' не тронут");
            return false;
        }

        if (!TryWriteAtomically(data, path))
            return false;

        LogSanitized(report, $"сохранение '{slot}'");

        // Отложенные состояния пишутся рядом с картой и применяются после загрузки (Этап 4).
        if (report.DeferredStates.Count > 0)
            WriteDeferredStates(config, slot, report);

        Log.Info($"SaveSystem: фильтрованное сохранение '{slot}' записано в '{path.ToRootedPath()}'. {report}");
        LogRemoved(report, $"сохранение '{slot}'");
        return true;
    }

    /// <summary>
    ///     Отдельной строкой печатает, какие прототипы и в каком количестве вычищены из сохранения.
    ///     Общий счётчик удалений не отвечает на вопрос «чего не стало в раунде», а искать удалённое
    ///     по файлу сохранения дорого — файл больше ста мегабайт.
    /// </summary>
    private void LogRemoved(MapFilterReport report, string source)
    {
        var top = report.RemovedTop();
        if (top.Length == 0)
            return;

        Log.Info($"SaveSystem: {source}: удалено по прототипам: {top}");
    }

    /// <summary>
    ///     Прогоняет пост-процессор по уже существующему файлу сохранения, перезаписывая его на месте.
    ///     Нужен для ручной проверки конфига без перезапуска раунда.
    /// </summary>
    public bool TryFilterSaveFile(ResPath path, out MapFilterReport report, MapFilterConfig? config = null)
    {
        report = new MapFilterReport();
        config ??= CreateFilterConfig();

        if (!_mapLoader.TryReadFile(path, out var data))
        {
            Log.Error($"SaveSystem: не удалось прочитать '{path}' для фильтрации");
            return false;
        }

        try
        {
            report = MapStateFilter.Run(data, config, FieldProbe);
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: фильтрация '{path}' не удалась, файл не тронут:\n{e}");
            return false;
        }

        var rooted = path.ToRootedPath();

        if (report.MissingEntitiesSection)
        {
            Log.Error($"SaveSystem: в '{rooted}' нет секции 'entities' — это не карта, файл не тронут");
            return false;
        }

        if (!TryWriteAtomically(data, path))
            return false;

        LogSanitized(report, $"файл '{rooted}'");

        Log.Info($"SaveSystem: файл '{rooted}' отфильтрован. {report}");
        return true;
    }

    /// <summary>
    ///     Выводит в лог подробности починки компонентов: какие обязательные поля дописаны и какие
    ///     компоненты пришлось снять из-за того, что заполнить поле нечем.
    /// </summary>
    /// <remarks>
    ///     Иначе исчезновение или самодостаточность компонента в сохранении невозможно объяснить
    ///     постфактум, а их десятки тысяч — поэтому подробности ограничены <see cref="MapFilterReport.MaxDetails"/>.
    /// </remarks>
    private void LogSanitized(MapFilterReport report, string source)
    {
        if (report.SanitizedComponents > 0)
        {
            Log.Warning($"SaveSystem: {source}: снято компонентов без обязательных полей: {report.SanitizedComponents}");

            foreach (var detail in report.SanitizedDetails)
                Log.Warning($"SaveSystem: {source}: {detail}");

            if (report.SanitizedComponents > report.SanitizedDetails.Count)
                Log.Warning($"SaveSystem: {source}: список сокращён до {report.SanitizedDetails.Count} записей");
        }

        if (report.RemovedParentlessEntities > 0)
        {
            Log.Warning($"SaveSystem: {source}: удалено сущностей без родителя: {report.RemovedParentlessEntities}");

            foreach (var detail in report.ParentlessDetails)
                Log.Warning($"SaveSystem: {source}: {detail}");

            if (report.RemovedParentlessEntities > report.ParentlessDetails.Count)
                Log.Warning($"SaveSystem: {source}: список сокращён до {report.ParentlessDetails.Count} записей");
        }

        if (report.RepairedRequiredFields == 0)
            return;

        Log.Info($"SaveSystem: {source}: дописано обязательных полей в узлы компонентов: {report.RepairedRequiredFields}");

        foreach (var detail in report.RepairedDetails)
            Log.Info($"SaveSystem: {source}: {detail}");

        if (report.RepairedRequiredFields > report.RepairedDetails.Count)
            Log.Info($"SaveSystem: {source}: список сокращён до {report.RepairedDetails.Count} записей");
    }

    /// <summary>
    ///     Пишет отложенные состояния в sidecar-файл слота (см. <see cref="MapFilterConfig.DeferredFileName"/>).
    /// </summary>
    private void WriteDeferredStates(MapFilterConfig config, string slot, MapFilterReport report)
    {
        var path = SaveDir / slot / config.DeferredFileName;

        if (TryWriteAtomically(MapStateFilter.BuildDeferredTree(report), path))
            Log.Info($"SaveSystem: отложенных состояний записано в '{path.ToRootedPath()}': {report.DeferredStates.Count}");
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

        // Файл мог быть записан версией, срезавшей mapInit: доставляем флаг сущностям перед загрузкой.
        PrepareMapInitFlags(path);

        // В отличие от DefaultMap, эту карту тикер не инициализирует, поэтому InitializeMap здесь.
        // Сущности уже с флагом mapInit, и инициализация сводится к карте и к тем сущностям,
        // которые не попали под уже инициализированные поддеревья (гриды и их содержимое).
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
    ///     Выравнивает флаг <c>mapInit</c> в файле сохранения перед загрузкой: проставляет его всем
    ///     сущностям и снимает у самой карты. Файл перезаписывается целиком (через временный файл и
    ///     подмену), изменения заносятся только если есть что править.
    /// </summary>
    /// <remarks>
    ///     Без флага читатель формата 7 считает сущности предыннициализированными и прогоняет MapInit
    ///     заново, а прототип возвращает компоненты наполнения, которых в файле нет: <c>ContainerFill</c>
    ///     и <c>StorageFill</c> набивают контейнеры вторым комплектом, <c>GridFill</c> докидывает
    ///     спаскапсулы, <c>RandomFillSolution</c> долирует реагенты. Файлы прежней версии пост-процессора
    ///     флаг срезали целиком, поэтому доставляем его заново.
    ///     У карты флаг оставлять нельзя: <c>GameTicker.StartRound</c> инициализирует <c>DefaultMap</c> сам.
    ///     Файл — сотни мегабайт, YAML-дерево из него строить дорого, поэтому правим построчно:
    ///     флаг вставляется первым ключом сущности, у карты удаляется он сам и поля <c>Map</c>.
    /// </remarks>
    /// <returns>Число исправленных сущностей, либо -1 при ошибке.</returns>
    public int PrepareMapInitFlags(ResPath path)
    {
        var rooted = path.ToRootedPath();

        if (!_res.UserData.Exists(rooted))
        {
            Log.Warning($"SaveSystem: файл '{rooted}' не найден, флаги mapInit не готовы");
            return -1;
        }

        var mapsKey = new Regex(@"^maps:\s*$", RegexOptions.Compiled);
        var rootKey = new Regex(@"^[A-Za-z]", RegexOptions.Compiled);
        var rootValue = new Regex(@"^- (\d+)\s*$", RegexOptions.Compiled);
        var entitiesKey = new Regex(@"^entities:\s*$", RegexOptions.Compiled);
        var entityItem = new Regex(@"^(?<indent> *)- uid: (?<uid>\d+)\s*$", RegexOptions.Compiled);
        var componentItem = new Regex(@"^ +- type: (?<type>\S+)\s*$", RegexOptions.Compiled);
        var mapInitField = new Regex(@"^ +Map(?:Initialized|Paused)\s*:", RegexOptions.Compiled);

        var mapUids = new HashSet<int>();
        var tempPath = rooted.WithExtension("yml.tmp");

        var marked = 0;
        var cleared = 0;
        var tempLength = 0L;

        try
        {
            using (var reader = new StreamReader(_res.UserData.Open(rooted, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8))
            {
                using (var stream = _res.UserData.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // leaveOpen — иначе writer закроет поток, и stream.Length бросит ObjectDisposedException.
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024 * 1024, true))
                    {
                        var inEntities = false;
                        var inMapsHeader = false;
                        var isMapEntity = false;
                        var keyIndent = 0;
                        var needInsert = false;
                        var component = string.Empty;

                        string? line;
                        while ((line = reader.ReadLine()) is not null)
                        {
                            // Заголовок: из него нужен только список карт.
                            if (!inEntities)
                            {
                                if (entitiesKey.IsMatch(line))
                                {
                                    inEntities = true;
                                    inMapsHeader = false;
                                }
                                else if (inMapsHeader)
                                {
                                    var value = rootValue.Match(line);
                                    if (value.Success)
                                        mapUids.Add(int.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture));
                                    else if (rootKey.IsMatch(line))
                                        inMapsHeader = false;
                                }
                                else if (mapsKey.IsMatch(line))
                                {
                                    inMapsHeader = true;
                                }

                                writer.WriteLine(line);
                                continue;
                            }

                            var item = entityItem.Match(line);
                            if (item.Success)
                            {
                                var uid = int.Parse(item.Groups["uid"].Value, CultureInfo.InvariantCulture);

                                // Ключи сущности на два пробела глубже её пункта в списке группы.
                                keyIndent = item.Groups["indent"].Length + 2;
                                isMapEntity = mapUids.Contains(uid);
                                needInsert = !isMapEntity;
                                component = string.Empty;

                                writer.WriteLine(line);
                                continue;
                            }

                            if (isMapEntity)
                            {
                                var comp = componentItem.Match(line);
                                if (comp.Success)
                                {
                                    component = comp.Groups["type"].Value;
                                    writer.WriteLine(line);
                                    continue;
                                }

                                if (component.Equals(MapComponentName, StringComparison.OrdinalIgnoreCase) &&
                                    mapInitField.IsMatch(line))
                                {
                                    cleared++;
                                    continue;
                                }
                            }

                            if (!IsKeyAtIndent(line, keyIndent))
                            {
                                writer.WriteLine(line);
                                continue;
                            }

                            var colon = line.IndexOf(':', keyIndent);
                            var isInitFlag = colon > keyIndent &&
                                             line.AsSpan(keyIndent, colon - keyIndent).Trim()
                                                 .Equals(MapInitKey, StringComparison.OrdinalIgnoreCase);

                            if (isInitFlag)
                            {
                                // Флаг у сущности уже есть — вставлять второй не нужно.
                                needInsert = false;

                                if (isMapEntity)
                                {
                                    cleared++;
                                    continue;
                                }

                                if (!line[(colon + 1)..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                                {
                                    writer.WriteLine(line[..keyIndent] + MapInitKey + ": true");
                                    marked++;
                                    continue;
                                }

                                writer.WriteLine(line);
                                continue;
                            }

                            if (needInsert)
                            {
                                writer.WriteLine(new string(' ', keyIndent) + MapInitKey + ": true");
                                writer.WriteLine(line);
                                needInsert = false;
                                marked++;
                                continue;
                            }

                            writer.WriteLine(line);
                        }
                    }

                    tempLength = stream.Length;
                }
            }

            // Пустая копия означает, что чтение исходника оборвалось — исходный файл не трогаем.
            if (tempLength == 0)
            {
                Log.Error($"SaveSystem: '{rooted}' пустой, подготовка флагов mapInit пропущена");
                return -1;
            }

            // Файл уже в порядке (его записывал пост-процессор) — не перезаписываем его.
            if (marked + cleared == 0)
                return 0;

            // Без списка карт мы не знали, у кого флаг обязан оставаться снятым, и поставили его всем:
            // такая загрузка упадёт на «Map is already initialized».
            if (mapUids.Count == 0)
            {
                Log.Error($"SaveSystem: в '{rooted}' нет списка 'maps', флаги mapInit не проставлены");
                return -1;
            }

            // Подменяем исходный файл исправленной копией (прежняя версия остаётся в *.bak).
            if (!TryReplaceFromTemp(tempPath, rooted))
                return -1;
        }
        catch (Exception e)
        {
            Log.Error($"SaveSystem: не удалось подготовить флаги mapInit в '{rooted}':\n{e}");
            return -1;
        }
        finally
        {
            TryDeleteFile(tempPath);
        }

        Log.Info($"SaveSystem: mapInit проставлен {marked} сущностям, у карты снято {cleared} флагов ('{rooted}')");
        return marked + cleared;
    }

    /// <summary>
    ///     True, если строка — ключ узла ровно на отступе <paramref name="indent"/>:
    ///     ровно столько пробелов, затем буква. Поля компонентов лежат глубже и не подходят.
    /// </summary>
    private static bool IsKeyAtIndent(string line, int indent)
    {
        if (line.Length <= indent)
            return false;

        for (var i = 0; i < indent; i++)
        {
            if (line[i] != ' ')
                return false;
        }

        return char.IsLetter(line[indent]);
    }

    /// <summary>
    ///     Проверяет, существует ли сохранение для слота.
    /// </summary>
    public bool SlotExists(string slot)
    {
        return _res.UserData.Exists(GetSavePath(slot).ToRootedPath());
    }
}
