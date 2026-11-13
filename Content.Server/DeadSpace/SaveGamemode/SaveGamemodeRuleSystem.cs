using System;
using System.Collections.Generic;
using System.IO;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Maps;
using Content.Shared.Station.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.SaveGamemode;

/// <summary>
///     Игровое правило режима сохранения/восстановления раунда:
///     при старте подменяет стандартную карту сохранением, при конце пишет карту обратно.
/// </summary>
public sealed class SaveGamemodeRuleSystem : GameRuleSystem<SaveGamemodeRuleComponent>
{
    [Dependency] private readonly SaveSystem _save = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IGameMapManager _gameMap = default!;
    [Dependency] private readonly NPCSystem _npc = default!;

    /// <summary>
    ///     Результат проверки файла сохранения за текущий раунд.
    ///     Файл сохранения режимной карты большой, поэтому разбирать YAML второй раз в том же раунде незачем:
    ///     <see cref="OnLoadingMaps"/> выполняется раньше <see cref="Started"/> и всегда обновляет кэш.
    /// </summary>
    private readonly Dictionary<string, bool> _saveValidity = new();

    /// <summary>
    ///     Карты, которые режим отменил ради сохранения (первая — будущая DefaultMap).
    ///     Нужны, чтобы при неудачной загрузке сохранения вернуть раунду обычную станцию:
    ///     пустая DefaultMap без станции заканчивается падением на распределении работ.
    /// </summary>
    private readonly List<GameMapPrototype> _replacedMaps = new();

    /// <summary>
    ///     Идёт регистрация станции для карты, поднятой из сохранения.
    ///     В это окно обязательные гриды раунда станции не спавнятся, см. <see cref="OnStationGridAdded"/>.
    /// </summary>
    private bool _registeringSavedMap;

    /// <summary>
    ///     Станции, у которых спавн обязательных гридов уже убран — чтобы не писать об этом в лог на каждую сетку.
    /// </summary>
    private readonly HashSet<EntityUid> _gridsSuppressed = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps, before: [typeof(StationSystem)]);
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdded);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    /// <summary>
    ///     Снимает у станции, созданной для загруженного сохранения, компоненты, которые на
    ///     <c>StationPostInitEvent</c> заводят обязательные гриды раунда.
    /// </summary>
    /// <remarks>
    ///     Станцию создаёт <c>InitializeNewStation</c> внутри <c>PostGameMapLoad</c>, и сразу после этого
    ///     <c>StationPostInitEvent</c> рождает карго-шаттл, торговую станцию, шаттл утилизаторов и руины.
    ///     Сохранение уже содержит их прошлые версии, поэтому раунд получал по два комплекта.
    ///     <c>StationGridAddedEvent</c> — единственная точка между появлением станции и её пост-инициализацией.
    ///     Спасательный и прибывающий шаттлы не трогаем: их гриды на отдельных картах, в сохранение они не
    ///     попадают, а прибытие нужно для позднего захода игроков.
    /// </remarks>
    private void OnStationGridAdded(StationGridAddedEvent ev)
    {
        if (!_registeringSavedMap)
            return;

        // ИЛИ, а не И: снимаются оба компонента, независимо от того, какой из них оказался на станции.
        var removed = RemComp<GridSpawnComponent>(ev.Station) | RemComp<StationCargoShuttleComponent>(ev.Station);

        if (removed && _gridsSuppressed.Add(ev.Station))
        {
            Log.Info($"SaveGamemode: станция {ToPrettyString(ev.Station)} из сохранения — обязательные гриды " +
                     "(карго-шаттл, торговая станция, шаттл утилизаторов, руины) не спавнятся");
        }
    }

    /// <summary>
    ///     Перехватывает загрузку стандартных карт: если режим грузится из сохранения, очищаем список карт —
    ///     движок создаст пустую DefaultMap, а в Started мы подменим её сохранением.
    ///     Пустой или битый файл карту не отменяет, иначе раунд стартовал бы без станции.
    /// </summary>
    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (!comp.LoadOnStart)
                continue;

            var path = SaveSystem.GetSavePath(comp.SaveSlot);
            var valid = _save.SlotExists(comp.SaveSlot) && IsSaveFileValid(path);
            _saveValidity[comp.SaveSlot] = valid;

            if (!valid)
            {
                Log.Error($"SaveGamemode: сохранение '{comp.SaveSlot}' пусто или повреждено ({path}), " +
                          "берётся стандартная карта");
                continue;
            }

            Log.Info($"SaveGamemode: подмена стандартной карты сохранением '{comp.SaveSlot}'");
            // Запоминаем отменённые карты: если сохранение не загрузится, раунду нужна обычная станция.
            _replacedMaps.Clear();
            _replacedMaps.AddRange(ev.Maps);
            ev.Maps.Clear();
            break;
        }
    }

    protected override void Started(EntityUid uid, SaveGamemodeRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        Log.Info($"SaveGamemode rule started: {uid}");

        if (component.LoadOnStart && _save.SlotExists(component.SaveSlot))
        {
            var path = SaveSystem.GetSavePath(component.SaveSlot);
            // OnLoadingMaps уже разобрал файл в этом раунде; если правила запускались вне стандартного
            // старта раунда, проверяем на месте.
            var valid = _saveValidity.TryGetValue(component.SaveSlot, out var cached)
                ? cached
                : IsSaveFileValid(path);

            if (valid)
            {
                TryLoadSaveAsDefaultMap(component);
            }
            else
            {
                Log.Error($"SaveGamemode: сохранение '{component.SaveSlot}' повреждено или пустое ({path}), загрузка отменена. Будет использована стандартная карта.");
            }
        }

        // TODO: логика старта режима (спавн игроков, телепортация на карту и т.д.)
    }

    /// <summary>
    ///     Проверяет, что файл сохранения существует, не пустой и содержит хотя бы один YAML-документ.
    /// </summary>
    private bool IsSaveFileValid(ResPath path)
    {
        var rooted = path.ToRootedPath();
        if (!_res.UserData.Exists(rooted))
            return false;

        try
        {
            using var reader = _res.UserData.OpenText(rooted);
            var content = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(content))
                return false;

            using var stringReader = new StringReader(content);
            var yaml = new YamlDotNet.RepresentationModel.YamlStream();
            yaml.Load(stringReader);
            return yaml.Documents.Count > 0;
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: ошибка при проверке сохранения {path}: {e.Message}");
            return false;
        }
    }

    protected override void Ended(EntityUid uid, SaveGamemodeRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        Log.Info($"SaveGamemode rule ended: {uid}");

        // TODO: очистка/завершение режима
    }

    /// <summary>
    ///     При завершении раунда: сохраняем карту, удаляем временные файлы,
    ///     автоматически переключаем пресет на следующий.
    /// </summary>
    private void OnRoundEnded(RoundEndedEvent ev)
    {
        // Кэш проверки файла относится к завершившемуся раунду: впереди новые сохранения и новая загрузка.
        _saveValidity.Clear();
        _replacedMaps.Clear();

        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.SaveOnRoundEnd)
                TrySaveCurrentMap(comp);

            if (comp.DeleteSaveOnRoundEnd)
                _save.TryDeleteSlot(comp.SaveSlot);

            if (!string.IsNullOrWhiteSpace(comp.NextPreset))
            {
                Log.Info($"SaveGamemode: переключение пресета на '{comp.NextPreset}'");
                GameTicker.SetGamePreset(comp.NextPreset, force: false);
            }

            break;
        }
    }

    private void TryLoadSaveAsDefaultMap(SaveGamemodeRuleComponent component)
    {
        var defaultMap = GameTicker.DefaultMap;
        if (defaultMap == MapId.Nullspace)
        {
            Log.Error("SaveGamemode: DefaultMap не установлен, загрузка сохранения невозможна");
            return;
        }

        var path = SaveSystem.GetSavePath(component.SaveSlot);

        // Файл мог быть записан версией, срезавшей mapInit: без флага движок заново прогоняет
        // MapInit, и наполнение контейнеров из прототипа высыпается вторым комплектом.
        _save.PrepareMapInitFlags(path);

        var loaded = TryLoadOnce(defaultMap, path, out var map);

        if (!loaded || map == null)
        {
            // Файл может быть синтаксически целым, но нечитаемым по составу: компонент без обязательного
            // поля, которое нечем заполнить. Пост-процессор умеет такое вычищать, а заодно сохраняет
            // предыдущую версию файла в .bak — пробуем загрузиться после него.
            Log.Warning($"SaveGamemode: сохранение '{component.SaveSlot}' не загрузилось, " +
                        $"пробую починить файл и загрузить снова");

            if (!CanReuseMapSlot(defaultMap))
            {
                // Загрузка оборвалась на середине, и карта осталась в состоянии удаления.
                // Повторно загрузиться на этот MapId уже нельзя, поэтому не тратим минуту на
                // пост-обработку файла, а сразу уходим на запасную карту.
                Log.Error($"SaveGamemode: карта {defaultMap} не освобождена, повторная загрузка невозможна");
            }
            else if (_save.TryFilterSaveFile(path, out var repair))
            {
                Log.Info($"SaveGamemode: файл '{component.SaveSlot}' пропущен через пост-обработку. {repair}");
                loaded = TryLoadOnce(defaultMap, path, out map);
            }
        }

        if (!loaded || map == null)
        {
            Log.Error($"SaveGamemode: не удалось загрузить сохранение '{component.SaveSlot}' из {path}");

            // Пустую DefaultMap мы уже удалили, а без карты раунд существовать не может:
            // возвращаем карту с тем же MapId, иначе спавн игроков упадёт на несуществующей карте.
            // Если загрузка оборвалась на середине, на этом MapId могла остаться недосозданная
            // карта — её выбрасываем и собираем обычную станцию.
            if (!TryRestoreReplacedMaps(defaultMap))
                TryCreateEmptyMap(defaultMap);

            return;
        }

        Log.Info($"SaveGamemode: сохранение '{component.SaveSlot}' загружено на карту {defaultMap}");

        WakeLoadedNpcs(defaultMap);

        // Сама станция в файл не сериализуется: она живёт в nullspace, и на карте остаётся только
        // пометка BecomesStation у сетки. Без станции раунд обрывается на спавне игроков, поэтому
        // регистрируем её так же, как это делает тикер для обычной карты.
        if (TryRegisterStations(defaultMap, component.SuppressRoundstartGrids))
            return;

        Log.Error("SaveGamemode: станцию зарегистрировать не удалось, раунд уходит на обычную карту");

        if (!TryRestoreReplacedMaps(defaultMap))
            TryCreateEmptyMap(defaultMap);
    }

    /// <summary>
    ///     Регистрирует станции на карте, поднятой из сохранения.
    /// </summary>
    /// <remarks>
    ///     Станция не часть карты: <c>StationData</c>/<c>StationJobs</c> создаёт <c>InitializeNewStation</c>
    ///     во время раунда, в файле от неё остаётся только <c>BecomesStationComponent</c> у сетки. Без станции
    ///     <c>SpawnPlayers</c> падает на <c>Assert(stations.Count > 0)</c>. Поднимаем то же событие
    ///     <c>PostGameMapLoad</c>, что и тикер для обычной карты, в то же окно между загрузкой и спавном.
    ///     Состояние станции (имя, работы, банк) из сохранения не восстанавливается — в файле его нет.
    /// </remarks>
    /// <param name="suppressRoundstartGrids">
    ///     Не спавнять обязательные гриды станции на время регистрации: в сохранении они уже есть.
    /// </param>
    /// <returns>
    ///     False, если на карте нет сетки со станцией или для неё не нашлось конфигурации:
    ///     раунд на такой карте существовать не может.
    /// </returns>
    private bool TryRegisterStations(MapId mapId, bool suppressRoundstartGrids)
    {
        var grids = new List<EntityUid>();
        var stationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var alreadyRegistered = 0;

        var query = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            // Сетка уже приписана к станции — повторная регистрация ей не нужна.
            if (HasComp<StationMemberComponent>(uid))
            {
                alreadyRegistered++;
                continue;
            }

            grids.Add(uid);

            if (TryComp<BecomesStationComponent>(uid, out var becomes) && !string.IsNullOrEmpty(becomes.Id))
                stationIds.Add(becomes.Id);
        }

        if (stationIds.Count == 0)
        {
            if (alreadyRegistered > 0)
                return true;

            Log.Error($"SaveGamemode: на карте {mapId} нет ни одной сетки с BecomesStation — станцию создать нечем");
            return false;
        }

        var proto = FindGameMapPrototype(stationIds);
        if (proto == null)
        {
            Log.Error($"SaveGamemode: не найден прототип игровой карты со станциями " +
                      $"{string.Join(", ", stationIds)} — конфигурацию станции взять неоткуда");
            return false;
        }

        _gridsSuppressed.Clear();
        _registeringSavedMap = suppressRoundstartGrids;
        try
        {
            RaiseLocalEvent(new PostGameMapLoad(proto, mapId, grids, null));
        }
        finally
        {
            _registeringSavedMap = false;
        }

        Log.Info($"SaveGamemode: станции {string.Join(", ", stationIds)} по конфигу карты {proto.ID} " +
                 $"зарегистрированы на карте {mapId} (сеток: {grids.Count})");
        return true;
    }

    /// <summary>
    ///     Ищет прототип игровой карты, в конфигурации которого есть станции с такими именами.
    /// </summary>
    /// <remarks>
    ///     Имя сохранённой карты для этого не подходит: станции ищутся по <c>BecomesStationComponent.Id</c>
    ///     в словаре <c>stations</c> прототипа, и у одной карты их несколько. Карта, которую режим отменил
    ///     ради сохранения, — эталон, проверяем её первой, чтобы не начать грузить конфигурацию чужой станции.
    /// </remarks>
    private GameMapPrototype? FindGameMapPrototype(HashSet<string> stationIds)
    {
        foreach (var proto in _replacedMaps)
        {
            foreach (var stationId in stationIds)
            {
                if (proto.Stations.ContainsKey(stationId))
                    return proto;
            }
        }

        foreach (var proto in _gameMap.AllMaps())
        {
            foreach (var stationId in stationIds)
            {
                if (proto.Stations.ContainsKey(stationId))
                    return proto;
            }
        }

        return null;
    }

    /// <summary>
    ///     Будит NPC на загруженной карте.
    /// </summary>
    /// <remarks>
    ///     NPC будит только <c>OnNPCMapInit</c>, а <c>WakeNPC</c> — это <c>EnsureComp&lt;ActiveNPCComponent&gt;</c>,
    ///     без которого <c>HTNSystem.UpdateNPC</c> сущность не обрабатывает вовсе: существо стоит и ни на что
    ///     не реагирует. Из сохранения MapInit не приходит — флаг <c>mapInit</c> в файле стоит именно затем.
    ///     Спальным режимом по расстоянию это не лечится: <c>NPCSystem</c> будит лишь тех, кого усыпил сам.
    ///     Дальние существа он усыпит обратно за пятнадцать секунд, цена — одна волна планирования на старте.
    /// </remarks>
    private void WakeLoadedNpcs(MapId mapId)
    {
        var woken = 0;
        var query = AllEntityQuery<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != mapId || TerminatingOrDeleted(uid))
                continue;

            _npc.WakeNPC(uid);
            woken++;
        }

        Log.Info($"SaveGamemode: разбужено NPC на загруженной карте: {woken}");
    }

    /// <summary>
    ///     Одна попытка загрузить сохранение на <paramref name="defaultMap"/> вместо карты, созданной движком.
    /// </summary>
    /// <remarks>
    ///     Загрузка битого сохранения бросает исключение из читателя компонентов
    ///     (<c>RequiredFieldNotMappedException</c>, <c>KeyNotFoundException</c> в сериализаторе DecalGrid),
    ///     и без обёртки оно прилетает в <c>GameTicker.StartRound</c>, роняя сервер.
    ///     Инициализацию карты оставляем тикеру, иначе он получит «Map is already initialized».
    /// </remarks>
    private bool TryLoadOnce(MapId defaultMap, ResPath path, out Entity<MapComponent>? map)
    {
        map = null;

        // Удаляем карту, созданную движком (или оставшуюся от оборвавшейся загрузки),
        // и загружаем сохранение с тем же MapId.
        if (!TryClearMapSlot(defaultMap))
            return false;

        try
        {
            if (!_mapLoader.TryLoadMapWithId(defaultMap, path, out map, out _, DeserializationOptions.Default))
            {
                map = null;
                return false;
            }

            return map != null;
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: загрузка сохранения из {path} бросила исключение");
            Log.Error(e.ToString());
            map = null;
            return false;
        }
    }

    /// <summary>
    ///     Можно ли снова пользоваться <paramref name="mapId"/>: карты нет, либо она обычная.
    /// </summary>
    /// <remarks>
    ///     Оборвавшаяся загрузка оставляет карту в состоянии удаления: движок сам начинает вычищать
    ///     недосозданные сущности (<c>MapLoaderSystem.Delete</c>) и падает на середине. Такую карту нельзя
    ///     ни удалить, ни освободить, а попытки это сделать бросают исключение в <c>StartRound</c>.
    /// </remarks>
    private bool CanReuseMapSlot(MapId mapId)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid) || !Exists(mapUid))
            return true;

        return MetaData(mapUid!.Value).EntityLifeStage < EntityLifeStage.Terminating;
    }

    /// <summary>
    ///     Освободить <paramref name="mapId"/> под новую загрузку.
    /// </summary>
    /// <returns>
    ///     False, если карту нельзя ни удалить, ни переиспользовать — вызывающий обязан уйти
    ///     на запасную карту, а не пытаться грузиться на занятый MapId.
    /// </returns>
    private bool TryClearMapSlot(MapId mapId)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid) || !Exists(mapUid))
            return true;

        if (!CanReuseMapSlot(mapId))
        {
            Log.Error($"SaveGamemode: карта {mapId} зависла в состоянии удаления после оборвавшейся загрузки");
            return false;
        }

        try
        {
            _mapSystem.DeleteMap(mapId);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: не удалось удалить карту {mapId} перед загрузкой: {e}");
            return false;
        }
    }

    /// <summary>
    ///     Загружает вместо сохранения карты, которые режим отменил при старте раунда.
    /// </summary>
    /// <remarks>
    ///     Сохранение может быть синтаксически целым, но нечитаемым по составу, и тогда загрузка падает
    ///     уже после проверки файла. Раунд обязан стартовать на реальной станции: пустая карта грузится,
    ///     но распределение работ падает на отсутствии станций.
    /// </remarks>
    private bool TryRestoreReplacedMaps(MapId defaultMap)
    {
        // Fallback-карты могли не запомниться (правило запустили вручную) — берём текущую выбранную карту.
        var maps = _replacedMaps.Count > 0
            ? _replacedMaps
            : _gameMap.GetSelectedMap() is { } selected
                ? new List<GameMapPrototype> { selected }
                : new List<GameMapPrototype>();

        if (maps.Count == 0)
        {
            Log.Error("SaveGamemode: нечего возвращать вместо сохранения — выбранная карта не определена");
            return false;
        }

        for (var i = 0; i < maps.Count; i++)
        {
            var proto = maps[i];
            try
            {
                // Тот же путь, что у обычного старта раунда: LoadGameMapWithId поднимает
                // Pre/PostGameMapLoad, чтобы станция и гриды зарегистрировались как обычно.
                // Карту не инициализируем — это делает GameTicker.StartRound.
                if (i == 0)
                {
                    if (_mapSystem.MapExists(defaultMap))
                        _mapSystem.DeleteMap(defaultMap);

                    GameTicker.LoadGameMapWithId(proto, defaultMap, DeserializationOptions.Default);

                    Log.Warning($"SaveGamemode: вместо сохранения загружена карта '{proto.ID}' на {defaultMap}");
                }
                else
                {
                    GameTicker.LoadGameMap(proto, out var extraMap, DeserializationOptions.Default);
                    Log.Warning($"SaveGamemode: вместо сохранения загружена карта '{proto.ID}' на {extraMap}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"SaveGamemode: не удалось загрузить карту '{proto.ID}' вместо сохранения: {e}");
                // Недосозданная карта на нужном MapId помешает и спавну, и следующей попытке.
                TryDeleteMap(defaultMap);
                return false;
            }
        }

        return _mapSystem.MapExists(defaultMap);
    }

    /// <summary>
    ///     Крайний случай: ни сохранения, ни обычной карты. Пустая карта лучше падения старта раунда
    ///     на отсутствующей карте, но станции на ней нет — раунд без игроков и работ.
    /// </summary>
    private void TryCreateEmptyMap(MapId defaultMap)
    {
        try
        {
            if (_mapSystem.MapExists(defaultMap))
                _mapSystem.DeleteMap(defaultMap);

            _mapSystem.CreateMap(defaultMap, runMapInit: false);
            Log.Error($"SaveGamemode: вместо сохранения создана пустая карта {defaultMap} — " +
                      "на ней нет станции, раунд на ней играть нельзя");
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: не удалось восстановить пустую карту {defaultMap}: {e}");
        }
    }

    private void TryDeleteMap(MapId mapId)
    {
        try
        {
            if (_mapSystem.MapExists(mapId))
                _mapSystem.DeleteMap(mapId);
        }
        catch (Exception e)
        {
            Log.Error($"SaveGamemode: не удалось удалить карту {mapId}: {e}");
        }
    }

    private void TrySaveCurrentMap(SaveGamemodeRuleComponent component)
    {
        var mapId = GameTicker.DefaultMap;
        if (mapId == MapId.Nullspace)
        {
            Log.Warning("SaveGamemode: DefaultMap не установлен, сохранение пропущено");
            return;
        }

        Log.Info($"SaveGamemode: попытка сохранить карту {mapId} в слот '{component.SaveSlot}'");

        // Сохранение через пост-процессор: он вычищает runtime-состояние и снимает mapInit,
        // чтобы карта загрузилась как неинициализированная.
        if (_save.TrySaveFilteredSlot(mapId, component.SaveSlot, out var report))
            Log.Info($"SaveGamemode: карта {mapId} сохранена в слот '{component.SaveSlot}'. {report}");
        else
            Log.Error($"SaveGamemode: не удалось сохранить карту {mapId} в слот '{component.SaveSlot}' " +
                      $"(старое сохранение слота не тронуто)");
    }
}
